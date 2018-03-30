using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.enums;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace Assets.Scripts
{
  internal class Map 
  {
    public int MaxInterest;
    private readonly GameObject vertexPrefab;
    private readonly GameObject edgePrefab;
    public const int Size = 10;

    private _Map map;

    public float SpawnRate
    {
      get { return map.SpawnRate; }
      set { map.SpawnRate = value; }
    }

    public List<VertexController> Vertices
    {
      get { return map.Vertices; }
      set { map.Vertices = value; }
    }
    public List<EdgeController> Edges
    {
      get { return map.Edges; }
      set { map.Edges = value; }
    }

    public VertexController StartVertex => Vertices[0];

    public Map(float spawnRate)
    {
      SpawnRate = spawnRate;
      MaxInterest = -1;

      Vertices = new List<VertexController>();
      Edges = new List<EdgeController>();
      edgePrefab = Resources.Load<GameObject>("Prefabs/Edge");
      vertexPrefab = Resources.Load<GameObject>("Prefabs/Vertex");
    }

    /// <summary>
    /// finds distances from start vertex to others
    /// </summary>
    private void FordBellman()
    {
      foreach (var t in Vertices)
      {
        t.DistanceFromStart = 1000000;
      }
      Vertices[0].DistanceFromStart = 0;
      for (var i = 0; i < Vertices.Count; i++)
      {
        foreach (var edge in Edges)
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

    public bool IsEmpty()
    {
      return !Vertices.Any();
    }

    public void Randomize()
    {
      RandomizeVertices();
      RandomizeEdges();
      Prepare();
    }

    public void InitFromJson(MapWrapper mapWrapper)
    {
      SpawnRate = mapWrapper.SpawnRate;
      Clear();
      InitVertices(mapWrapper.Vertices);
      InitEdges(mapWrapper.Edges);
      Prepare();
    }

    public void Prepare()
    {
      FordBellman();
      foreach (var v in Vertices)
      {
        v.Depth = Vertices.Count;
      }
      SpreadDepth(Vertices[0]);
      MaxInterest = Vertices.Sum(v => v.Interest);
      SpreadMeasure();
    }

    /// <summary>
    /// filling map with random vertices with random interests
    /// </summary>
    private void RandomizeVertices()
    {
      CreateVertex(new Vector2(0, 0), 0, "Start", "S");

      //* 2 cause map size is too small if * 1
      for (var i = 1; i < Size * 2; i += 2)
      {
        for (var j = 1; j < Size * 2; j += 2)
        {
          if (Random.value > SpawnRate)
          {
            CreateVertex(new Vector2(i, j), RandomizeInterest(), Vertices.Count.ToString());
          }
        }
      }
    }

    private void RandomizeEdges()
    {
      List<VertexController> unlinkedV = new List<VertexController>(Vertices);
      unlinkedV.Remove(StartVertex);

      List<VertexController> path = new List<VertexController> { StartVertex };

      while (unlinkedV.Count > 0)
      {
        VertexController nextV = unlinkedV[Random.Range(0, unlinkedV.Count)];
        CreateEdge(path[Random.Range(0, path.Count - 1)], nextV, Random.Range(1, 7));
        path.Add(nextV);
        unlinkedV.Remove(nextV);
      }
    }

    private void CreateVertex(Vector2 initPos, int interest, string name, string childName = null)
    {
      GameObject vertexGameObject = Object.Instantiate(vertexPrefab, GameObject.Find("Vertices").transform);
      VertexController vertexController = vertexGameObject.GetComponent<VertexController>();
      vertexController.Init(initPos, interest, name, childName);
      Vertices.Add(vertexController);
    }

    public int RandomizeInterest()
    {
      return Random.Range(1, 10);
    }
    /// <summary>
    /// creates edge between two vertices by their position
    /// </summary>
    private void CreateEdge(VertexController from, VertexController to, int w)
    {
      GameObject edgeGameObject = Object.Instantiate(edgePrefab, GameObject.Find("Edges").transform);
      EdgeController edge = edgeGameObject.GetComponent<EdgeController>();
      edge.Init(from, to);
      edge.Weight = w;
      Edges.Add(edge);
    }
    
    private void SpreadDepth(VertexController currentVertex)
    {
      Vertices[0].Depth = 0;
      currentVertex.CurrentState = VertexState.Visited;
      foreach (var v in currentVertex.GetAdjacentVertices())
      {
        if (v.Depth > currentVertex.Depth + 1)
        {
          v.Depth = currentVertex.Depth + 1;
        }
        if (v.CurrentState == VertexState.Unvisited)
        {
          SpreadDepth(v);
        }
      }
    }

    /// <summary>
    /// resets vertices best interest and time
    /// </summary>
    public void ResetVertices()
    {
      foreach (var vertex in Vertices)
      {
        vertex.CurrentBestInterest = 0;
        vertex.CurrentBestTime = 0;
      }
    }

    public void Clear()
    {
      MaxInterest = -1;
      ClearVertices();
      ClearEdges();
    }

    private void ClearVertices()
    {
      foreach (VertexController t in Vertices)
      {
        Object.Destroy(t.gameObject);
      }
      Vertices.Clear();
    }

    private void ClearEdges()
    {
      foreach (EdgeController t in Edges)
      {
        Object.Destroy(t.gameObject);
      }
      Edges.Clear();
    }

    public void ClearColoring()
    {
      foreach (var edge in Edges)
      {
        edge.CurrentEdgeState = EdgeState.Unused;
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
      foreach (var vertex in Vertices)
      {
        vertex.CurrentState = VertexState.Unvisited;
      }
      int maxDepth = Vertices.Max(v => v.Depth);

      for (var i = 0; i <= maxDepth; i++) //вверх по глубине
      {
        foreach (var currentVertex in Vertices.Where(v => v.Depth == i)) // по каждой вершине с текущей глубиной распространяем наилучшую меру
        {
          foreach (var equalVertex in currentVertex.GetAdjacentVertices()// проверяем не будет ли лучшей меры от смежных вершин не глубже текущей 
            .Where(vertex => vertex.Depth == currentVertex.Depth))
          {
            float currentMeasure;
            var edgeBetweenVertices = currentVertex.GetConnectingEdge(equalVertex);
            if (equalVertex.BestMeasure < currentVertex.BestMeasure)
            {
              currentMeasure = HeuristicMeasure(currentVertex.Interest, edgeBetweenVertices.Weight) + equalVertex.BestMeasure;
              if (currentMeasure > currentVertex.BestMeasure)
              {
                currentVertex.BestMeasure = currentMeasure;
              }
            }
            else
            {
              currentMeasure = HeuristicMeasure(equalVertex.Interest, edgeBetweenVertices.Weight) + currentVertex.BestMeasure;
              if (currentMeasure > equalVertex.BestMeasure)
              {
                equalVertex.BestMeasure = currentMeasure;
              }
            }
          }
          foreach (var deeperVertex in currentVertex.GetAdjacentVertices()// проталкиваем меру из текущей вершины вниз по глубине
            .Where(vertex => vertex.Depth > currentVertex.Depth))
          {
            var edgeToDeeperVertex = currentVertex.GetConnectingEdge(deeperVertex);
            var measureFromCurrentVertex = HeuristicMeasure(deeperVertex.Interest, edgeToDeeperVertex.Weight) + currentVertex.BestMeasure;
            if (measureFromCurrentVertex > deeperVertex.BestMeasure)
            {
              deeperVertex.BestMeasure = measureFromCurrentVertex;
            }
          }
        }
      }
    }

    private void InitEdges(IEnumerable<_Edge> mapWrapperEdges)
    {
      foreach (var edgeStruct in mapWrapperEdges)
      {
        var first = Vertices.Find(v => v.Name == edgeStruct.FirstVertexName);
        var second = Vertices.Find(v => v.Name == edgeStruct.SecondVertexName);
        CreateEdge(first, second, edgeStruct.Weight);
      }
    }

    private void InitVertices(IEnumerable<_Vertex> mapWrapperVertices)
    {
      foreach (_Vertex vertexStruct in mapWrapperVertices)
      {
        CreateVertex(vertexStruct);
      }
    }

    private void CreateVertex(_Vertex vertexStruct)
    {
      CreateVertex(new Vector2(vertexStruct.x, vertexStruct.y), vertexStruct.Interest, vertexStruct.Name, vertexStruct.ChildText);
    }

    public void HideUnreachableVertices(int restriction)
    {
      if (Vertices.All(v => v.DistanceFromStart == 0))//map should be prepared before hiding inaccessible vertices
      {
        return;
      }
      foreach (VertexController vertex in Vertices)
      {
        if (vertex.DistanceFromStart * 2 > restriction)
        {
          vertex.GetComponent<SpriteRenderer>().color = Color.black;
        }
        else
        {
          vertex.GetComponent<SpriteRenderer>().color = Color.white;
        }
      }
    }
  }
}
