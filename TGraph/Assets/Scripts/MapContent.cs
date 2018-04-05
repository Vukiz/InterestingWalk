using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using Debug = UnityEngine.Debug;

namespace Assets.Scripts
{
  class MapContent
  {
    private int bestInterest;// the best interest which current alghorithm was able to get
    private int iterations;//iterations on current alghorithm;

    private bool updatedPathColoring;
    private int timeRestriction;

    public int TimeRestriction
    {
      get { return timeRestriction; }
      set
      {
        timeRestriction = value;
        Map?.HideUnreachableVertices(value);
      }
    }

    private readonly object threadStateLock;
    private readonly object bestPathLock;
    private readonly object coloringLock;
    private Map map;

    private GraphPath currentBestPath; //best path from start to end --- {Vstart, V2, .... , Vend}
    private readonly List<ThreadStateToken> stateTokens;

    private readonly Stopwatch sw;

    public int ThreadsСount = 0;

    public string BestPathTime => currentBestPath.Time.ToString();

    public string BestPathInterest => currentBestPath.Interest.ToString();

    public bool IsMapEmpty => Map.IsEmpty();

    public string SwCurrentTime => sw != null && sw.Elapsed.TotalSeconds > 0
      ? sw.Elapsed.TotalSeconds.ToString("0.000",CultureInfo.InvariantCulture)
      : string.Empty;

    public Map Map
    {
      get { return map; }
      set { map = value; }
    }

    public MapContent(float spawnRate, int Restriction)
    {
      Map = new Map(spawnRate);
      sw = new Stopwatch();
      threadStateLock = new object();
      coloringLock = new object();
      bestPathLock = new object();

      TimeRestriction = Restriction;
      SetBestPath(new GraphPath());
      stateTokens = new List<ThreadStateToken>();
    }

    /// <summary>
    ///stops timer and outputs result
    /// </summary>
    public void StopTimerAndOutputResult()
    {
      //Debug.Log("Timer Stopped");
      if (sw.IsRunning)
      {
        StopTimer();
      }
      if (currentBestPath.Interest > 0)
      {
        Debug.Log("Found path with " + currentBestPath.VerticesCount + " vertices, " + currentBestPath.Interest + " interest, " + currentBestPath.Time + " time");
        Debug.Log("Found " + iterations + " possible routes");
        iterations = 0;
       // Debug.Log("Algorithm calls : " + CallsCounter);
      }
    }

    /// <summary>
    /// clears map : removes all edges and vertices, clears threads. nullifies Bestpath
    /// </summary>
    public void Clear()
    {
      SetBestPath(new GraphPath());
      bestInterest = 0;
      Map.Clear();
      //Debug.Log("Cleared map");
    }

    /// <summary>
    /// do not surroind this method with threadStateLock lock
    /// </summary>
    public void ClearThreads()
    {
      lock (threadStateLock)
      {
        foreach (var token in stateTokens)
        {
          token.IsCancelled = true;
        }
      }
     // Debug.Log("Cleared all threads");
    }

    public void PrintBestPathIfNeeded()
    {
      if (updatedPathColoring) //not locking here because it doesn't matter if we get wrong value
      {
        return;
      }

      lock (coloringLock)
      {
        updatedPathColoring = true;
      }

      Map.ClearColoring();

      if (currentBestPath == null || currentBestPath.VerticesCount == 0)
      {
        return;
      }
      currentBestPath.ColorGraph();
      //var s = currentBestPath.GetAllPathVertices();
     // Debug.Log("Best route has " + currentBestPath.Interest + " interest for " + currentBestPath.Time + " time and consists of " + s);
    }

    public void OnRandomizeClick()
    {
      ResetMapIfNeeded();
      Map.Randomize();
      Map.HideUnreachableVertices(TimeRestriction);
    }
    public void InitMapFromJson(MapWrapper mapWrapper)
    {
      if (mapWrapper == null)
      {
        return;
      }
      ResetMapIfNeeded();
      Map.InitFromJson(mapWrapper);
      Map.HideUnreachableVertices(TimeRestriction);
    }

