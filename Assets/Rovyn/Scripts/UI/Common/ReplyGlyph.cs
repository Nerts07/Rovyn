using UnityEngine;

namespace Rovyn.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class ReplyGlyph : UnityEngine.UI.MaskableGraphic
    {
        static readonly Vector2[] Stroke =
        {
            new(15, 5), new(15, 8), new(14, 10), new(12, 12), new(5, 12),
        };

        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
        {
            mesh.Clear();
            var rect = GetPixelAdjustedRect();
            float scale = Mathf.Min(rect.width, rect.height) / 20;
            var origin = rect.center - new Vector2(10, 10) * scale;
            for (int i = 1; i < Stroke.Length; i++)
                AddLine(mesh, origin + Stroke[i - 1] * scale, origin + Stroke[i] * scale, scale);

            Vector2 tip = origin + Stroke[Stroke.Length - 1] * scale;
            AddLine(mesh, tip, origin + new Vector2(9, 16) * scale, scale);
            AddLine(mesh, tip, origin + new Vector2(9, 8) * scale, scale);
        }

        void AddLine(UnityEngine.UI.VertexHelper mesh, Vector2 start, Vector2 end, float halfWidth)
        {
            var edge = (end - start).normalized;
            var normal = new Vector2(-edge.y, edge.x) * halfWidth;
            start -= edge * (halfWidth * 0.25f);
            end += edge * (halfWidth * 0.25f);
            int first = mesh.currentVertCount;
            mesh.AddVert(start - normal, color, Vector4.zero);
            mesh.AddVert(start + normal, color, Vector4.zero);
            mesh.AddVert(end + normal, color, Vector4.zero);
            mesh.AddVert(end - normal, color, Vector4.zero);
            mesh.AddTriangle(first, first + 1, first + 2);
            mesh.AddTriangle(first + 2, first + 3, first);
        }
    }
}
