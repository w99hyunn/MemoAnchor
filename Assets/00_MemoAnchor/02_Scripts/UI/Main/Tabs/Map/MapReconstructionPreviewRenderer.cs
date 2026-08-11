using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace MemoAnchor.UI
{
    public readonly struct MapPreviewMemoMarker
    {
        public readonly Vector3 Position;
        public readonly string Title;
        public readonly string KindClass;
        public readonly bool IsCompletionRequested;
        public readonly Action OpenDetail;

        public MapPreviewMemoMarker(
            Vector3 position,
            string title,
            string kindClass,
            bool isCompletionRequested,
            Action openDetail)
        {
            Position = position;
            Title = title;
            KindClass = kindClass;
            IsCompletionRequested = isCompletionRequested;
            OpenDetail = openDetail;
        }
    }

    public sealed class MapReconstructionPreviewRenderer : MonoBehaviour
    {
        private const int PREVIEW_LAYER = 31;
        private const int MAX_RENDER_TEXTURE_SIZE = 2048;

        private Image _target;
        private Camera _previewCamera;
        private RenderTexture _renderTexture;
        private GameObject _previewRoot;
        private Transform _previewSurface;
        private Mesh _mesh;
        private Material _material;
        private Mesh _gridMesh;
        private Material _gridMaterial;
        private VisualElement _memoMarkerContainer;
        private Button _memoCard;
        private VisualElement _memoCardKind;
        private VisualElement _memoCardStem;
        private Label _memoCardTitle;
        private readonly List<MapPreviewMemoMarker> _memoMarkers = new();
        private readonly List<Button> _memoMarkerButtons = new();
        private Vector3 _previousPointerPosition;
        private Vector2 _firstPointerPosition;
        private Vector2 _secondPointerPosition;
        private Vector2 _pinchStartCenter;
        private float _yaw = -35f;
        private float _pitch = 28f;
        private float _distance;
        private float _minimumDistance;
        private float _maximumDistance;
        private float _pinchStartDistance;
        private float _pinchStartCameraDistance;
        private float _fitDistanceScale = 1f;
        private float _viewRadius;
        private Vector3 _focusPosition;
        private Vector3 _panOffset;
        private Vector3 _pinchStartPanOffset;
        private Vector3 _pinchStartCameraRight;
        private Vector3 _pinchStartCameraUp;
        private int _firstPointerId = -1;
        private int _secondPointerId = -1;
        private int _previewLayer;
        private int _selectedMemoMarkerIndex = -1;
        private bool _isDragging;
        private bool _isViewActive;
        private bool _hasFocusPosition;
        private bool _showOrientationAids;

        public void Initialize(Image target)
        {
            Initialize(target, PREVIEW_LAYER);
        }

        public void Initialize(Image target, int previewLayer)
        {
            _target = target;
            _previewLayer = previewLayer;
            _target.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            _target.RegisterCallback<PointerDownEvent>(OnPointerDown);
            _target.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            _target.RegisterCallback<PointerUpEvent>(OnPointerUp);
            _target.RegisterCallback<PointerCancelEvent>(OnPointerCancel);
            _target.RegisterCallback<WheelEvent>(OnWheel);
            MapBackfaceDisplaySettings.Changed += ApplyBackfaceDisplayMode;

            CreatePreviewCamera();
        }

        public void Initialize(
            Image target,
            VisualElement memoMarkerContainer,
            Button memoCard,
            VisualElement memoCardKind,
            VisualElement memoCardStem,
            Label memoCardTitle)
        {
            _showOrientationAids = true;
            Initialize(target);
            _memoMarkerContainer = memoMarkerContainer;
            _memoCard = memoCard;
            _memoCardKind = memoCardKind;
            _memoCardStem = memoCardStem;
            _memoCardTitle = memoCardTitle;
            _memoCard.clicked += OpenSelectedMemoDetail;
        }

        public void Show(Mesh mesh)
        {
            Show(mesh, ARKitMeshScanController.CreateReconstructionPreviewMaterial(mesh));
        }

        public void Show(Mesh mesh, Material material)
        {
            ClearMesh();

            _mesh = mesh;
            _material = material;
            ApplyBackfaceDisplayMode();
            _previewRoot = new GameObject("Map Reconstruction Preview");
            _previewRoot.transform.SetParent(transform, false);
            _previewRoot.transform.position = Vector3.one * 10000f;
            _previewRoot.layer = _previewLayer;

            GameObject surface = new("Map Reconstruction Surface");
            surface.transform.SetParent(_previewRoot.transform, false);
            surface.layer = _previewLayer;
            surface.AddComponent<MeshFilter>().sharedMesh = _mesh;
            _previewSurface = surface.transform;

            MeshRenderer meshRenderer = surface.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = _material;

            Bounds bounds = _mesh.bounds;
            if (_showOrientationAids)
            {
                CreateOrientationAids(bounds);
            }
            float radius = Mathf.Max(bounds.extents.magnitude, 0.1f);
            _hasFocusPosition = false;
            _panOffset = Vector3.zero;
            _fitDistanceScale = 1f;
            ApplyViewRadius(radius);
            _yaw = -35f;
            _pitch = 28f;

            UpdateCameraTransform();
            _target.RemoveFromClassList("is-hidden");
            if (_isViewActive && _renderTexture)
            {
                _previewCamera.enabled = true;
            }
        }

        public void FocusOn(Vector3 position)
        {
            if (!_mesh)
            {
                return;
            }

            _focusPosition = position;
            _hasFocusPosition = true;
            _panOffset = Vector3.zero;
            _fitDistanceScale = 0.82f;
            ApplyViewRadius(CalculateRadiusFromPosition(
                _mesh.bounds,
                position));
            UpdateCameraTransform();
        }

        public void SetMarkers(IReadOnlyList<MapPreviewMemoMarker> markers)
        {
            ClearMemoMarkers();
            for (int i = 0; i < markers.Count; i++)
            {
                int markerIndex = i;
                Button markerButton = new()
                {
                    pickingMode = PickingMode.Position
                };
                markerButton.AddToClassList("map-reconstruction-memo-marker");
                markerButton.EnableInClassList("is-completion-requested", markers[i].IsCompletionRequested);
                markerButton.clicked += () => SelectMemoMarker(markerIndex);
                _memoMarkerContainer.Add(markerButton);
                _memoMarkerButtons.Add(markerButton);
                _memoMarkers.Add(markers[i]);
            }

            UpdateMemoMarkerPositions();
        }

        private void SelectMemoMarker(int markerIndex)
        {
            if (_selectedMemoMarkerIndex == markerIndex)
            {
                OpenSelectedMemoDetail();
                return;
            }

            _selectedMemoMarkerIndex = markerIndex;
            MapPreviewMemoMarker marker = _memoMarkers[markerIndex];
            _memoCardTitle.text = marker.Title;
            _memoCardKind.RemoveFromClassList("memo-list-item-icon-text");
            _memoCardKind.RemoveFromClassList("memo-list-item-icon-check");
            _memoCardKind.RemoveFromClassList("memo-list-item-icon-mic");
            _memoCardKind.RemoveFromClassList("memo-list-item-icon-gallery");
            _memoCardKind.AddToClassList(marker.KindClass);
            _memoCard.EnableInClassList("is-completion-requested", marker.IsCompletionRequested);
            _memoCardStem.EnableInClassList("is-completion-requested", marker.IsCompletionRequested);
            _memoCard.RemoveFromClassList("is-concealed");
            UpdateMemoMarkerPositions();
        }

        private void OpenSelectedMemoDetail()
        {
            if (_selectedMemoMarkerIndex < 0)
                return;

            Action openDetail = _memoMarkers[_selectedMemoMarkerIndex].OpenDetail;
            HideSelectedMemoCard();
            openDetail();
        }

        private void HideSelectedMemoCard()
        {
            _selectedMemoMarkerIndex = -1;
            _memoCard.AddToClassList("is-concealed");
            _memoCardStem.AddToClassList("is-concealed");
            _memoCard.RemoveFromClassList("is-completion-requested");
            _memoCardStem.RemoveFromClassList("is-completion-requested");
        }

        private void ClearMemoMarkers()
        {
            if (_memoMarkerContainer == null)
            {
                return;
            }

            if (_memoCard != null)
            {
                HideSelectedMemoCard();
            }
            _memoMarkerContainer.Clear();
            _memoMarkerButtons.Clear();
            _memoMarkers.Clear();
        }

        private void UpdateMemoMarkerPositions()
        {
            if (!_mesh || _memoMarkers.Count == 0)
                return;

            float panelWidth = _target.resolvedStyle.width;
            float panelHeight = _target.resolvedStyle.height;
            bool selectedMarkerVisible = false;
            Vector2 selectedAnchor = default;
            for (int i = 0; i < _memoMarkers.Count; i++)
            {
                Vector3 worldPosition = _previewRoot.transform.TransformPoint(_memoMarkers[i].Position);
                Vector3 viewportPosition = _previewCamera.WorldToViewportPoint(worldPosition);
                bool visible = viewportPosition.z > 0f
                    && viewportPosition.x >= 0f
                    && viewportPosition.x <= 1f
                    && viewportPosition.y >= 0f
                    && viewportPosition.y <= 1f;
                Button markerButton = _memoMarkerButtons[i];
                markerButton.EnableInClassList("is-hidden", !visible);
                if (!visible)
                    continue;

                float left = viewportPosition.x * panelWidth;
                float top = (1f - viewportPosition.y) * panelHeight;
                markerButton.style.left = left;
                markerButton.style.top = top;
                if (i == _selectedMemoMarkerIndex)
                {
                    selectedMarkerVisible = true;
                    selectedAnchor = new Vector2(left, top);
                }
            }

            _memoCard.EnableInClassList("is-concealed", _selectedMemoMarkerIndex < 0 || !selectedMarkerVisible);
            _memoCardStem.EnableInClassList("is-concealed", _selectedMemoMarkerIndex < 0 || !selectedMarkerVisible);
            if (selectedMarkerVisible)
                PositionMemoCard(selectedAnchor, panelWidth, panelHeight);
        }

        private void PositionMemoCard(Vector2 anchor, float panelWidth, float panelHeight)
        {
            float cardWidth = _memoCard.resolvedStyle.width;
            float cardHeight = _memoCard.resolvedStyle.height;
            float markerHeight = _memoMarkerButtons[_selectedMemoMarkerIndex].resolvedStyle.height;
            float edgeMargin = markerHeight * 0.35f;
            float desiredStemHeight = markerHeight * 1.7f;
            float maximumCardTop = Mathf.Max(edgeMargin, panelHeight - cardHeight - edgeMargin);
            float cardLeft = Mathf.Clamp(
                anchor.x - cardWidth * 0.5f,
                edgeMargin,
                Mathf.Max(edgeMargin, panelWidth - cardWidth - edgeMargin));
            float cardTop = Mathf.Clamp(
                anchor.y - markerHeight - desiredStemHeight - cardHeight,
                edgeMargin,
                maximumCardTop);
            float stemHeight = Mathf.Max(0f, anchor.y - markerHeight - cardTop - cardHeight);

            _memoCard.style.left = cardLeft;
            _memoCard.style.top = cardTop;
            _memoCardStem.style.left = anchor.x - _memoCardStem.resolvedStyle.width * 0.5f;
            _memoCardStem.style.top = cardTop + cardHeight;
            _memoCardStem.style.height = stemHeight;
        }

        public void SetViewActive(bool active)
        {
            _isViewActive = active;
            _previewCamera.enabled = active && _mesh && _renderTexture;
        }

        public void Clear()
        {
            ClearMesh();
            _target.AddToClassList("is-hidden");
            _previewCamera.enabled = false;
        }

        private void CreatePreviewCamera()
        {
            GameObject cameraObject = new("Map Reconstruction Preview Camera");
            cameraObject.transform.SetParent(transform, false);
            cameraObject.layer = _previewLayer;

            _previewCamera = cameraObject.AddComponent<Camera>();
            _previewCamera.clearFlags = CameraClearFlags.SolidColor;
            _previewCamera.backgroundColor = new Color(0.675f, 0.675f, 0.675f, 1f);
            _previewCamera.cullingMask = 1 << _previewLayer;
            _previewCamera.fieldOfView = 40f;
            _previewCamera.allowHDR = false;
            _previewCamera.allowMSAA = true;
            _previewCamera.enabled = false;

            GameObject lightObject = new("Map Reconstruction Preview Light");
            lightObject.transform.SetParent(transform, false);
            lightObject.transform.localRotation = new Quaternion(0.40821794f, -0.23456973f, 0.10938166f, 0.8754261f);
            lightObject.layer = _previewLayer;

            Light previewLight = lightObject.AddComponent<Light>();
            previewLight.type = LightType.Directional;
            previewLight.intensity = 0.6f;
            previewLight.cullingMask = 1 << _previewLayer;
            previewLight.shadows = LightShadows.None;
        }

        private void ApplyBackfaceDisplayMode()
        {
            if (!_material)
            {
                return;
            }

            MapBackfaceDisplayMode mode = MapBackfaceDisplaySettings.Current;
            if (_material.HasProperty("_Cull"))
            {
                _material.SetFloat(
                    "_Cull",
                    mode == MapBackfaceDisplayMode.Hidden
                        ? (float)UnityEngine.Rendering.CullMode.Back
                        : (float)UnityEngine.Rendering.CullMode.Off);
            }
            if (_material.HasProperty("_UseBackColor"))
            {
                bool useBackColor = mode == MapBackfaceDisplayMode.SolidColor;
                _material.SetFloat("_UseBackColor", useBackColor ? 1f : 0f);
                _material.SetColor("_BackColor", new Color(0.32f, 0.36f, 0.42f, 1f));
            }
        }

        private void CreateOrientationAids(Bounds bounds)
        {
            CreateGroundGrid(bounds);
        }

        private void CreateGroundGrid(Bounds bounds)
        {
            float horizontalRadius = Mathf.Max(bounds.extents.x, bounds.extents.z, 0.1f);
            float spacing = CalculateGridSpacing(horizontalRadius * 0.25f);
            float halfExtent = Mathf.Ceil(horizontalRadius * 1.35f / spacing) * spacing;
            int lineCount = Mathf.RoundToInt(halfExtent / spacing);
            float heightOffset = Mathf.Max(bounds.size.y * 0.003f, 0.002f);
            float gridY = bounds.min.y - heightOffset;
            Vector3[] vertices = new Vector3[(lineCount * 2 + 1) * 4];
            int vertexIndex = 0;

            for (int i = -lineCount; i <= lineCount; i++)
            {
                float offset = i * spacing;
                vertices[vertexIndex++] = new Vector3(bounds.center.x - halfExtent, gridY, bounds.center.z + offset);
                vertices[vertexIndex++] = new Vector3(bounds.center.x + halfExtent, gridY, bounds.center.z + offset);
                vertices[vertexIndex++] = new Vector3(bounds.center.x + offset, gridY, bounds.center.z - halfExtent);
                vertices[vertexIndex++] = new Vector3(bounds.center.x + offset, gridY, bounds.center.z + halfExtent);
            }

            _gridMesh = new Mesh
            {
                name = "Map Preview Ground Grid"
            };
            _gridMesh.vertices = vertices;
            int[] indices = new int[vertices.Length];
            for (int i = 0; i < indices.Length; i++)
            {
                indices[i] = i;
            }
            _gridMesh.SetIndices(indices, MeshTopology.Lines, 0);
            _gridMesh.RecalculateBounds();

            GameObject gridObject = new("Map Preview Ground Grid");
            gridObject.transform.SetParent(_previewRoot.transform, false);
            gridObject.layer = _previewLayer;
            gridObject.AddComponent<MeshFilter>().sharedMesh = _gridMesh;

            Shader gridShader = Shader.Find("Sprites/Default")
                                ?? Shader.Find("Universal Render Pipeline/Unlit");
            _gridMaterial = new Material(gridShader)
            {
                name = "Map Preview Ground Grid Material"
            };
            Color gridColor = new(0.25f, 0.29f, 0.34f, 0.22f);
            if (_gridMaterial.HasProperty("_Color"))
            {
                _gridMaterial.SetColor("_Color", gridColor);
            }
            if (_gridMaterial.HasProperty("_BaseColor"))
            {
                _gridMaterial.SetColor("_BaseColor", gridColor);
            }

            MeshRenderer gridRenderer = gridObject.AddComponent<MeshRenderer>();
            gridRenderer.sharedMaterial = _gridMaterial;
            gridRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            gridRenderer.receiveShadows = false;
        }

        private static float CalculateGridSpacing(float targetSpacing)
        {
            float magnitude = Mathf.Pow(10f, Mathf.Floor(Mathf.Log10(Mathf.Max(targetSpacing, 0.001f))));
            float normalized = targetSpacing / magnitude;
            if (normalized >= 5f)
            {
                return 5f * magnitude;
            }
            if (normalized >= 2f)
            {
                return 2f * magnitude;
            }
            return magnitude;
        }

        private void OnGeometryChanged(GeometryChangedEvent evt)
        {
            float renderScale = Mathf.Min(
                1f,
                MAX_RENDER_TEXTURE_SIZE / Mathf.Max(evt.newRect.width, evt.newRect.height));
            int width = Mathf.Max(1, Mathf.RoundToInt(evt.newRect.width * renderScale));
            int height = Mathf.Max(1, Mathf.RoundToInt(evt.newRect.height * renderScale));
            if (_renderTexture && _renderTexture.width == width && _renderTexture.height == height)
            {
                return;
            }

            ReleaseRenderTexture();
            _renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                name = "Map Reconstruction Preview",
                antiAliasing = 4
            };
            _renderTexture.Create();
            _previewCamera.targetTexture = _renderTexture;
            _previewCamera.enabled = _isViewActive && _mesh;
            _target.image = _renderTexture;

            if (_mesh)
            {
                _distance = Mathf.Clamp(CalculateFitDistance(_viewRadius) * _fitDistanceScale, _minimumDistance, _maximumDistance);
                UpdateCameraTransform();
            }
        }

        private void ApplyViewRadius(float radius)
        {
            _viewRadius = Mathf.Max(radius, 0.1f);
            _minimumDistance = _viewRadius * 0.75f;
            _maximumDistance = _viewRadius * 8f;
            _distance = CalculateFitDistance(_viewRadius) * _fitDistanceScale;
        }

        private static float CalculateRadiusFromPosition(Bounds bounds, Vector3 position)
        {
            float radius = 0.1f;
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            for (int x = 0; x < 2; x++)
            {
                for (int y = 0; y < 2; y++)
                {
                    for (int z = 0; z < 2; z++)
                    {
                        Vector3 corner = new(
                            x == 0 ? min.x : max.x,
                            y == 0 ? min.y : max.y,
                            z == 0 ? min.z : max.z);
                        radius = Mathf.Max(radius, Vector3.Distance(position, corner));
                    }
                }
            }

            return radius;
        }

        private float CalculateFitDistance(float radius)
        {
            float aspect = _renderTexture ? _renderTexture.width / (float)_renderTexture.height : 1f;
            float verticalFov = _previewCamera.fieldOfView * Mathf.Deg2Rad;
            float horizontalFov = 2f * Mathf.Atan(Mathf.Tan(verticalFov * 0.5f) * aspect);
            float fitFov = Mathf.Min(verticalFov, horizontalFov);
            return radius / Mathf.Tan(fitFov * 0.5f);
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (!_mesh || evt.button != 0)
            {
                return;
            }

            if (_firstPointerId < 0)
            {
                _target.CapturePointer(evt.pointerId);
                _firstPointerId = evt.pointerId;
                _firstPointerPosition = evt.position;
                _previousPointerPosition = evt.position;
                _isDragging = true;
            }
            else if (_secondPointerId < 0 && evt.pointerId != _firstPointerId)
            {
                _target.CapturePointer(evt.pointerId);
                _secondPointerId = evt.pointerId;
                _secondPointerPosition = evt.position;
                _pinchStartDistance = Vector2.Distance(_firstPointerPosition, _secondPointerPosition);
                _pinchStartCameraDistance = _distance;
                _pinchStartCenter = (_firstPointerPosition + _secondPointerPosition) * 0.5f;
                _pinchStartPanOffset = _panOffset;
                _pinchStartCameraRight = _previewCamera.transform.right;
                _pinchStartCameraUp = _previewCamera.transform.up;
                _isDragging = false;
            }
            evt.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (evt.pointerId == _firstPointerId)
            {
                _firstPointerPosition = evt.position;
            }
            else if (evt.pointerId == _secondPointerId)
            {
                _secondPointerPosition = evt.position;
            }
            else
            {
                return;
            }

            if (_secondPointerId >= 0 && _pinchStartDistance > 0f)
            {
                Vector2 pinchCenter = (_firstPointerPosition + _secondPointerPosition) * 0.5f;
                float pinchDistance = Vector2.Distance(_firstPointerPosition, _secondPointerPosition);
                _distance = Mathf.Clamp(
                    _pinchStartCameraDistance * _pinchStartDistance / Mathf.Max(pinchDistance, 1f),
                    _minimumDistance,
                    _maximumDistance);

                Vector2 centerDelta = pinchCenter - _pinchStartCenter;
                float viewHeight = Mathf.Max(_target.resolvedStyle.height, 1f);
                float worldUnitsPerPixel = 2f * _pinchStartCameraDistance
                    * Mathf.Tan(_previewCamera.fieldOfView * 0.5f * Mathf.Deg2Rad) / viewHeight;
                _panOffset = _pinchStartPanOffset
                    + (-_pinchStartCameraRight * centerDelta.x
                       + _pinchStartCameraUp * centerDelta.y) * worldUnitsPerPixel;
                ClampPanOffset();
                UpdateCameraTransform();
                evt.StopPropagation();
                return;
            }

            if (!_isDragging || evt.pointerId != _firstPointerId || !_target.HasPointerCapture(evt.pointerId))
            {
                return;
            }

            Vector3 delta = evt.position - _previousPointerPosition;
            _previousPointerPosition = evt.position;
            _yaw += delta.x * 0.18f;
            _pitch = Mathf.Clamp(_pitch - delta.y * 0.18f, -80f, 80f);
            UpdateCameraTransform();
            evt.StopPropagation();
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            RemovePointer(evt.pointerId);
        }

        private void OnPointerCancel(PointerCancelEvent evt)
        {
            RemovePointer(evt.pointerId);
        }

        private void RemovePointer(int pointerId)
        {
            if (_target.HasPointerCapture(pointerId))
            {
                _target.ReleasePointer(pointerId);
            }

            if (pointerId == _firstPointerId)
            {
                _firstPointerId = _secondPointerId;
                _firstPointerPosition = _secondPointerPosition;
                _secondPointerId = -1;
            }
            else if (pointerId == _secondPointerId)
            {
                _secondPointerId = -1;
            }

            _pinchStartDistance = 0f;
            _pinchStartCameraDistance = _distance;
            _pinchStartCenter = _firstPointerPosition;
            _isDragging = _firstPointerId >= 0;
            _previousPointerPosition = _firstPointerPosition;
        }

        private void ClampPanOffset()
        {
            Vector3 basePosition = _hasFocusPosition ? _focusPosition : _mesh.bounds.center;
            Vector3 targetPosition = basePosition + _previewRoot.transform.InverseTransformVector(_panOffset);
            Vector3 margin = _mesh.bounds.extents * 0.25f;
            Vector3 minimum = _mesh.bounds.min - margin;
            Vector3 maximum = _mesh.bounds.max + margin;
            targetPosition = new Vector3(
                Mathf.Clamp(targetPosition.x, minimum.x, maximum.x),
                Mathf.Clamp(targetPosition.y, minimum.y, maximum.y),
                Mathf.Clamp(targetPosition.z, minimum.z, maximum.z));
            _panOffset = _previewRoot.transform.TransformVector(targetPosition - basePosition);
        }

        private void OnWheel(WheelEvent evt)
        {
            if (!_mesh)
            {
                return;
            }

            _distance = Mathf.Clamp(_distance * (1f + evt.delta.y * 0.04f), _minimumDistance, _maximumDistance);
            UpdateCameraTransform();
            evt.StopPropagation();
        }

        private void UpdateCameraTransform()
        {
            Vector3 center = _hasFocusPosition
                ? _previewRoot.transform.TransformPoint(_focusPosition)
                : _previewSurface.TransformPoint(_mesh.bounds.center);
            center += _panOffset;
            Quaternion orbit = Quaternion.Euler(_pitch, _yaw, 0f);
            _previewCamera.transform.position = center - orbit * Vector3.forward * _distance;
            _previewCamera.transform.rotation = Quaternion.LookRotation(center - _previewCamera.transform.position, Vector3.up);

            _previewCamera.nearClipPlane = Mathf.Max(0.01f, _distance - _viewRadius * 2f);
            _previewCamera.farClipPlane = _distance + _viewRadius * 4f;
            UpdateMemoMarkerPositions();
        }

        private void ClearMesh()
        {
            ResetPointerInput();
            ClearMemoMarkers();

            if (_previewRoot)
            {
                Destroy(_previewRoot);
            }
            if (_material)
            {
                Destroy(_material);
            }
            if (_gridMaterial)
            {
                Destroy(_gridMaterial);
            }
            if (_gridMesh)
            {
                Destroy(_gridMesh);
            }
            if (_mesh)
            {
                Destroy(_mesh);
            }

            _previewRoot = null;
            _previewSurface = null;
            _material = null;
            _gridMaterial = null;
            _gridMesh = null;
            _mesh = null;
            _hasFocusPosition = false;
            _panOffset = Vector3.zero;
        }

        private void ResetPointerInput()
        {
            if (_firstPointerId >= 0 && _target.HasPointerCapture(_firstPointerId))
            {
                _target.ReleasePointer(_firstPointerId);
            }
            if (_secondPointerId >= 0 && _target.HasPointerCapture(_secondPointerId))
            {
                _target.ReleasePointer(_secondPointerId);
            }

            _firstPointerId = -1;
            _secondPointerId = -1;
            _pinchStartDistance = 0f;
            _pinchStartCameraDistance = 0f;
            _pinchStartCenter = Vector2.zero;
            _pinchStartPanOffset = Vector3.zero;
            _isDragging = false;
        }

        private void ReleaseRenderTexture()
        {
            if (!_renderTexture)
            {
                return;
            }

            _previewCamera.targetTexture = null;
            _previewCamera.enabled = false;
            _target.image = null;
            _renderTexture.Release();
            Destroy(_renderTexture);
            _renderTexture = null;
        }

        private void OnDestroy()
        {
            MapBackfaceDisplaySettings.Changed -= ApplyBackfaceDisplayMode;
            _target.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            _target.UnregisterCallback<PointerDownEvent>(OnPointerDown);
            _target.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
            _target.UnregisterCallback<PointerUpEvent>(OnPointerUp);
            _target.UnregisterCallback<PointerCancelEvent>(OnPointerCancel);
            _target.UnregisterCallback<WheelEvent>(OnWheel);
            if (_memoCard != null)
            {
                _memoCard.clicked -= OpenSelectedMemoDetail;
            }
            ClearMesh();
            ReleaseRenderTexture();
        }
    }
}
