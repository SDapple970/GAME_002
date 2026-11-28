using UnityEngine;
using UnityEngine.UI;

namespace Game.Combat.UI
{
    /// <summary>Reference-film geometry only; owns no input command or combat state.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CombatReferenceGraphic : MaskableGraphic
    {
        public enum Geometry { Sector, SlantedPlate, Disc, Capsule }
        [SerializeField] private Geometry geometry;
        [SerializeField] private float startAngle = 145f;
        [SerializeField] private float endAngle = 168f;
        [SerializeField] private float innerRatio = 0.24f;
        [SerializeField] private float slant = 24f;
        [SerializeField] private Color endColor = Color.white;

        public void Configure(Geometry shape, Color tint, float start = 145f, float end = 168f,
            float inner = 0.24f, float skew = 24f, Color? gradient = null)
        {
            geometry = shape;
            color = tint;
            startAngle = start;
            endAngle = end;
            innerRatio = inner;
            slant = skew;
            endColor = gradient ?? tint;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = GetPixelAdjustedRect();
            if (geometry == Geometry.Capsule)
            {
                float capsuleRadius = Mathf.Min(rect.height, rect.width) * .5f;
                mesh.AddVert(rect.center, color, Vector2.zero);
                const int segments = 48;
                for (int i = 0; i <= segments; i++)
                {
                    float angle = (i * 360f / segments - 90f) * Mathf.Deg2Rad;
                    float centerX = i <= segments / 2 ? rect.xMax - capsuleRadius : rect.xMin + capsuleRadius;
                    mesh.AddVert(new Vector2(centerX + Mathf.Cos(angle) * capsuleRadius, rect.center.y + Mathf.Sin(angle) * capsuleRadius), color, Vector2.zero);
                    if (i > 0) mesh.AddTriangle(0, i, i + 1);
                }
                mesh.AddTriangle(0, segments + 1, 1);
                return;
            }
            if (geometry == Geometry.SlantedPlate)
            {
                Quad(mesh, new Vector2(rect.xMin, rect.yMin), new Vector2(rect.xMin + slant, rect.yMax),
                    new Vector2(rect.xMax, rect.yMax), new Vector2(rect.xMax - slant, rect.yMin), color, endColor);
                return;
            }
            Vector2 center = rect.center;
            float radius = Mathf.Min(rect.width, rect.height) * 0.5f;
            float from = geometry == Geometry.Disc ? 0f : startAngle;
            float to = geometry == Geometry.Disc ? 360f : endAngle;
            float inner = geometry == Geometry.Disc ? 0f : radius * innerRatio;
            int steps = Mathf.Max(8, Mathf.CeilToInt((to - from) / 3f));
            for (int i = 0; i < steps; i++)
            {
                float a = Mathf.Lerp(from, to, (float)i / steps) * Mathf.Deg2Rad;
                float b = Mathf.Lerp(from, to, (float)(i + 1) / steps) * Mathf.Deg2Rad;
                Vector2 va = new(Mathf.Cos(a), Mathf.Sin(a));
                Vector2 vb = new(Mathf.Cos(b), Mathf.Sin(b));
                Quad(mesh, center + va * inner, center + va * radius,
                    center + vb * radius, center + vb * inner, color, color);
            }
        }

        public override bool Raycast(Vector2 screenPoint, Camera eventCamera)
        {
            if (!base.Raycast(screenPoint, eventCamera)) return false;
            if (geometry != Geometry.Sector) return true;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPoint, eventCamera, out Vector2 local))
                return false;
            Vector2 delta = local - rectTransform.rect.center;
            float radius = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * 0.5f;
            float angle = Mathf.Repeat(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, 360f);
            return delta.magnitude >= radius * innerRatio && delta.magnitude <= radius && angle >= startAngle && angle <= endAngle;
        }

        private static void Quad(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color left, Color right)
        {
            int index = mesh.currentVertCount;
            mesh.AddVert(a, left, Vector2.zero);
            mesh.AddVert(b, left, Vector2.zero);
            mesh.AddVert(c, right, Vector2.zero);
            mesh.AddVert(d, right, Vector2.zero);
            mesh.AddTriangle(index, index + 1, index + 2);
            mesh.AddTriangle(index, index + 2, index + 3);
        }
    }
}