    private void ResetMapIfNeeded()
    {
      if (map.IsEmpty())
      {
        return;
      }
      StopTimerAndOutputResult();
      Clear();
      ClearThreads();
    }

    private void StopTimer()
    {
      if (!sw.IsRunning)
      {
        return;
      }
      sw.Stop();
      Debug.Log(sw.Elapsed);
    }

    private void StartTimer()
    {
      if (sw.IsRunning)
      {
        return;
      }
      sw.Reset();
      sw.Start();
    }
    
    private void SetBestPath(GraphPath newPath)
    {
      currentBestPath = newPath;
      bestInterest = currentBestPath.Interest;
      updatedPathColoring = false;  
      if (bestInterest == map.MaxInterest)
      {
        ClearThreads();
        StopTimerAndOutputResult();
      }
    }

    private void StartThreadFromPool(Action<GraphPath, ThreadStateToken> action, GraphPath startPath)
    {
      ThreadStateToken token = new ThreadStateToken();
      Action<object> wrappedAction = s =>
      {
        ThreadStateToken stoken = (ThreadStateToken)s;
        try
        {
          lock (threadStateLock)
          {
            ThreadsСount++;
          }
          //Debug.Log("Starting with path of " + startPath.VerticesCount + " " + startPath.CurrentVertex.Name);
          action.Invoke(startPath, stoken);

        }
        catch (Exception e)
        {
          Debug.Log(e.Message);
        }
        finally
        {
          lock (threadStateLock)
          {
            ThreadsСount--;
            if (stateTokens.Contains(stoken))
            {
              stateTokens.Remove(stoken);
              if (stateTokens.Count == 0)
              {
                StopTimerAndOutputResult();
              }
            }
          }
        }
      };

      lock (threadStateLock)
      {
        stateTokens.Add(token);
      }
      ThreadPool.QueueUserWorkItem(s => wrappedAction(s), token);
    }

    /// <summary>
    /// алгоритм в два шага DepthSearch и BackPath вызываемый из него
    /// </summary>
    /// <param name="paralleling"></param>
    public void FindPath(bool paralleling)
    {
      StopTimerAndOutputResult();
      ClearThreads();
      while (stateTokens.Any())
      {
        Thread.Sleep(100);
      }
      SetBestPath(new GraphPath());
      ThreadsСount = 0;
      Map.ResetVertices();
      StartTimer();
      // запустить рекурсионный поиск из каждой вершины смежной со стартовой
      var currentVertex = Map.StartVertex;
      var startPath = new GraphPath().Add(currentVertex);
      if (paralleling)
      {
        StartThreadFromPool(ParallelDepthSearch, startPath);
      }
      else
      {
        CallsCounter = 0;
        StartThreadFromPool(DepthSearch, startPath);
      }
    }

    private bool AllNeighboursUsed(VertexController currV, GraphPath path)
    {
      return currV.GetAdjacentVertices().All(path.Contains);
    }

    private bool CheckConditions(VertexController currV, GraphPath path, VertexController nextV)
    {
      //если перейдя в вершину мы превысим лимит по времени, то в эту вершину мы не пойдем 
      // если мы идем в вершину в которой замыкается цикл - идти туда не надо
      return nextV.DistanceFromStart + path.Time + nextV.GetConnectingEdge(currV).Weight > TimeRestriction || path.CheckVForCycle(nextV);
    }

    private bool CheckBranchForRedundancy(VertexController currentVertex, int currentPathInterest, int currentPathTime)
    {
      return currentVertex.CurrentBestTime > 0
             && (currentVertex.CurrentBestInterest > currentPathInterest
                 || currentVertex.CurrentBestInterest == currentPathInterest
                 && currentVertex.CurrentBestTime < currentPathTime);
    }

