using UnityEngine;

namespace Game.Adapter.In.UI
{
    /// <summary>Authoritative master polygon; shares the artwork's complete transform.</summary>
    public sealed class MapPolygonHitArea : MonoBehaviour, ICanvasRaycastFilter
    {
        [SerializeField] private Vector2[] polygon = System.Array.Empty<Vector2>();
        public Vector2[] Polygon => polygon;
        public void Configure(Vector2[] points) => polygon=points;
        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            if(!RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform,screenPoint,eventCamera,out var p)) return false;
            return Contains(new Vector2(p.x,-p.y));
        }
        public bool Contains(Vector2 point)
        {
            bool inside=false;
            for(int i=0,j=polygon.Length-1;i<polygon.Length;j=i++)
            {
                var a=polygon[i]; var b=polygon[j];
                if((a.y>point.y)!=(b.y>point.y) && point.x<(b.x-a.x)*(point.y-a.y)/(b.y-a.y)+a.x) inside=!inside;
            }
            return inside;
        }
    }
}
