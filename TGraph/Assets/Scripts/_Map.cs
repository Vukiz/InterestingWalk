using System.Collections.Generic;

namespace Assets.Scripts
{
  struct _Map
  {
    public float SpawnRate;
    public List<EdgeController> Edges;
    public List<VertexController> Vertices;
  }
}