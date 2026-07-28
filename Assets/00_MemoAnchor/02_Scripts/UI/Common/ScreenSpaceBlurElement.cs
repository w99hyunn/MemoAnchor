using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace MemoAnchor.UI
{
    [UxmlElement]
    public partial class ScreenSpaceBlurElement : VisualElement
    {
        [UxmlAttribute("blur-opacity")]
        public float BlurOpacity { get; set; } = 0.94f;

        [UxmlAttribute("blur-brightness")]
        public float BlurBrightness { get; set; } = 0.62f;

        [UxmlAttribute("corner-segments")]
        public int CornerSegments { get; set; } = 12;

        private readonly BlurOverlayElement blurOverlay;
        private IVisualElementScheduledItem repaintItem;

        public ScreenSpaceBlurElement()
        {
            blurOverlay = new BlurOverlayElement(this)
            {
                pickingMode = PickingMode.Ignore
            };
            blurOverlay.style.position = Position.Absolute;
            blurOverlay.style.left = 0;
            blurOverlay.style.top = 0;
            blurOverlay.style.right = 0;
            blurOverlay.style.bottom = 0;
            Insert(0, blurOverlay);

            RegisterCallback<AttachToPanelEvent>(HandleAttachToPanel);
            RegisterCallback<DetachFromPanelEvent>(HandleDetachFromPanel);
        }

        private void HandleAttachToPanel(AttachToPanelEvent evt)
        {
            ScreenSpaceUIToolkitBlurRendererFeature.RegisterElement();
            repaintItem = schedule.Execute(RepaintBlur).Every(16);
        }

        private void HandleDetachFromPanel(DetachFromPanelEvent evt)
        {
            ScreenSpaceUIToolkitBlurRendererFeature.UnregisterElement();
            repaintItem.Pause();
            repaintItem = null;
        }

        private void RepaintBlur()
        {
            if (resolvedStyle.display == DisplayStyle.Flex && resolvedStyle.visibility == Visibility.Visible)
                blurOverlay.MarkDirtyRepaint();
        }

        private sealed class BlurOverlayElement : VisualElement
        {
            private readonly ScreenSpaceBlurElement owner;

            public BlurOverlayElement(ScreenSpaceBlurElement owner)
            {
                this.owner = owner;
                generateVisualContent += GenerateBlurVisualContent;
            }

            private void GenerateBlurVisualContent(MeshGenerationContext context)
            {
                Texture blurTexture = ScreenSpaceUIToolkitBlurRendererFeature.BlurTexture;
                Rect rect = contentRect;
                if (blurTexture == null || blurTexture.dimension != TextureDimension.Tex2D ||
                    rect.width <= 0f || rect.height <= 0f)
                {
                    return;
                }

                IResolvedStyle ownerStyle = owner.resolvedStyle;
                Vector4 radii = new Vector4(
                    ownerStyle.borderTopLeftRadius,
                    ownerStyle.borderTopRightRadius,
                    ownerStyle.borderBottomRightRadius,
                    ownerStyle.borderBottomLeftRadius);
                float brightness = Mathf.Clamp01(owner.BlurBrightness);
                Color tint = new Color(brightness, brightness, brightness, Mathf.Clamp01(owner.BlurOpacity));
                DrawRoundedMesh(context, rect, blurTexture, tint, radii);
            }

            private void DrawRoundedMesh(
                MeshGenerationContext context,
                Rect rect,
                Texture texture,
                Color color,
                Vector4 radii)
            {
                Vector4 resolvedRadii = ClampCornerRadii(rect, radii);
                int segments = Mathf.Clamp(owner.CornerSegments, 1, 24);
                int perimeterCount = (segments + 1) * 4;
                var points = new Vector2[perimeterCount];
                int pointIndex = 0;
                AddArc(points, ref pointIndex, new Vector2(rect.xMin + resolvedRadii.w, rect.yMax - resolvedRadii.w), resolvedRadii.w, 90f, 180f, segments);
                AddArc(points, ref pointIndex, new Vector2(rect.xMin + resolvedRadii.x, rect.yMin + resolvedRadii.x), resolvedRadii.x, 180f, 270f, segments);
                AddArc(points, ref pointIndex, new Vector2(rect.xMax - resolvedRadii.y, rect.yMin + resolvedRadii.y), resolvedRadii.y, 270f, 360f, segments);
                AddArc(points, ref pointIndex, new Vector2(rect.xMax - resolvedRadii.z, rect.yMax - resolvedRadii.z), resolvedRadii.z, 0f, 90f, segments);

                var vertices = new Vertex[perimeterCount + 1];
                vertices[0] = CreateVertex(rect.center, color);
                for (int i = 0; i < perimeterCount; i++)
                    vertices[i + 1] = CreateVertex(points[i], color);

                var indices = new ushort[perimeterCount * 3];
                int index = 0;
                for (int i = 0; i < perimeterCount; i++)
                {
                    indices[index++] = 0;
                    indices[index++] = (ushort)(i + 1);
                    indices[index++] = (ushort)(((i + 1) % perimeterCount) + 1);
                }

                MeshWriteData mesh = context.Allocate(vertices.Length, indices.Length, texture);
                Rect uvRegion = mesh.uvRegion;
                for (int i = 0; i < vertices.Length; i++)
                    vertices[i].uv = vertices[i].uv * uvRegion.size + uvRegion.min;

                mesh.SetAllVertices(vertices);
                mesh.SetAllIndices(indices);
            }

            private Vertex CreateVertex(Vector2 position, Color color)
            {
                VisualElement root = panel.visualTree;
                Rect rootBounds = root.worldBound;
                Vector2 panelPoint = this.LocalToWorld(position);
                Vector2 uv = new Vector2(
                    Mathf.InverseLerp(rootBounds.xMin, rootBounds.xMax, panelPoint.x),
                    1f - Mathf.InverseLerp(rootBounds.yMin, rootBounds.yMax, panelPoint.y));
                return new Vertex
                {
                    position = new Vector3(position.x, position.y, Vertex.nearZ),
                    tint = color,
                    uv = uv
                };
            }

            private static Vector4 ClampCornerRadii(Rect rect, Vector4 radii)
            {
                float cornerLimit = Mathf.Min(rect.width, rect.height) * 0.5f;
                return new Vector4(
                    Mathf.Min(radii.x, cornerLimit),
                    Mathf.Min(radii.y, cornerLimit),
                    Mathf.Min(radii.z, cornerLimit),
                    Mathf.Min(radii.w, cornerLimit));
            }

            private static void AddArc(
                Vector2[] points,
                ref int pointIndex,
                Vector2 center,
                float radius,
                float startAngle,
                float endAngle,
                int segments)
            {
                for (int i = 0; i <= segments; i++)
                {
                    float angle = Mathf.Lerp(startAngle, endAngle, i / (float)segments) * Mathf.Deg2Rad;
                    points[pointIndex++] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                }
            }
        }
    }
}
