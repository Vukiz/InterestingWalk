using System.Collections.Generic;
using System.Linq;

namespace Assets.Scripts
{
  public static class VertexExtension
  {
    public static EdgeController GetConnectingEdge(this VertexController currentVertex,
      VertexController connectedVertex)
    {
      return currentVertex.ConjoinedEdges.Find(e => e.IsConnecting(currentVertex, connectedVertex));
    }

    public static List<VertexController> GetAdjacentVertices(this VertexController currentVertex)
    {
      return currentVertex.ConjoinedEdges.Select(edge => edge.First != currentVertex ? edge.First : edge.Second).ToList();
    }
  }
}