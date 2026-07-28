using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
#if UNITY_EDITOR
using UnityEditor.UIElements;

[assembly: UxmlNamespacePrefix("MemoAnchor.UI", "ma")]
#endif

namespace MemoAnchor.UI
{
    [UxmlElement]
    public partial class ScanOutsideBlurElement : VisualElement
    {
        [UxmlAttribute("target-element-name")]
        public string TargetElementName { get; set; } = "scan-frame";

        [UxmlAttribute("blur-opacity")]
        public float BlurOpacity { get; set; } = 0.94f;

        [UxmlAttribute("blur-brightness")]
        public float BlurBrightness { get; set; } = 0.72f;

        [UxmlAttribute("corner-segments")]
        public int CornerSegments { get; set; } = 10;

        [UxmlAttribute("inner-corner-radius")]
        public float InnerCornerRadius { get; set; } = -1f;

        private readonly BlurOverlayElement blurOverlay;
        private IVisualElementScheduledItem repaintItem;

        public ScanOutsideBlurElement()
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
            private readonly ScanOutsideBlurElement owner;

            public BlurOverlayElement(ScanOutsideBlurElement owner)
            {
                this.owner = owner;
                generateVisualContent += GenerateBlurVisualContent;
            }

            private void GenerateBlurVisualContent(MeshGenerationContext context)
            {
                Texture blurTexture = ScreenSpaceUIToolkitBlurRendererFeature.BlurTexture;
                Rect outerRect = contentRect;
                if (blurTexture == null || blurTexture.dimension != TextureDimension.Tex2D ||
                    outerRect.width <= 0f || outerRect.height <= 0f)
                {
                    return;
                }

                VisualElement target = panel.visualTree.Q<VisualElement>(owner.TargetElementName);
                Rect targetWorldBounds = target.worldBound;
                Vector2 targetMin = this.WorldToLocal(targetWorldBounds.min);
                Vector2 targetMax = this.WorldToLocal(targetWorldBounds.max);
                Rect innerRect = Rect.MinMaxRect(targetMin.x, targetMin.y, targetMax.x, targetMax.y);
                if (innerRect.width <= 0f || innerRect.height <= 0f)
                    return;

                Vector4 radii;
                if (owner.InnerCornerRadius >= 0f)
                {
                    float radius = owner.InnerCornerRadius;
                    radii = new Vector4(radius, radius, radius, radius);
                }
                else
                {
                    IResolvedStyle targetStyle = target.resolvedStyle;
                    radii = new Vector4(
                        targetStyle.borderTopLeftRadius,
                        targetStyle.borderTopRightRadius,
                        targetStyle.borderBottomRightRadius,
                        targetStyle.borderBottomLeftRadius);
                }
                float brightness = Mathf.Clamp01(owner.BlurBrightness);
                Color tint = new Color(brightness, brightness, brightness, Mathf.Clamp01(owner.BlurOpacity));
                DrawOutsideRoundedRect(context, outerRect, innerRect, blurTexture, tint, radii);
            }

