using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;
using Random = UnityEngine.Random;

namespace Assets.Scripts
{
  public class MapСontroller : MonoBehaviour
  {
    public Vector2 StartPosition;
    public GraphPath BestPath;
    private bool paralleling = true;
    private Toggle parallelToggle;
    public float SpawnRate;
    public int TimeRestriction;

    public int BestInterest;
    public int MaxInterest;

    private int iterations;
    private bool updatedPathColoring;
    private const int Size = 10;

    private GameObject vertexPrefab;
    private GameObject edgePrefab;

    private Text threadsStatusText;
    private Text pathInterestText;
    private Text pathTimeText;

    private List<GameObject> map;
    private List<EdgeController> edges;
    private List<VertexController> vertices;
    private List<ThreadStateToken> stateTokens;

    private object threadStateLock;
    private object coloringLock;

    private Button findBtn;
    private Button randomizeBtn;

    private Stopwatch sw;

    // Use this for initialization
    private void Start()
    {
      threadStateLock = new object();
      coloringLock = new object();
      Init();
    }

    private void PrintBestPath()
    {
      updatedPathColoring = true;

      if (BestPath == null || BestPath.VerticesCount() == 0)
      {
        return;
      }

      foreach (var edge in edges)
      {
        edge.CurrentEdgeState = EdgeController.EdgeState.Unused;
      }
      BestPath.ColorGraph();
      var s = BestPath.GetPathFullVertices();
      Debug.Log("Best route has " + BestPath.Interest + " interest for " + BestPath.Time + " time and consists of ");
      Debug.Log(s);
    }

    private void RandomizeGraph()
    {
      RandomizeVertices();
      RandomizeEdges();
    }

    public void OnRandomizeButtonClick()
    {
      Clear();
      RandomizeGraph();
      findBtn.interactable = true;
    }
    public void OnFindBtnClick()
    {
      parallelToggle.enabled = false;
      findBtn.interactable = false;
      FordBellman();
      foreach (var v in vertices)
      {
        v.Depth = vertices.Count;
      }
      vertices[0].Depth = 0;
      SpreadDepth(vertices[0]);
      MaxInterest = vertices.Sum(v => v.Interest);
      FindPath();
    }

    private void OnParallelToggle(bool value)
    {
      paralleling = value;
      if (value)
      {
        InvokeRepeating("UpdateThreadStatusText", 0.2f, 1f);
        threadsStatusText.gameObject.SetActive(true);
      }
      else
      {
        CancelInvoke("UpdateThreadStatusText");
        threadsStatusText.gameObject.SetActive(false);

      }
    }
    /// <summary>
    /// called once upon application start
    /// </summary>
    private void Init()
    {
      threadsStatusText = GameObject.Find("ThreadsStatus").GetComponent<Text>();
      findBtn = GameObject.Find("FindBtn").GetComponent<Button>();
      randomizeBtn = GameObject.Find("RandomizeBtn").GetComponent<Button>();
      edgePrefab = Resources.Load<GameObject>("Prefabs/Edge");
      vertexPrefab = Resources.Load<GameObject>("Prefabs/Vertex");
      pathTimeText = GameObject.Find("PathTime").GetComponent<Text>();
      pathInterestText = GameObject.Find("PathInterest").GetComponent<Text>();
      parallelToggle = GameObject.Find("ParallelToggle").GetComponent<Toggle>();

      parallelToggle.onValueChanged.AddListener(OnParallelToggle);
      findBtn.onClick.AddListener(OnFindBtnClick);
      randomizeBtn.onClick.AddListener(OnRandomizeButtonClick);

      InvokeRepeating("UpdatePathTime", 0.2f, 1f);
      InvokeRepeating("UpdatePathInterest", 0.2f, 1f);
      OnParallelToggle(paralleling);//true by default 
      Clear();
    }
    private void Clear()
    {
      if (iterations > 0)
      {
        Debug.Log("Found " + iterations + " possible routes");
        iterations = 0;
      }
      findBtn.interactable = false;
      lock (coloringLock)
      {
        updatedPathColoring = false;
        BestPath = new GraphPath();
      }

      if (paralleling)
      {
        ClearThreads();
        UpdateThreadStatusText();
      }
      BestInterest = 0;
      ClearVertices();
      ClearEdges();
      ClearMap();
      StopTimer();
      Debug.Log("Cleared map");
    }
    private void ClearMap()
    {
      if (map == null)
      {
        map = new List<GameObject>();
      }
      else
      {
        map.Clear();
      }
    }
    private void ClearVertices()
    {

      if (vertices != null)
      {
        foreach (VertexController t in vertices)
        {
          Destroy(t.gameObject);
        }
        vertices.Clear();
      }
      else
      {
        vertices = new List<VertexController>();
      }
    }
    private void ClearEdges()
    {
      if (edges != null)
      {
        foreach (EdgeController t in edges)
        {
          Destroy(t.gameObject);
        }
        edges.Clear();
      }
      else
      {
        edges = new List<EdgeController>();
      }
    }

