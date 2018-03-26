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

    private readonly int timeRestriction;
    private readonly object threadStateLock;
    private readonly object bestPathLock;
    private readonly object coloringLock;
    private Map map;

    private GraphPath currentBestPath; //best path from start to end --- {Vstart, V2, .... , Vend}
    private List<ThreadStateToken> stateTokens;

    private readonly Stopwatch sw;

    public int ThreadsСount = 0;

    public string BestPathTime => currentBestPath.Time.ToString();

    public string BestPathInterest => currentBestPath.Interest.ToString();

    public bool IsMapEmpty => map.IsEmpty();

    public string SwCurrentTime => sw != null && sw.Elapsed.TotalSeconds > 0
      ? sw.Elapsed.TotalSeconds.ToString("0.000",CultureInfo.InvariantCulture)
      : string.Empty;

    public Map Map
    {
      get { return map; }
      set { map = value; }
    }

    public MapContent(float spawnRate, int TimeRestriction)
    {
      map = new Map(spawnRate);
      sw = new Stopwatch();
      threadStateLock = new object();
      coloringLock = new object();
      bestPathLock = new object();

      timeRestriction = TimeRestriction;
      SetBestPath(new GraphPath());
      stateTokens = new List<ThreadStateToken>();
    }

    /// <summary>
    ///stops timer and outputs result
    /// </summary>
    public void StopTimerAndOutputResult()
    {
      Debug.Log("Timer Stopped");
      if (sw.IsRunning)
      {
        StopTimer();
      }
      if (currentBestPath.Interest > 0)
      {
        Debug.Log("Found path with " + currentBestPath.VerticesCount() + " vertices and " + currentBestPath.Interest + " interest");
      }
      if (iterations > 0)
      {
        Debug.Log("Found " + iterations + " possible routes");
        iterations = 0;
      }
    }

    /// <summary>
    /// clears map : removes all edges and vertices, clears threads. nullifies Bestpath
    /// </summary>
    public void Clear()
    {
      SetBestPath(new GraphPath());
      bestInterest = 0;
      map.Clear();
      Debug.Log("Cleared map");
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
      Debug.Log("Cleared all threads");
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

      map.ClearColoring();

      if (currentBestPath == null || currentBestPath.VerticesCount() == 0)
      {
        return;
      }
      currentBestPath.ColorGraph();
      var s = currentBestPath.GetAllPathVertices();
      Debug.Log("Best route has " + currentBestPath.Interest + " interest for " + currentBestPath.Time +
                " time and consists of " + s);
    }

    public void OnRandomizeClick()
    {
      ResetMapIfNeeded();
      map.Randomize();
      map.Prepare();
    }
    public void InitMapFromJson(MapWrapper mapWrapper)
    {
      ResetMapIfNeeded();
      map.InitFromJson(mapWrapper);
      map.Prepare();
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
      updatedPathColoring = false;//TODO should coloring lock be here?  
      if (bestInterest == map.MaxInterest)
      {
        ClearThreads();
        StopTimerAndOutputResult();
      }
    }

    private void StartThreadFromPool(Action<VertexController, GraphPath, ThreadStateToken> action,
      VertexController startV, GraphPath startPath)
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
          action.Invoke(startV, startPath, stoken);
          
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
      map.ResetVertices();
      StartTimer();
      // запустить рекурсионный поиск из каждой вершины смежной со стартовой
      var currentVertex = map.StartVertex;
      var startPath = new GraphPath().Add(currentVertex);
      if (paralleling)
      {
        StartThreadFromPool(ParallelDepthSearch, currentVertex, startPath);
      }
      else
      {
        StartThreadFromPool(DepthSearch, currentVertex, startPath);
      }
    }

    private bool AllNeighboursUsed(VertexController currV, GraphPath path)
    {
      return currV.GetAdjacentVertices().All(path.Contains);
    }

    private bool BackPathNeeded(VertexController currV, GraphPath path, VertexController nextV)
    {
      //если перейдя в вершину мы превысим лимит по времени, то в эту вершину мы не пойдем 
      // если мы идем в вершину в которой замыкается цикл - идти туда не надо
      return nextV.DistanceFromStart + path.Time + nextV.GetConnectingEdge(currV).Weight > timeRestriction || path.CheckVForCycle(nextV);
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
    private void ParallelDepthSearch(VertexController currV, GraphPath path, ThreadStateToken token)
    {
      //если мы посетили все вершины, то дальше нет смысла искать и нужно вернуться в конечную точку
      if (AllNeighboursUsed(currV, path))
      {
        ParallelBackPath(currV, path, token);
        return;
      }
      bool backPathNeeded = false; //нужно ли из этой вершины вернуться в начало
      foreach (var nextV in currV.GetAdjacentVertices())
      {
        if (token.IsCancelled)
        {
          return;
        }
        if (BackPathNeeded(currV, path, nextV)) // если пора возвращаться - возвращаемся
        {
          backPathNeeded = true;
          continue;
        }
        StartThreadFromPool(ParallelDepthSearch, nextV, new GraphPath(path).Add(nextV));
      }
      if (backPathNeeded)
      {
        //initiated in the same thread so no new thread is created
        ParallelBackPath(currV, path, token);
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
    private void ParallelBackPath(VertexController currentVertex, GraphPath path, ThreadStateToken token)
    {
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
      }
      //так как мы уже должны возвращаться мы не будем дальше искать если вернулись в начало - 
      //если бы была возможность набрать больше интереса 
      //, то такой путь найдется в другом случае - с другой последовательностью вершин
      if (currentVertex == map.StartVertex)
      {
        iterations++;
        lock (bestPathLock)
        {
          if (IsPathBetter(path))
          {
            SetBestPath(path);
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

        if (nextV.DistanceFromStart + path.Time + currentVertex.GetConnectingEdge(nextV).Weight <= timeRestriction)
        {
          StartThreadFromPool(ParallelBackPath, nextV, new GraphPath(path).Add(nextV));
        }
      }
    }
    #endregion

    #region cosequentially realisation

    private void DepthSearch(VertexController currV, GraphPath path, ThreadStateToken token = null)
    {
      if (AllNeighboursUsed(currV, path)) // Если все смежные использованы значит мы ничего не пропустили и нужно возвращаться
      {
        BackPath(currV, path);
        return;
      }

      bool backPathNeeded = false; //нужно ли из этой вершины вернуться в начало
      foreach (var nextV in currV.GetAdjacentVertices())
      {
        if (BackPathNeeded(currV, path, nextV)) // Если нужно вернуться назад то ставим флаг и идем дальше
        {
          backPathNeeded = true;
          continue;
        }
        DepthSearch(nextV, new GraphPath(path).Add(nextV)); // То что из этой вершины нужно вернуться
      }
      if (backPathNeeded)
      {
        BackPath(currV, path);
      }
    }

    private void BackPath(VertexController currentVertex, GraphPath path)
    {
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
        lock (bestPathLock)
        {
          if (IsPathBetter(path))
          {
            SetBestPath(path);
          }
        }
        return;
      }
      //Если есть цикл то нам такое не надо
      //по каждой смежной вершине в которой (расстояние из нее + время ребра до нее + текущее время пути  <= ограничения
      foreach (var nextV in currentVertex.GetAdjacentVertices().Where(nextV => !path.CheckVForCycle(nextV)))
      {
        if (nextV.DistanceFromStart + path.Time + currentVertex.GetConnectingEdge(nextV).Weight <= timeRestriction)
        {
          BackPath(nextV, new GraphPath(path).Add(nextV));
        }
      }
    }

    #endregion
  }
}
