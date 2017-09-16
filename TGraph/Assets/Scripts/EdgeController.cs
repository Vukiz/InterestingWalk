using UnityEngine;

namespace Assets.Scripts
{
    public class EdgeController : MonoBehaviour
    {
        public int Weight
        {
            get { return weight; }
            set
            {
                weight = value;
                GetComponentInChildren<TextMesh>().text = value.ToString();
                GetComponentInChildren<TextMesh>().transform.localScale =
                    new Vector2(GetComponentInChildren<TextMesh>().transform.localScale.x/transform.localScale.x,2f);
            }
        }

        private int weight;
        public VertexController First;
        public VertexController Second;

        public EdgeState CurrentEdgeState
        {
            get { return currentEdgeState; }
            set
            {
                currentEdgeState = value;
                GetComponent<SpriteRenderer>().color = value == EdgeState.Used ? Color.red : Color.cyan;
            }
        }
        private EdgeState currentEdgeState;
        public enum EdgeState
        {
            Used,
            Unused
        }
        public void Init(VertexController f, VertexController s)
        {
            First = f;
            Second = s;
            CurrentEdgeState = EdgeState.Unused;
            var fPos = f.transform.position;
            var sPos = s.transform.position;
            f.ConjoinedEdges.Add(this);
            s.ConjoinedEdges.Add(this);
            transform.position = new Vector3((sPos.x - fPos.x)/2 + fPos.x, (sPos.y - fPos.y) / 2 + fPos.y, 1);//помещаем под вершины
            transform.localScale = new Vector2(Vector2.Distance(fPos, sPos)/GetComponent<SpriteRenderer>().bounds.size.x, 0.2f);
            
            transform.rotation = Quaternion.Euler(0, 0, Mathf.Rad2Deg * Mathf.Atan2(sPos.y - fPos.y, sPos.x - fPos.x));
        }

        public bool IsConnecting(VertexController a, VertexController b)
        {
            return (First == a && Second == b) || (First == b && Second == a);
        }
    }
}