    private void StopTimer()
    {
      if (sw == null) return;
      sw.Stop();
      Debug.Log(sw.Elapsed);
      sw = null;
    }

    private void StartTimer()
    {
      if (sw != null)
      {
        if (sw.IsRunning)
        {
          return;
        }
      }
      else
      {
        sw = new Stopwatch();
      }
      sw.Start();
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
      /*foreach (var v in vertices)
      {
          Debug.Log("Distance to "+v.name + " is "+v.DistanceFromStart);
      }*/
    }

    private void Update()
    {
      lock (coloringLock)
      {
        GameObject.Find("Updated").GetComponent<Text>().text = updatedPathColoring.ToString();
        if (!updatedPathColoring)
        {
          PrintBestPath();
        }
      }
      
      lock (threadStateLock)
      {
        if (!paralleling || stateTokens.Count == 0 || stateTokens.All(token => token.IsCancelled))
        {
          return;
        }
      }
      if (BestInterest == MaxInterest)
      {
        ClearThreads();
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
      for (var i = 0; i < maxDepth; i++)//вверх по глубине
      {
        foreach (var currentVertex in vertices.Where(v => v.Depth == i))
        {
          foreach (var equalVertex in currentVertex.GetAdjacentVertices().Where(vertex => vertex.Depth == currentVertex.Depth))
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
    /// <summary>
    /// алгоритм в два шага DepthSearch и BackPath вызываемый из него
    /// </summary>
    private void FindPath()
    {
      var currentVertex = vertices[0];
      StartTimer();
      // запустить рекурсионный поиск из каждой вершины смежной со стартовой
      // .Where(vert => vert.CurrentState == VertexController.VertexState.Unvisited
      if (!paralleling)
      {
        ThreadPool.QueueUserWorkItem(s => DepthSearch(currentVertex, new GraphPath().Add(currentVertex)));
      }
      else
      {
        StartThreadFromPool(ParallelDepthSearch, currentVertex, new GraphPath().Add(currentVertex));
      }
    }
    
    private void StartThreadFromPool(Action<VertexController, GraphPath, ThreadStateToken> action,
      VertexController startV, GraphPath startPath)
    {
      ThreadStateToken token = new ThreadStateToken();
      Action<object> wrappedAction = s =>
      {
        ThreadStateToken stoken = (ThreadStateToken) s;
        try
        {
          action.Invoke(startV, startPath, stoken);
        }
        finally
        {
          lock (threadStateLock)
          {
            stateTokens.Remove(stoken);
          }
        }
      };
      lock (threadStateLock)
      {
        stateTokens.Add(token);
      }
      ThreadPool.QueueUserWorkItem(s => wrappedAction(s), token);
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
        if (nextV.DistanceFromStart + path.Time + nextV.GetConnectingEdge(currV).Weight > TimeRestriction ||
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
        Debug.Log("Found path with " + path.VerticesCount() + " vertices and " + path.Interest + " interest");
        lock (coloringLock)
        {
          if (path.Interest > BestInterest ||
              path.Interest == BestInterest && path.Time < BestPath.Time)
          {
            Debug.Log("Replacing best path with current one");
            BestPath = path;
            BestInterest = BestPath.Interest;
            updatedPathColoring = false;
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

        if (nextV.DistanceFromStart + path.Time + nextEdge.Weight <= TimeRestriction)
        {
          StartThreadFromPool(ParallelBackPath, nextV, new GraphPath(path).Add(nextV));
        }
      }
    }
    #endregion
    #region cosequentially realisation

    private void DepthSearch(VertexController currV, GraphPath path)
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
        if (nextV.DistanceFromStart + path.Time + nextV.GetConnectingEdge(currV).Weight > TimeRestriction ||
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
        || (currentVertex.CurrentBestInterest == currentPathInterest && currentVertex.CurrentBestTime < currentPathTime))
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
        lock (coloringLock)
        {
          iterations++;
          Debug.Log("Found path with " + path.VerticesCount() + " vertices and " + path.Interest + " interest");
          if (path.Interest > BestInterest ||
              path.Interest == BestInterest && path.Time < BestPath.Time)
          {
            Debug.Log("Replacing best path with current one");
            BestPath = path;
            BestInterest = BestPath.Interest;
            updatedPathColoring = false;
          }
        }
        return;
      }
      //Если есть цикл то нам такое не надо
      //по каждой смежной вершине в которой (расстояние из нее + время ребра до нее + текущее время пути  <= ограничения
      foreach (var nextV in currentVertex.GetAdjacentVertices().Where(nextV => !path.CheckVForCycle(nextV)))
      {
        EdgeController nextEdge = currentVertex.GetConnectingEdge(nextV);
       //if(nextEdge)
        if (nextV.DistanceFromStart + path.Time + nextEdge.Weight <= TimeRestriction)
        {
          BackPath(nextV, new GraphPath(path).Add(nextV));
        }
      }
    }
#endregion

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
    /// creating edge between two vertices by their position
    /// </summary>
    /// <param name="from"></param>
    /// <param name="to"></param>
    /// <param name="w"> weight of the edge</param>
    private void CreateEdge(VertexController from, VertexController to, int w)
    {
      var edge = Instantiate(edgePrefab, transform.Find("Edges"));
      edge.GetComponent<EdgeController>().Init(from, to);
      edge.GetComponent<EdgeController>().Weight = w;
      edges.Add(edge.GetComponent<EdgeController>());
    }

    /// <summary>
    /// filling map with random vertices with random interests
    /// </summary>
    private void RandomizeVertices()
    {
      StartPosition = new Vector2(0, 0);
      var currentVertex = Instantiate(vertexPrefab, transform.Find("Vertices"));
      currentVertex.transform.position = StartPosition;
      currentVertex.GetComponentInChildren<TextMesh>().text = "S";
      currentVertex.name = "Vertex Start";
      vertices.Add(currentVertex.GetComponent<VertexController>());
      map.Add(currentVertex);
      for (var i = 1; i < Size * 2; i += 2)
      {
        for (var j = 1; j < Size * 2; j += 2)
        {
          if (Random.value > SpawnRate)
          {
            var vertex = Instantiate(vertexPrefab, transform.Find("Vertices"));
            vertex.transform.position = new Vector2(i, j);
            vertex.GetComponent<VertexController>().RandomizeInterest();
            vertex.name = "Vertex " + vertices.Count;
            map.Add(vertex);
            vertices.Add(vertex.GetComponent<VertexController>());
          }
        }
      }
    }

    private void RandomizeEdges()
    {
      var unlinkedV = new List<GameObject>(map);
      var path = new List<GameObject> { unlinkedV.First() };
      unlinkedV.Remove(path.First());
      while (unlinkedV.Count > 0)
      {
        var nextV = unlinkedV[Random.Range(0, unlinkedV.Count)];
        CreateEdge(path[Random.Range(0, path.Count - 1)].GetComponent<VertexController>(), nextV.GetComponent<VertexController>(), Random.Range(1, 7));
        path.Add(nextV);
        unlinkedV.Remove(nextV);
      }
    }

    private void UpdatePathTime()
    {
     pathTimeText.text = BestPath.Time.ToString();
    }

    private void UpdatePathInterest()
    {
      pathInterestText.text = BestPath.Interest.ToString();
    }

    private void UpdateThreadStatusText()
    {
      lock (threadStateLock)
      {
        threadsStatusText.text = stateTokens.Count.ToString();
      }
    }

    private void OnApplicationQuit()
    {
      ClearThreads();
    }
    private void ClearThreads()
    {
      if (!paralleling) return;
      lock (threadStateLock)
      {
        if (stateTokens == null)
        {
          stateTokens = new List<ThreadStateToken>();
        }
        else
        {
          foreach (var token in stateTokens)
          {
            token.IsCancelled = true;
          }
          stateTokens = new List<ThreadStateToken>();
        }
      }
      Debug.Log("Cleared all threads");
    }
  }
}
