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
    public bool paralleling = true;
    public float SpawnRate;
    public int TimeRestriction;
    public int BestInterest = 0;
    public int MaxInterest = 0;

    private int iterations = 0;
    private bool updated = false;
    private const int Size = 10;

    private GameObject vertexPrefab;
    private GameObject edgePrefab;

    private List<GameObject> map;
    private List<EdgeController> edges;
    private List<VertexController> vertices;
    private List<Thread> threads;

    private Button FindBtn;
    private Button RandomizeBtn;

    private readonly Button.ButtonClickedEvent findButtonClickedEvent = new Button.ButtonClickedEvent();
    private readonly Button.ButtonClickedEvent randomizeButtonClickedEvent = new Button.ButtonClickedEvent();

    // Use this for initialization
    private void Start()
    {
      Init();
      FindBtn = GameObject.Find("FindBtn").GetComponent<Button>();
      FindBtn.interactable = false; 
      RandomizeBtn = GameObject.Find("RandomizeBtn").GetComponent<Button>();
      findButtonClickedEvent.AddListener(OnFindBtnClick);
      randomizeButtonClickedEvent.AddListener(OnRandomizeButtonClick);
      FindBtn.onClick = findButtonClickedEvent;
      RandomizeBtn.onClick = randomizeButtonClickedEvent;
    }

    private void PrintBestPath(List<VertexController> path)
    {
      updated = true;
      ColorBestPath(path);
      var s = path.Aggregate("", (current, v) => current + (" " + v.name));
      Debug.Log("Best route has " + CountInterest(path) + " interest for " + CountTime(path) + " time and consists of ");
      Debug.Log(s);
    }
    private void ColorBestPath(List<VertexController> path)
    {
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
      ClearMap();
      RandomizeVertices();
      RandomizeEdges();
    }

    public void OnRandomizeButtonClick()
    {
      RandomizeGraph();
      FindBtn.interactable = true;
    }
    public void OnFindBtnClick()
    {
      FindBtn.interactable = false;
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
    private void Init()
    {
      ClearMap();
      edgePrefab = Resources.Load<GameObject>("Prefabs/Edge");
      vertexPrefab = Resources.Load<GameObject>("Prefabs/Vertex");
      InvokeRepeating("UpdatePathTime", 0.2f, 1f);
      InvokeRepeating("UpdatePathInterest", 0.2f, 1f);
    }

    private void ClearThreads()
    {
      if (threads != null)
      {
        foreach (var thread in threads)
        {
          thread.Abort();
        }
      }
      threads = new List<Thread>();
      Debug.Log("Cleared all threads");
      StartCoroutine(ClearConsole());
    }

    private IEnumerator ClearConsole()
    {
      // wait until console visible
      while (!Debug.developerConsoleVisible)
      {
        yield return null;
      }
      yield return null; // this is required to wait for an additional frame, without this clearing doesn't work (at least for me)
      Debug.ClearDeveloperConsole();
    }
    private void ClearMap()
    {
      ClearThreads();
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
      if (map == null)
      {
        map = new List<GameObject>();
      }
      else
      {
        map.Clear();
      }
      BestPath = new List<VertexController>();
      Debug.Log("Cleared map");
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
      if (!paralleling || threads.Count <= 0) return;
      if (BestInterest == MaxInterest)
      {
        ClearThreads();
      }

      if (!updated) PrintBestPath(new List<VertexController>(BestPath));

    }

    /// <summary>
    /// алгоритм в два шага DepthSearch и BackPath вызываемый из него
    /// </summary>
    private void FindPath()
    {
      var currentVertex = vertices[0];
      foreach (var adjV in currentVertex.GetAdjacentVertices()) // запустить рекурсионный поиск из каждой вершины смежной со стартовой
                                                                // .Where(vert => vert.CurrentState == VertexController.VertexState.Unvisited))
      {
        adjV.CurrentState = VertexController.VertexState.Visited;
        if (!paralleling) DepthSearch(adjV, new List<VertexController> { currentVertex, adjV });
        else
        {
          var deepThread = new Thread(() => DepthSearch(adjV, new List<VertexController> { currentVertex, adjV }));
          deepThread.Start();
          threads.Add(deepThread);
        }
      }
    }

    private bool CycleExist(VertexController v, List<VertexController> path)
    {
      return path[path.Count - 1] == path[path.Count - 3] && v == path[path.Count - 2];
    }
    /// <summary>
    /// вызывается на первом шаге когда мы двигаемся до тех пор пока не настанет время идти назад (BackPath)
    /// </summary>
    /// <param name="currV"></param>
    /// <param name="path"></param>
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
          /*if (path[path.Count - 1] == path[path.Count - 3] && path.Contains(nextV))
          {
              continue;
          }*/
        }
        path.Add(nextV);
        if (nextV.DistanceFromStart + CountTime(path) > TimeRestriction)// если пора возвращаться - возвращаемся
        {
          path.RemoveAt(path.Count - 1);
          BackPath(currV, new List<VertexController>(path));
          return;
        }
        DepthSearch(nextV, new List<VertexController>(path));
        path.RemoveAt(path.Count - 1);
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
    private void BackPath(VertexController currentVertex, List<VertexController> path)
    {
      //recurrency break condition
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
          updated = false;
          if (!paralleling)
          {
            PrintBestPath(new List<VertexController>(BestPath));
          }
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
      GameObject.Find("PathTime").GetComponent<Text>().text = CountTime(BestPath).ToString();
    }

    private void UpdatePathInterest()
    {
      GameObject.Find("PathInterest").GetComponent<Text>().text = CountInterest(BestPath).ToString();
    }

    private void OnApplicationQuit()
    {
      foreach (var thr in threads)
      {
        thr.Abort();
      }
      threads = new List<Thread>();
      Debug.Log("Found " + iterations + " possible routes");
    }
  }
}
