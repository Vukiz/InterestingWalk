using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace Assets.Scripts
{
  class MapContent
  {
    public Vector2 StartPosition;
    private const int Size = 10;
    private readonly float spawnRate = 1;
    private readonly int timeRestriction;

    private TimeHelper timer;
    public List<GameObject> Map;
    private List<EdgeController> edges;
    private List<VertexController> vertices;
    private List<ThreadStateToken> stateTokens;

    private bool updatedPathColoring;
    private readonly object threadStateLock;
    private object bestPathLock;
    private readonly object coloringLock;

    public int BestInterest = 0;
    public int MaxInterest;
    public readonly Stopwatch Sw = new Stopwatch();

    public GraphPath CurrentBestPath; //best path from start to end --- {Vstart, V2, .... , Vend}
    private readonly GameObject edgePrefab;
    private readonly GameObject vertexPrefab;
    private int iterations;//iterations on current alghorithm;

    public string StateTokensCount
    {
      get
      {
        lock (threadStateLock)
        {
          return stateTokens.Count.ToString();
        }
      }
    }

    public string BestPathInterest { get { return CurrentBestPath.Interest.ToString(); } }
    public string BestPathTime { get { return CurrentBestPath.Time.ToString(); } }

    public string SwCurrentTime
    {
      get
      {
        return (Sw != null && Sw.Elapsed.TotalSeconds > 0)
          ? Sw.Elapsed.TotalSeconds.ToString(CultureInfo.InvariantCulture)
          : string.Empty;
      }
    }

    public MapContent(float SpawnRate, int TimeRestriction)
    {
      timer = new TimeHelper();
      edgePrefab = Resources.Load<GameObject>("Prefabs/Edge");
      vertexPrefab = Resources.Load<GameObject>("Prefabs/Vertex");
      threadStateLock = new object();
      coloringLock = new object();
      bestPathLock = new object();
      timeRestriction = TimeRestriction;
      MaxInterest = -1;
      spawnRate = SpawnRate;
      SetBestPath(new GraphPath());
      lock (threadStateLock)
      {
        stateTokens = new List<ThreadStateToken>();
      }
      edges = new List<EdgeController>();
      vertices = new List<VertexController>();
      Map = new List<GameObject>();

    }

    /// <summary>
    /// called upon calcualtions complition
    /// </summary>
    public void FinalizeCalculations()
    {
      ClearThreads();
      if (Sw.IsRunning)
      {
        StopTimer();
      }
      if (CurrentBestPath.Interest > 0)
      {
        Debug.Log("Found path with " + CurrentBestPath.VerticesCount() + " vertices and " + CurrentBestPath.Interest + " interest");
      }
      if (iterations > 0)
      {
        Debug.Log("Found " + iterations + " possible routes");
        iterations = 0;
      }
    }

    public void ResetMap()
    {
      SetBestPath(new GraphPath());
      ClearThreads();
    }
    /// <summary>
    /// clears map : removes all edges and vertices, clears threads and stops timer. nullifies Bestpath
    /// </summary>
    public void Clear()
    {
      SetBestPath(new GraphPath());
      BestInterest = 0;
      MaxInterest = -1;
      ClearVertices();
      ClearEdges();
      Map.Clear();
      StopTimer();
      Debug.Log("Cleared map");
    }
    private void ClearVertices()
    {
      foreach (VertexController t in vertices)
      {
        Object.Destroy(t.gameObject);
      }
      vertices.Clear();
    }

    private void ClearEdges()
    {
      foreach (EdgeController t in edges)
      {
        Object.Destroy(t.gameObject);
      }
      edges.Clear();
    }

    public void ClearThreads()
    {
      lock (threadStateLock)
      {
        foreach (var token in stateTokens)
        {
          token.IsCancelled = true;
        }
        stateTokens = new List<ThreadStateToken>();
      }
      Debug.Log("Cleared all threads");
    }
    private void ClearGraphColoring()
    {
      foreach (var edge in edges)
      {
        edge.CurrentEdgeState = EdgeState.Unused;
      }
    }


    //TODO SOME UNITIY UI USAGE STILL IN THIS METHOD
    public void PrintBestPathIfNeeded()
    {
      lock (coloringLock)
      {
        if (updatedPathColoring)
        {
          return;
        }
        updatedPathColoring = true;
      }

      ClearGraphColoring();

      if (CurrentBestPath == null || CurrentBestPath.VerticesCount() == 0)
      {
        return;
      }
      lock (bestPathLock)
      {
        CurrentBestPath.ColorGraph();
        var s = CurrentBestPath.GetAllPathVertices();
        Debug.Log("Best route has " + CurrentBestPath.Interest + " interest for " + CurrentBestPath.Time +
                  " time and consists of " + s);
      }
    }

    /// <summary>
    /// finds distances from start vertex to others
    /// </summary>
    private void FordBellman()
    {
      foreach (var t in vertices)
      {
        t.DistanceFromStart = 1000000;
      }
      vertices[0].DistanceFromStart = 0;
      for (var i = 0; i < vertices.Count; i++)
      {
        foreach (var edge in edges)
        {
          if (edge.Second.DistanceFromStart > edge.First.DistanceFromStart + edge.Weight)
          {
            edge.Second.DistanceFromStart = edge.First.DistanceFromStart + edge.Weight;
          }
          if (edge.First.DistanceFromStart > edge.Second.DistanceFromStart + edge.Weight)
          {
            edge.First.DistanceFromStart = edge.Second.DistanceFromStart + edge.Weight;
          }
        }
      }
    }
    public void OnRandomizeClick()
    {
      if (edges.Any())
      {
        Clear();
      }
      FinalizeCalculations();
      RandomizeGraph();
      FordBellman();
      foreach (var v in vertices)
      {
        v.Depth = vertices.Count;
      }
      vertices[0].Depth = 0;
      SpreadDepth(vertices[0]);
      MaxInterest = vertices.Sum(v => v.Interest);
      SpreadMeasure();
    }

    private void RandomizeGraph()
    {
      RandomizeVertices();
      RandomizeEdges();
    }
    /// <summary>
    /// creates edge between two vertices by their position
    /// </summary>
    /// <param name="from"></param>
    /// <param name="to"></param>
    /// <param name="w"> weight of the edge</param>
    /// <param name="edgePrefab"></param>
    /// <param name="parentTransform"></param>
    private void CreateEdge(VertexController from, VertexController to, int w)
    {
      var edge = Object.Instantiate(edgePrefab, GameObject.Find("Edges").transform);
      edge.GetComponent<EdgeController>().Init(from, to);
      edge.GetComponent<EdgeController>().Weight = w;
      edges.Add(edge.GetComponent<EdgeController>());
    }
    private void RandomizeEdges()
    {
      var unlinkedV = new List<GameObject>(Map);
      var path = new List<GameObject> { unlinkedV.First() };
      unlinkedV.Remove(path.First());
      while (unlinkedV.Count > 0)
      {
        GameObject nextV = unlinkedV[Random.Range(0, unlinkedV.Count)];
        CreateEdge(path[Random.Range(0, path.Count - 1)].GetComponent<VertexController>(),
          nextV.GetComponent<VertexController>(), Random.Range(1, 7));
        path.Add(nextV);
        unlinkedV.Remove(nextV);
      }
    }
    /// <summary>
    /// filling map with random vertices with random interests
    /// </summary>
    private void RandomizeVertices()
    {
      CreateVertex(new Vector2(0, 0), "Vertex Start", "S");
      for (var i = 1; i < Size * 2; i += 2)
      {
        for (var j = 1; j < Size * 2; j += 2)
        {
          if (Random.value > spawnRate)
          {
            CreateVertex(new Vector2(i, j), "Vertex " + vertices.Count);
          }
        }
      }
    }

    private void CreateVertex(Vector2 startPosition, string name, string childName = null)
    {
      GameObject currentVertex = Object.Instantiate(vertexPrefab, GameObject.Find("Vertices").transform);
      currentVertex.GetComponent<VertexController>().Init(startPosition, name, childName);
      vertices.Add(currentVertex.GetComponent<VertexController>());
      Map.Add(currentVertex);
    }
    private void StopTimer()
    {
      Sw.Stop();
      Debug.Log(Sw.Elapsed);
    }

    private void StartTimer()
    {
      if (Sw.IsRunning)
      {
        return;
      }
      Sw.Reset();
      Sw.Start();
    }
    private static void SpreadDepth(VertexController currentVertex)
    {
      currentVertex.CurrentState = VertexController.VertexState.Visited;
      foreach (var v in currentVertex.GetAdjacentVertices())
      {
        if (v.Depth > currentVertex.Depth + 1)
        {
          v.Depth = currentVertex.Depth + 1;
        }
        if (v.CurrentState == VertexController.VertexState.Unvisited)
        {
          SpreadDepth(v);
        }
      }
    }

    /// <summary>
    /// calculates measure as Intererst/Time
    /// </summary>
    private float HeuristicMeasure(float interest, float time)
    {
      return interest / time;
    }
    private void SpreadMeasure()
    {
      foreach (var vertex in vertices)
      {
        vertex.CurrentState = VertexController.VertexState.Unvisited;
      }
      var maxDepth = vertices.Max(v => v.Depth);
      for (var i = 1; i < maxDepth; i++) //вверх по глубине
      {
        foreach (var currentVertex in vertices.Where(v => v.Depth == i))
        {
          foreach (var equalVertex in currentVertex.GetAdjacentVertices()
            .Where(vertex => vertex.Depth == currentVertex.Depth))
          {
            float currentMeasure;
            var edgeBetweenVertices = currentVertex.GetConnectingEdge(equalVertex);
            if (equalVertex.BestMeasure < currentVertex.BestMeasure)
            {
              currentMeasure = HeuristicMeasure(currentVertex.Interest, edgeBetweenVertices.Weight) +
                               equalVertex.BestMeasure;
              if (currentMeasure > currentVertex.BestMeasure)
              {
                currentVertex.BestMeasure = currentMeasure;
              }
            }
            else
            {
              currentMeasure = HeuristicMeasure(equalVertex.Interest, edgeBetweenVertices.Weight) +
                               currentVertex.BestMeasure;
              if (currentMeasure > equalVertex.BestMeasure)
              {
                equalVertex.BestMeasure = currentMeasure;
              }
            }
          }
        }
      }
    }

    private void SetBestPath(GraphPath newPath)
    {
        CurrentBestPath = newPath;
        BestInterest = CurrentBestPath.Interest;

      lock (coloringLock)
      {
        updatedPathColoring = false;
      }
      if (BestInterest == MaxInterest)
      {
        FinalizeCalculations();
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
          action.Invoke(startV, startPath, stoken);
        }
        finally
        {
          lock (threadStateLock)
          {
            stateTokens.Remove(stoken);
            if (stateTokens.Count == 0)
            {
              FinalizeCalculations();
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
      FinalizeCalculations();
      ResetMap();
      StartTimer();
      // запустить рекурсионный поиск из каждой вершины смежной со стартовой
      var currentVertex = vertices[0];
      var startPath = new GraphPath().Add(currentVertex);
      if (!paralleling)
      {
        StartThreadFromPool(DepthSearch, currentVertex, startPath);
      }
      else
      {
        StartThreadFromPool(ParallelDepthSearch, currentVertex, startPath);
      }
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
      if (currV.GetAdjacentVertices().All(path.Contains))
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
        if (nextV.DistanceFromStart + path.Time + nextV.GetConnectingEdge(currV).Weight > timeRestriction ||
            path.CheckVForCycle(nextV)) // если пора возвращаться - возвращаемся
        {
          backPathNeeded = true;
          continue;
        }
        StartThreadFromPool(ParallelDepthSearch, nextV, new GraphPath(path).Add(nextV));
      }
      if (backPathNeeded)
      {
        //initiated in the same thread so no new thread is created
        ParallelBackPath(currV, new GraphPath(path), token);
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
    private void ParallelBackPath(VertexController currentVertex, GraphPath path, ThreadStateToken token)
    {

      var currentPathInterest = path.Interest;
      var currentPathTime = path.Time;
      lock (currentVertex.Locker)
      {
        //если существует ветвь которая на обратном пути собрала больше интереса или такое же, но быстрее, то текущая ветвь не имеет смысла
        if (currentVertex.CurrentBestInterest > currentPathInterest
            || (currentVertex.CurrentBestInterest == currentPathInterest &&
                currentVertex.CurrentBestTime < currentPathTime))
        {
          return;
        }
        currentVertex.CurrentBestInterest = currentPathInterest;
        currentVertex.CurrentBestTime = currentPathTime;
      }
      //так как мы уже должны возвращаться мы не будем дальше искать если вернулись в начало - 
      //если бы была возможность набрать больше интереса 
      //, то такой путь найдется в другом случае - с другой последовательностью вершин
      if (currentVertex == vertices[0])
      {
        iterations++;
        lock (bestPathLock)
        {
          if (CurrentBestPath.Interest == 0 ||
              path.Interest > BestInterest ||
              path.Interest == BestInterest && path.Time < CurrentBestPath.Time)
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

        EdgeController nextEdge = currentVertex.GetConnectingEdge(nextV);

        if (nextV.DistanceFromStart + path.Time + nextEdge.Weight <= timeRestriction)
        {
          StartThreadFromPool(ParallelBackPath, nextV, new GraphPath(path).Add(nextV));
        }
      }
    }

    #endregion
    #region cosequentially realisation

    private void DepthSearch(VertexController currV, GraphPath path, ThreadStateToken token = null)
    {
      if (currV.GetAdjacentVertices().All(path.Contains))
      {
        BackPath(currV, path);
        return;
      }
      bool backPathNeeded = false; //нужно ли из этой вершины вернуться в начало
      foreach (var nextV in currV.GetAdjacentVertices())
      {
        //если перейдя в вершину мы превысим лимит по времени, то в эту вершину мы не пойдем 
        // если мы идем в вершину в которой замыкается цикл - идти туда не надо
        if (nextV.DistanceFromStart + path.Time + nextV.GetConnectingEdge(currV).Weight > timeRestriction ||
            path.CheckVForCycle(nextV))
        {
          backPathNeeded = true;
          continue;
        }
        DepthSearch(nextV, new GraphPath(path).Add(nextV));
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
      if (currentVertex.CurrentBestInterest > currentPathInterest
          || (currentVertex.CurrentBestInterest == currentPathInterest &&
              currentVertex.CurrentBestTime < currentPathTime))
      {
        return;
      }
      currentVertex.CurrentBestInterest = currentPathInterest;
      currentVertex.CurrentBestTime = currentPathTime;

      //так как мы уже должны возвращаться мы не будем дальше искать если вернулись в начало - 
      //если бы была возможность набрать больше интереса 
      //, то такой путь найдется в другом случае - с другой последовательностью вершин
      if (currentVertex == vertices[0])
      {
        iterations++;
        lock (bestPathLock)
        {
          if (CurrentBestPath.Interest == 0 ||
              path.Interest > BestInterest ||
              path.Interest == BestInterest && path.Time < CurrentBestPath.Time)
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
        EdgeController nextEdge = currentVertex.GetConnectingEdge(nextV);
        if (nextV.DistanceFromStart + path.Time + nextEdge.Weight <= timeRestriction)
        {
          BackPath(nextV, new GraphPath(path).Add(nextV));
        }
      }
    }

    #endregion


  }
}