    private bool IsPathBetter(GraphPath path)
    {
      return currentBestPath.Interest == 0 ||
             path.Interest > bestInterest ||
             path.Interest == bestInterest && path.Time < currentBestPath.Time;
    }

    #region Parallel realisation

    /// <summary>
    /// вызывается на первом шаге когда мы двигаемся до тех пор пока не настанет время идти назад (BackPath)
    /// </summary>
    /// <param name="currV"></param>
    /// <param name="path"></param>
    /// <param name="token"></param>
    private void ParallelDepthSearch(GraphPath path, ThreadStateToken token)
    {
      VertexController currV = path.CurrentVertex;
      //если мы посетили все вершины, то дальше нет смысла искать и нужно вернуться в конечную точку

      if (AllNeighboursUsed(currV, path)) // Если все смежные использованы значит нужно вернуться ближе к началу
      {
        if (currV == map.StartVertex) // если больше некуда идти то закончить
        {
          ParallelBackPath(path, token);
          return;
        }
        var nextV = currV.GetAdjacentVertices().OrderBy(v => v.DistanceFromStart).FirstOrDefault();//возвращаться назад, но не заканчивать поиск
        ParallelDepthSearch(path.Add(nextV), token);
        return;
      }
      bool backPathNeeded = false; //нужно ли из этой вершины вернуться в начало
      IOrderedEnumerable<VertexController> sortedNeighbours = currV.GetAdjacentVertices().OrderByDescending(v => v.BestMeasure);
      foreach (var nextV in sortedNeighbours)
      {
        if (token.IsCancelled)
        {
          return;
        }
        if (CheckConditions(currV, path, nextV)) // если пора возвращаться - возвращаемся
        {
          backPathNeeded = true;
          continue;
        }
        if (currV == map.StartVertex)
        {
          StartThreadFromPool(ParallelDepthSearch, new GraphPath(path).Add(nextV));
        }
        else
        {
          ParallelDepthSearch(new GraphPath(path).Add(nextV), token);
        }
      }
      if (backPathNeeded)
      {
        //initiated in the same thread so no new thread is created
        ParallelBackPath(path, token);
      }
    }

    /// <summary>
    /// Trying to find best path to start
    /// adds vertices to current path 
    /// вызывается на втором шаге когда мы находимся в вершине после которой решаем идти назад в стартовую позицию
    /// такой же алгоритм как и DepthSearch но с другим условием выхода
    /// </summary>
    /// <param name="currentVertex"> from</param>
    /// <param name="path"></param>
    /// <param name="token"></param>
    private void ParallelBackPath(GraphPath path, ThreadStateToken token)
    {
      VertexController currentVertex = path.CurrentVertex;
      var currentPathInterest = path.Interest;
      var currentPathTime = path.Time;
      lock (currentVertex.Locker)
      {
        //если существует ветвь которая на обратном пути собрала больше интереса или такое же, но быстрее, то текущая ветвь не имеет смысла
        if (CheckBranchForRedundancy(currentVertex, currentPathInterest, currentPathTime))
        { 
          return;
        }
        currentVertex.CurrentBestInterest = currentPathInterest;
        currentVertex.CurrentBestTime = currentPathTime;
        if (currentVertex == map.StartVertex)
        {
          iterations++;
        }
      }
      //так как мы уже должны возвращаться мы не будем дальше искать если вернулись в начало - 
      //если бы была возможность набрать больше интереса 
      //, то такой путь найдется в другом случае - с другой последовательностью вершин
      if (currentVertex == map.StartVertex)
      {
        lock (bestPathLock)
        {
          if (IsPathBetter(path))
          {
            SetBestPath(path);//TODO УБРАТЬ BESTPATH ПОТОМУ ЧТО В ВЕРШИНАХ И ТАК ХРАНИМ ВСЕ НУЖНОЕ В СТАРТЕ В КОНЦЕ БУДЕТ ПРАВИЛНО ИЗ_ЗА ТОГО ЧТО ВЫШЕ
          }
        }
        return;
      }
      //Если есть цикл то нам такое не надо
      //по каждой смежной вершине в которой (расстояние из нее + время ребра до нее + текущее время пути  <= ограничения
      foreach (var nextV in currentVertex.GetAdjacentVertices().Where(nextV => !path.CheckVForCycle(nextV)))
      {
        if (token.IsCancelled)
        {
          return;
        }

        if (nextV.DistanceFromStart + path.Time + currentVertex.GetConnectingEdge(nextV).Weight <= TimeRestriction)
        {
          ParallelBackPath(new GraphPath(path).Add(nextV), token);
        }
      }
    }
    #endregion

