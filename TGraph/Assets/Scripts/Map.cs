using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Assets.Scripts
{
  class Map
  {
    public int MaxInterest;

    private readonly float spawnRate = 1;
    private const int Size = 10;

    private readonly GameObject vertexPrefab;
    private readonly GameObject edgePrefab;
    private readonly List<EdgeController> edges;
    private readonly List<VertexController> vertices;

    public VertexController StartVertex { get { return vertices[0]; } }
    public Map(float SpawnRate)
    {
      spawnRate = SpawnRate;
      MaxInterest = -1;

      vertices = new List<VertexController>();
      edges = new List<EdgeController>();
      edgePrefab = Resources.Load<GameObject>("Prefabs/Edge");
      vertexPrefab = Resources.Load<GameObject>("Prefabs/Vertex");
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

    public bool IsEmpty()
    {
      return !vertices.Any();
    }

    public void RandomizeAndPrepare()
    {
      RandomizeVertices();
      RandomizeEdges();
      FordBellman();
      SpreadDepth(vertices[0]);
      MaxInterest = vertices.Sum(v => v.Interest);
      SpreadMeasure();
    }

    /// <summary>
    /// filling map with random vertices with random interests
    /// </summary>
    private void RandomizeVertices()
    {
      CreateVertex(0, 0, "Vertex Start", "S");
      StartVertex.Interest = 0;

      for (var i = 1; i < Size * 2; i += 2)
      {
        for (var j = 1; j < Size * 2; j += 2)
        {
          if (Random.value > spawnRate)
          {
            CreateVertex(i, j, "Vertex " + vertices.Count);
          }
        }
      }
    }

    private void CreateVertex(int i, int j, string name, string childName = null)
    {
      GameObject vertexGameObject = Object.Instantiate(vertexPrefab, GameObject.Find("Vertices").transform);
      VertexController vertexController = vertexGameObject.GetComponent<VertexController>();
      vertexController.Init(i, j, name, childName);
      vertices.Add(vertexController);
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
      edges.Add(edge);
    }

    private void RandomizeEdges()
    {
      List<VertexController> unlinkedV = new List<VertexController>(vertices);
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

    private void SpreadDepth(VertexController currentVertex)
    {
      foreach (var v in vertices)
      {
        v.Depth = vertices.Count;
      }
      vertices[0].Depth = 0;
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
    /// resets vertices best interest and time
    /// </summary>
    public void ResetVertices()
    {
      foreach (var vertex in vertices)
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

    public void ClearColoring()
    {
      foreach (var edge in edges)
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
  }
}
