using System;

namespace Assets.Scripts
{
  [Serializable]
  public struct _Edge
  {
    public int Weight;
    public string FirstVertexName;
    public string SecondVertexName;
  }
}