using System;

namespace Assets.Scripts
{
  [Serializable]
  public class MapWrapper
  {
    public float SpawnRate;
    public _Vertex[] Vertices;
    public _Edge[] Edges;
  }
}