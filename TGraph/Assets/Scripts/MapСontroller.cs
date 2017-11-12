using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace Assets.Scripts
{
  public class MapСontroller : MonoBehaviour
  {
    public Vector2 StartPosition;
    public volatile List<VertexController> BestPath;
    public bool Paralleling = true;
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
    private object stateLock;

    private Button findBtn;
    private Button randomizeBtn;

    private readonly Button.ButtonClickedEvent findButtonClickedEvent = new Button.ButtonClickedEvent();
    private readonly Button.ButtonClickedEvent randomizeButtonClickedEvent = new Button.ButtonClickedEvent();

    // Use this for initialization
    private void Start()
    {
      stateLock = new object();
      Init();
    }

    private void PrintBestPath(List<VertexController> path)
    {
      updatedPathColoring = true;
      ColorBestPath(path);
      var s = path.Aggregate("", (current, v) => current + (" " + v.name));
      Debug.Log("Best route has " + CountInterest(path) + " interest for " + CountTime(path) + " time and consists of ");
      Debug.Log(s);
    }
    private void ColorBestPath(List<VertexController> path)
    {
      Debug.Log("Printing best path");
      foreach (var edge in edges)
      {
        edge.CurrentEdgeState = EdgeController.EdgeState.Unused;
      }
      for (var i = 0; i < path.Count - 1; i++)
      {
        edges.Find(e => e.IsConnecting(path[i], path[i + 1])).CurrentEdgeState = EdgeController.EdgeState.Used;
      }
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

      findButtonClickedEvent.AddListener(OnFindBtnClick);
      findBtn.onClick = findButtonClickedEvent;

      randomizeButtonClickedEvent.AddListener(OnRandomizeButtonClick);
      randomizeBtn.onClick = randomizeButtonClickedEvent;

      InvokeRepeating("UpdatePathTime", 0.2f, 1f);
      InvokeRepeating("UpdatePathInterest", 0.2f, 1f);
      InvokeRepeating("UpdateThreadStatusText", 0.2f, 1f);

      Clear();
    }
    private void Clear()
    {
      findBtn.interactable = false;
      updatedPathColoring = false;
      BestPath = new List<VertexController>();
      ClearThreads();
      ClearVertices();
      ClearEdges();
      ClearMap();

      UpdateThreadStatusText();
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
        foreach (var t in edges)
        {
          if (t.Second.DistanceFromStart > t.First.DistanceFromStart + t.Weight)
          {
            t.Second.DistanceFromStart = t.First.DistanceFromStart + t.Weight;
          }
          if (t.First.DistanceFromStart > t.Second.DistanceFromStart + t.Weight)
          {
            t.First.DistanceFromStart = t.Second.DistanceFromStart + t.Weight;
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
      lock (stateLock)
      {
        if (!Paralleling || stateTokens.Count == 0 || stateTokens.All(token => token.IsCancelled))
        {
          return;
        }
      }
      if (BestInterest == MaxInterest)
      {
        
        ClearThreads();
      }
      if (!updatedPathColoring) PrintBestPath(new List<VertexController>(BestPath));
    }
    /// <summary>
    /// calculates measure - current path
    /// </summary>
    private void CalculateHeuristicMeasure()
    {
      
    }

    /// <summary>
    /// алгоритм в два шага DepthSearch и BackPath вызываемый из него
    /// </summary>
    private void FindPath()
    {
      var currentVertex = vertices[0];
      // запустить рекурсионный поиск из каждой вершины смежной со стартовой
      // .Where(vert => vert.CurrentState == VertexController.VertexState.Unvisited))
      foreach (var adjV in currentVertex.GetAdjacentVertices()) 
      {
        adjV.CurrentState = VertexController.VertexState.Visited;
        if (!Paralleling) DepthSearch(adjV, new List<VertexController> { currentVertex, adjV });
        else
        {
          StartThreadFromPool(ParallelDepthSearch, adjV, new List<VertexController> { currentVertex, adjV });
        }
      }
    }

    private bool CycleExist(VertexController v, List<VertexController> path)
    {
      return path[path.Count - 1] == path[path.Count - 3] && v == path[path.Count - 2];
    }

    private void StartThreadFromPool(Action<VertexController, List<VertexController>, ThreadStateToken> action, VertexController startV, List<VertexController> startPath)
    {
      ThreadStateToken token = new ThreadStateToken();
      lock (stateLock)
      {
        stateTokens.Add(token);
      }
      ThreadPool.QueueUserWorkItem(s => action(startV, startPath, (ThreadStateToken)s), token);
    }

    /// <summary>
    /// вызывается на первом шаге когда мы двигаемся до тех пор пока не настанет время идти назад (BackPath)
    /// </summary>
    /// <param name="currV"></param>
    /// <param name="path"></param>
    /// <param name="token"></param>
    private void ParallelDepthSearch(VertexController currV, List<VertexController> path, ThreadStateToken token)
    {
      //если мы посетили все вершины, то дальше нет смысла искать и нужно вернуться в конечную точку
      if (vertices.All(path.Contains))
      {
        ParallelBackPath(currV, new List<VertexController>(path), token);
        return;
      }
      foreach (var nextV in currV.GetAdjacentVertices())
      {
        if (token.IsCancelled)
        {
          lock (stateLock)
          {
            stateTokens.Remove(token);
          }
          return;
        }

        if (path.Count > 2)
        {
          //Если мы уже были в вершине еще раз туда идти не надо
          if (CycleExist(nextV, path))
          {
            StartThreadFromPool(ParallelBackPath, currV, new List<VertexController>(path));
            continue;
          }
        }
        if (nextV.DistanceFromStart + CountTime(path) > TimeRestriction) // если пора возвращаться - возвращаемся
        {
          ParallelBackPath(currV, new List<VertexController>(path), token);
          return;
        }
        StartThreadFromPool(ParallelDepthSearch, nextV, new List<VertexController>(path) {nextV});
      }
      lock (stateLock)
      {
        stateTokens.Remove(token);
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
    private void ParallelBackPath(VertexController currentVertex, List<VertexController> path, ThreadStateToken token)
    {
      //так как мы уже должны возвращаться мы не будем дальше искать если вернулись в начало - 
      //если бы была возможность набрать больше интереса 
      //, то такой путь найдется в другом случае - с другой последовательностью вершин
      if (currentVertex == vertices[0])
      {
        iterations++;
        Debug.Log("Found path with " + path.Count + " vertices and " + CountInterest(path) + " interest");
        if (CountInterest(path) > BestInterest || (CountInterest(path) == BestInterest && CountTime(path) < CountTime(BestPath)))
        {
          Debug.Log("Replacing best path with current one");
          BestPath = new List<VertexController>(path);
          BestInterest = CountInterest(BestPath);
          updatedPathColoring = false;
          
        }
        lock (stateLock)
        {
          stateTokens.Remove(token);
        }
        return;
      }
      //Если есть цикл то нам такое не надо
      //по каждой смежной вершине в которой (расстояние из нее + время ребра до нее + текущее время пути  <= ограничения
      foreach (var nextV in currentVertex.GetAdjacentVertices().Where(nextV => !CycleExist(nextV, path)))
      {
        if (token.IsCancelled)
        {
          lock (stateLock)
          {
            stateTokens.Remove(token);
          }
          return;
        }

        if (nextV.DistanceFromStart + CountTime(path) <= TimeRestriction)
        {
          StartThreadFromPool(ParallelBackPath, nextV, new List<VertexController>(path) {nextV});
        }
      }
      lock (stateLock)
      {
        stateTokens.Remove(token);
      }
    }

    #region cosequentially realisation
    private void DepthSearch(VertexController currV, List<VertexController> path)
    {
      if (vertices.All(path.Contains))
      {
        BackPath(currV, path);
        return;
      }
      foreach (var nextV in currV.GetAdjacentVertices())
      {
        if (path.Count > 2)
        {
          //Если мы уже были в вершине еще раз туда идти не надо
          if (CycleExist(nextV, path))
          {
            BackPath(currV, new List<VertexController>(path));
            continue;
          }
        }
        path.Add(nextV);
        if (nextV.DistanceFromStart + CountTime(path) > TimeRestriction) // если пора возвращаться - возвращаемся
        {
          path.RemoveAt(path.Count - 1);
          BackPath(currV, new List<VertexController>(path));
          return;
        }
        DepthSearch(nextV, new List<VertexController>(path));
        path.RemoveAt(path.Count - 1);
      }
    }
    private void BackPath(VertexController currentVertex, List<VertexController> path)
    {
      //так как мы уже должны возвращаться мы не будем дальше искать если вернулись в начало - 
      //если бы была возможность набрать больше интереса 
      //, то такой путь найдется в другом случае - с другой последовательностью вершин
      if (currentVertex == vertices[0])
      {
        iterations++;
        Debug.Log("Found path with " + path.Count + " vertices and " + CountInterest(path) + " interest");
        if (CountInterest(path) > BestInterest ||
            (CountInterest(path) == BestInterest && CountTime(path) < CountTime(BestPath)))
        {
          Debug.Log("Replacing best path with current one");
          BestPath = new List<VertexController>(path);
          BestInterest = CountInterest(BestPath);
          PrintBestPath(new List<VertexController>(BestPath));
        }
        return;
      }
      //Если есть цикл то нам такое не надо
      //по каждой смежной вершине в которой (расстояние из нее + время ребра до нее + текущее время пути  <= ограничения
      foreach (var nextV in currentVertex.GetAdjacentVertices().Where(nextV => !CycleExist(nextV, path)))
      {
        path.Add(nextV);
        if (nextV.DistanceFromStart + CountTime(path) <= TimeRestriction)
        {
          BackPath(nextV, new List<VertexController>(path));
        }
        path.RemoveAt(path.Count - 1);
      }
    }
#endregion
    private int CountTime(List<VertexController> path)
    {
      var time = 0;
      if (path.Count < 2) return 0;
      for (var i = 0; i < path.Count - 1; i++)
      {
        var curEdge = edges.Find(e => e.IsConnecting(path[i], path[i + 1]));
        time += curEdge.Weight;
      }
      return time;
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

    private static int CountInterest(IEnumerable<VertexController> path)
    {
      return path == null ? 0 : path.Distinct().Sum(v => v.Interest);
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
     pathTimeText.text = CountTime(BestPath).ToString();
    }

    private void UpdatePathInterest()
    {
      pathInterestText.text = CountInterest(BestPath).ToString();
    }

    private void UpdateThreadStatusText()
    {
      lock (stateLock)
      {
        threadsStatusText.text = stateTokens.Count.ToString();
      }
    }

    private void OnApplicationQuit()
    {
      ClearThreads();
      Debug.Log("Found " + iterations + " possible routes");
    }
    private void ClearThreads()
    {
      lock (stateLock)
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
