using System.Collections.Generic;
using Assets.Scripts.enums;
using UnityEngine;

namespace Assets.Scripts
{
  public class VertexController : MonoBehaviour
  {
    public List<EdgeController> ConjoinedEdges;
    public VertexState CurrentState;
    public _Vertex Vertex;
    public int Interest
    {
      get { return Vertex.Interest; }
      set
      {
        Vertex.Interest = value; 
        SetChildText(value.ToString());
      }
    }

    public int DistanceFromStart;
    public int CurrentBestInterest;
    public int CurrentBestTime;
    public int Depth;

    public string Name
    {
      get { return Vertex.Name; }
      set
      {
        Vertex.Name = value;
        gameObject.name = value;
      }
    }

    private string ChildText
    {
      get { return Vertex.ChildText; }
      set
      {
        Vertex.ChildText = value;
      }
    }

    private Vector2 position
    {
      get
      {
        return new Vector2(Vertex.x,Vertex.y);
      }
      set
      {
        Vertex.x = value.x;
        Vertex.y = value.y;
        gameObject.transform.position = value;
      }
    }

    public float BestMeasure;
    public object Locker = new object();

    public void Init(_Vertex initialVertex)
    {
      Vertex = initialVertex;
      gameObject.transform.position = position;
      gameObject.name = Name;
      if (!string.IsNullOrEmpty(ChildText))
      {
        SetChildText(ChildText);
      }
    }

    public void Init(Vector2 initPosition, int interest, string initName, string childText = null)
    {
      position = initPosition;
      Name = initName;
      Interest = interest;
      if (!string.IsNullOrEmpty(childText))
      {
        SetChildText(childText);
      }
    }

    private void SetChildText(string text)
    {
      ChildText = text;
      GetComponentInChildren<TextMesh>().text = text;
    }
  }
}