    #region cosequentially realisation

    private int CallsCounter = 0;
    private void DepthSearch(GraphPath path, ThreadStateToken token = null)
    {
      CallsCounter++;
      VertexController currV = path.CurrentVertex;
      if (AllNeighboursUsed(currV, path)) // Если все смежные использованы значит нужно вернуться ближе к началу
      {
        if (currV == map.StartVertex) // если больше некуда идти то закончить
        {
          BackPath(path);
          return;
        }
        var nextV = currV.GetAdjacentVertices().OrderBy(v => v.DistanceFromStart).FirstOrDefault();//возвращаться назад, но не заканчивать поиск
        DepthSearch(path.Add(nextV));
        return;
      }

      bool backPathNeeded = false; //нужно ли из этой вершины вернуться в начало
      foreach (var nextV in currV.GetAdjacentVertices().OrderByDescending(v => v.BestMeasure))// проверяем только те вершины в которых еще не были (отсортировано по уыванию меры чтобы отсеять неэффективные пути
      {
        //Мы ставим флаг о необходимости вернуться назад если следующая вершина нарушит наши ограничения ( время ) или получим цикл
        if (CheckConditions(currV, path, nextV)) // Если нужно вернуться назад то ставим флаг и идем дальше
        {
          backPathNeeded = true;
          continue;
        }
        DepthSearch(new GraphPath(path).Add(nextV)); // То что из этой вершины нужно вернуться
      }
      if (backPathNeeded)
      {
        BackPath(path);
      }
    }

    private void BackPath(GraphPath path)
    {
      CallsCounter++;
      VertexController currentVertex = path.CurrentVertex;
      var currentPathInterest = path.Interest;
      var currentPathTime = path.Time;
      //если существует ветвь которая на обратном пути собрала больше интереса или такое же, но быстрее, то текущая ветвь не имеет смысла
      if(CheckBranchForRedundancy(currentVertex, currentPathInterest, currentPathTime))
      {
        return;
      }
      currentVertex.CurrentBestInterest = currentPathInterest;
      currentVertex.CurrentBestTime = currentPathTime;

      //так как мы уже должны возвращаться мы не будем дальше искать если вернулись в начало - 
      //если бы была возможность набрать больше интереса 
      //, то такой путь найдется в другом случае - с другой последовательностью вершин
      if (currentVertex == map.StartVertex)
      {
        iterations++;
        if (IsPathBetter(path))
        {
          SetBestPath(path);//TODO УБРАТЬ BESTPATH ПОТОМУ ЧТО В ВЕРШИНАХ И ТАК ХРАНИМ ВСЕ НУЖНОЕ В СТАРТЕ В КОНЦЕ БУДЕТ ПРАВИЛНО ИЗ_ЗА ТОГО ЧТО ВЫШЕ
        }
        return;
      }
      //Если есть цикл то нам такое не надо
      //по каждой смежной вершине в которой (расстояние из нее + время ребра до нее + текущее время пути  <= ограничения
      foreach (var nextV in currentVertex.GetAdjacentVertices().Where(nextV => !path.CheckVForCycle(nextV)))
      {
        if (CheckConditions(currentVertex, path, nextV))
        {
          continue;
        }
        BackPath(new GraphPath(path).Add(nextV));
      }
    }

    #endregion
  }
}