            private void DrawOutsideRoundedRect(
                MeshGenerationContext context,
                Rect outerRect,
                Rect innerRect,
                Texture texture,
                Color color,
                Vector4 radii)
            {
                innerRect.xMin = Mathf.Clamp(innerRect.xMin, outerRect.xMin, outerRect.xMax);
                innerRect.xMax = Mathf.Clamp(innerRect.xMax, outerRect.xMin, outerRect.xMax);
                innerRect.yMin = Mathf.Clamp(innerRect.yMin, outerRect.yMin, outerRect.yMax);
                innerRect.yMax = Mathf.Clamp(innerRect.yMax, outerRect.yMin, outerRect.yMax);

                var vertices = new List<Vertex>(80);
                var indices = new List<ushort>(180);
                AddRect(vertices, indices, Rect.MinMaxRect(outerRect.xMin, outerRect.yMin, outerRect.xMax, innerRect.yMin), color);
                AddRect(vertices, indices, Rect.MinMaxRect(outerRect.xMin, innerRect.yMax, outerRect.xMax, outerRect.yMax), color);
                AddRect(vertices, indices, Rect.MinMaxRect(outerRect.xMin, innerRect.yMin, innerRect.xMin, innerRect.yMax), color);
                AddRect(vertices, indices, Rect.MinMaxRect(innerRect.xMax, innerRect.yMin, outerRect.xMax, innerRect.yMax), color);

                Vector4 resolvedRadii = ClampCornerRadii(innerRect, radii);
                int segments = Mathf.Clamp(owner.CornerSegments, 1, 24);
                AddCornerFan(
                    vertices,
                    indices,
                    new Vector2(innerRect.xMin, innerRect.yMin),
                    new Vector2(innerRect.xMin + resolvedRadii.x, innerRect.yMin + resolvedRadii.x),
                    resolvedRadii.x,
                    180f,
                    270f,
                    segments,
                    color);
                AddCornerFan(
                    vertices,
                    indices,
                    new Vector2(innerRect.xMax, innerRect.yMin),
                    new Vector2(innerRect.xMax - resolvedRadii.y, innerRect.yMin + resolvedRadii.y),
                    resolvedRadii.y,
                    270f,
                    360f,
                    segments,
                    color);
                AddCornerFan(
                    vertices,
                    indices,
                    new Vector2(innerRect.xMax, innerRect.yMax),
                    new Vector2(innerRect.xMax - resolvedRadii.z, innerRect.yMax - resolvedRadii.z),
                    resolvedRadii.z,
                    0f,
                    90f,
                    segments,
                    color);
                AddCornerFan(
                    vertices,
                    indices,
                    new Vector2(innerRect.xMin, innerRect.yMax),
                    new Vector2(innerRect.xMin + resolvedRadii.w, innerRect.yMax - resolvedRadii.w),
                    resolvedRadii.w,
                    90f,
                    180f,
                    segments,
                    color);

                MeshWriteData mesh = context.Allocate(vertices.Count, indices.Count, texture);
                Rect uvRegion = mesh.uvRegion;
                for (int i = 0; i < vertices.Count; i++)
                {
                    Vertex vertex = vertices[i];
                    vertex.uv = vertex.uv * uvRegion.size + uvRegion.min;
                    vertices[i] = vertex;
                }

                mesh.SetAllVertices(vertices.ToArray());
                mesh.SetAllIndices(indices.ToArray());
            }

            private void AddRect(List<Vertex> vertices, List<ushort> indices, Rect rect, Color color)
            {
                if (rect.width <= 0f || rect.height <= 0f)
                    return;

                ushort start = (ushort)vertices.Count;
                vertices.Add(CreateVertex(new Vector2(rect.xMin, rect.yMin), color));
                vertices.Add(CreateVertex(new Vector2(rect.xMax, rect.yMin), color));
                vertices.Add(CreateVertex(new Vector2(rect.xMax, rect.yMax), color));
                vertices.Add(CreateVertex(new Vector2(rect.xMin, rect.yMax), color));
                indices.Add(start);
                indices.Add((ushort)(start + 1));
                indices.Add((ushort)(start + 2));
                indices.Add((ushort)(start + 2));
                indices.Add((ushort)(start + 3));
                indices.Add(start);
            }

            private void AddCornerFan(
                List<Vertex> vertices,
                List<ushort> indices,
                Vector2 origin,
                Vector2 center,
                float radius,
                float startAngle,
                float endAngle,
                int segments,
                Color color)
            {
                if (radius <= 0f)
                    return;

                ushort originIndex = (ushort)vertices.Count;
                vertices.Add(CreateVertex(origin, color));
                ushort arcStartIndex = (ushort)vertices.Count;
                for (int i = 0; i <= segments; i++)
                {
                    float angle = Mathf.Lerp(startAngle, endAngle, i / (float)segments) * Mathf.Deg2Rad;
                    Vector2 point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                    vertices.Add(CreateVertex(point, color));
                }

                for (int i = 0; i < segments; i++)
                {
                    indices.Add(originIndex);
                    indices.Add((ushort)(arcStartIndex + i + 1));
                    indices.Add((ushort)(arcStartIndex + i));
                }
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
        }
    }
}
