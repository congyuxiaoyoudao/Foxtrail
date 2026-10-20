using UnityEngine;
using UnityEngine.UI;

namespace Hiking.Journey
{
    /// <summary>A dashed pointer drawn within the tutorial Canvas.</summary>
    public sealed class TutorialArrowGraphic : MaskableGraphic
    {
        [SerializeField, Min(1f)] float width = 3f;
        [SerializeField, Min(1f)] float dashLength = 13f;
        [SerializeField, Min(1f)] float gapLength = 9f;
        Vector2 start;
        Vector2 end;

        public void SetEndpoints(Vector2 from, Vector2 to)
        {
            if ((start - from).sqrMagnitude < .25f && (end - to).sqrMagnitude < .25f) return;
            start = from;
            end = to;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Vector2 delta = end - start;
            float length = delta.magnitude;
            if (length < 10f) return;
            Vector2 direction = delta / length;
            Vector2 normal = new Vector2(-direction.y, direction.x);
            float shaftLength = Mathf.Max(0, length - 15f);
            float stride = dashLength + gapLength;
            for (float offset = 0; offset < shaftLength; offset += stride)
            {
                float stop = Mathf.Min(offset + dashLength, shaftLength);
                AddQuad(mesh, start + direction * offset, start + direction * stop,
                    normal * (width * .5f));
            }
            Vector2 tip = end;
            Vector2 baseCenter = end - direction * 17f;
            AddTriangle(mesh, tip, baseCenter + normal * 8f, baseCenter - normal * 8f);
        }

        void AddQuad(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 halfWidth)
        {
            int index = mesh.currentVertCount;
            AddVertex(mesh, a + halfWidth);
            AddVertex(mesh, b + halfWidth);
            AddVertex(mesh, b - halfWidth);
            AddVertex(mesh, a - halfWidth);
            mesh.AddTriangle(index, index + 1, index + 2);
            mesh.AddTriangle(index, index + 2, index + 3);
        }

        void AddTriangle(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c)
        {
            int index = mesh.currentVertCount;
            AddVertex(mesh, a);
            AddVertex(mesh, b);
            AddVertex(mesh, c);
            mesh.AddTriangle(index, index + 1, index + 2);
        }

        void AddVertex(VertexHelper mesh, Vector2 point) =>
            mesh.AddVert(point, color, Vector2.zero);
    }
}
