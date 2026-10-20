using UnityEngine;
using UnityEngine.UI;

namespace Hiking.Journey
{
    /// <summary>A dashed quadratic curve pointing at a tutorial target.</summary>
    public sealed class TutorialArrowGraphic : MaskableGraphic
    {
        [SerializeField, Min(1f)] float width = 6f;
        [SerializeField, Min(1f)] float dashLength = 16f;
        [SerializeField, Min(1f)] float gapLength = 10f;
        Vector2 start;
        Vector2 control;
        Vector2 end;

        public void SetCurve(Vector2 from, Vector2 bend, Vector2 to)
        {
            if ((start - from).sqrMagnitude < .25f &&
                (control - bend).sqrMagnitude < .25f &&
                (end - to).sqrMagnitude < .25f) return;
            start = from;
            control = bend;
            end = to;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if ((end - start).sqrMagnitude < 100f) return;
            float stride = dashLength + gapLength;
            const int segments = 40;
            float totalLength = 0f;
            Vector2 sample = start;
            for (int i = 1; i <= segments; i++)
            {
                Vector2 nextSample = Evaluate(i / (float)segments);
                totalLength += Vector2.Distance(sample, nextSample);
                sample = nextSample;
            }
            float shaftLength = Mathf.Max(0f, totalLength - 18f);
            float travelled = 0f;
            Vector2 previous = start;
            for (int i = 1; i <= segments; i++)
            {
                Vector2 next = Evaluate(i / (float)segments);
                Vector2 segment = next - previous;
                float segmentLength = segment.magnitude;
                if (segmentLength > .001f)
                {
                    Vector2 direction = segment / segmentLength;
                    Vector2 normal = new Vector2(-direction.y, direction.x) * (width * .5f);
                    float visibleLength = Mathf.Min(segmentLength, shaftLength - travelled);
                    for (float offset = 0f; offset < visibleLength;)
                    {
                        float phase = (travelled + offset) % stride;
                        bool inDash = phase < dashLength;
                        float portion = Mathf.Min(visibleLength - offset,
                            (inDash ? dashLength : stride) - phase);
                        if (inDash)
                            AddQuad(mesh, previous + direction * offset,
                                previous + direction * (offset + portion), normal);
                        offset += Mathf.Max(.001f, portion);
                    }
                }
                travelled += segmentLength;
                previous = next;
            }
            Vector2 tangent = end - control;
            if (tangent.sqrMagnitude < .001f) tangent = end - start;
            Vector2 tipDirection = tangent.normalized;
            Vector2 tipNormal = new Vector2(-tipDirection.y, tipDirection.x);
            Vector2 baseCenter = end - tipDirection * 19f;
            AddTriangle(mesh, end, baseCenter + tipNormal * 9f, baseCenter - tipNormal * 9f);
        }

        Vector2 Evaluate(float t)
        {
            float oneMinus = 1f - t;
            return oneMinus * oneMinus * start + 2f * oneMinus * t * control + t * t * end;
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
