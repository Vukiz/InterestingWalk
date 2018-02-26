using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Assets.Scripts.enums;

namespace Assets.Scripts
{
  /// <inheritdoc />
  /// <summary>
  /// represents single examplar of path
  /// </summary>
  public class GraphPath : IEnumerable
  {
    private int interest = 0;
    private int time = 0;
    private readonly List<VertexController> vertices;
    private readonly List<EdgeController> edges;
    private readonly List<int> edgesCount;

    public int Time
    {
      get { return time; }
    }
    public int Interest
    {
      get { return interest; }
    }
    public GraphPath()
    {
      vertices = new List<VertexController>();
      edges = new List<EdgeController>();
      edgesCount = new List<int>();
    }

    public GraphPath(GraphPath path)
    {
      vertices = new List<VertexController>(path.vertices);
      edges = new List<EdgeController>(path.edges);
      edgesCount = new List<int>(path.edgesCount);
      interest = path.interest;
      time = path.time;
    }

    public GraphPath Add(VertexController v)
    {
      if (vertices.Count > 0)
      {
        var newEdge = vertices.Last().GetConnectingEdge(v);
        if (!edges.Contains(newEdge))
        {
          edges.Add(newEdge);
          edgesCount.Add(1);
        }
        else
        {
          if (++edgesCount[edges.IndexOf(newEdge)] > 2)
          {
            Debug.Assert(false);
          }
        }
        time += newEdge.Weight;
      }
      if (!vertices.Contains(v))
      {
        interest += v.Interest;
      }
      vertices.Add(v);
      return this;
    }

    public bool Contains(VertexController v)
    {
      return vertices.Contains(v);
    }
    public bool Contains(List<VertexController> vs)
    {
      return vertices.All(vs.Contains);
    }
    public int CountInterest()
    {
      return vertices.Distinct().Sum(v => v.Interest);
    }

    public void ColorGraph()
    {
      foreach (var edge in edges)
      {
        edge.CurrentEdgeState = EdgeState.Used;
      }
    }
    public IEnumerator GetEnumerator()
    {
      return vertices.GetEnumerator();
    }

    public object GetAllPathVertices()
    { 
      return vertices.Aggregate("", (current, v) => current + (" " + v.name));
    }

    public bool CheckVForCycle(VertexController nextV)
    {
      if (vertices.Count < 3)
      {
        return false;
      }
      var newEdge = vertices.Last().GetConnectingEdge(nextV);
      var newEdgeIndex = edges.IndexOf(newEdge);
      if (newEdgeIndex < 0)
      {
        return false;
      }
      return edgesCount[newEdgeIndex] > 1;
    }

    public int VerticesCount()
    {
      return vertices.Count;
    }
  }
}
