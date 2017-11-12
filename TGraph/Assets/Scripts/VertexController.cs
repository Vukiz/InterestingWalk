using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Assets.Scripts
{
  public class VertexController : MonoBehaviour
  {
    public int Interest;
    public List<EdgeController> ConjoinedEdges;
    public VertexState CurrentState;
    public int DistanceFromStart = 0;
    public int Depth;

    public enum VertexState
    {
      Unvisited,
      Visited
    }

    public void RandomizeInterest()
    {
      Interest = Random.Range(1, 10);
      GetComponentInChildren<TextMesh>().text = Interest.ToString();
    }

    public EdgeController GetConnectingEdge(VertexController connectedVertex)
    {
      return MapСontroller.Edges.Find(e => e.IsConnecting(this, connectedVertex));
    }

    public List<VertexController> GetAdjacentVertices()
    {
      return ConjoinedEdges.Select(edge => (edge.First != this) ? edge.First : edge.Second).ToList();
    }
  }
}
