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
        private float _yaw = -35f;
        private float _pitch = 28f;
        private float _distance;
        private float _minimumDistance;
        private float _maximumDistance;
        private float _pinchStartDistance;
        private float _pinchStartCameraDistance;
        private int _firstPointerId = -1;
        private int _secondPointerId = -1;
        private int _selectedMemoMarkerIndex = -1;
        private bool _isDragging;
        private bool _isViewActive;

        public void Initialize(
            Image target,
            VisualElement memoMarkerContainer,
            Button memoCard,
            VisualElement memoCardKind,
            VisualElement memoCardStem,
            Label memoCardTitle)
        {
            _target = target;
            _memoMarkerContainer = memoMarkerContainer;
            _memoCard = memoCard;
            _memoCardKind = memoCardKind;
            _memoCardStem = memoCardStem;
            _memoCardTitle = memoCardTitle;
            _target.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            _target.RegisterCallback<PointerDownEvent>(OnPointerDown);
            _target.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            _target.RegisterCallback<PointerUpEvent>(OnPointerUp);
            _target.RegisterCallback<PointerCancelEvent>(OnPointerCancel);
            _target.RegisterCallback<WheelEvent>(OnWheel);
            _memoCard.clicked += OpenSelectedMemoDetail;

            CreatePreviewCamera();
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
            _previewRoot = new GameObject("Map Reconstruction Preview");
            _previewRoot.transform.SetParent(transform, false);
            _previewRoot.transform.position = Vector3.one * 10000f;
            _previewRoot.layer = PREVIEW_LAYER;

            GameObject surface = new("Map Reconstruction Surface");
            surface.transform.SetParent(_previewRoot.transform, false);
            surface.transform.localScale = new Vector3(1f, 1f, -1f);
            surface.layer = PREVIEW_LAYER;
            surface.AddComponent<MeshFilter>().sharedMesh = _mesh;
            _previewSurface = surface.transform;

            MeshRenderer meshRenderer = surface.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = _material;

            Bounds bounds = _mesh.bounds;
            float radius = Mathf.Max(bounds.extents.magnitude, 0.1f);
            _minimumDistance = radius * 0.75f;
            _maximumDistance = radius * 8f;
            _distance = CalculateFitDistance(radius);
            _yaw = -35f;
            _pitch = 28f;

            UpdateCameraTransform();
            _target.RemoveFromClassList("is-hidden");
            if (_isViewActive && _renderTexture)
            {
                _previewCamera.enabled = true;
            }
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
            HideSelectedMemoCard();
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
            cameraObject.layer = PREVIEW_LAYER;

            _previewCamera = cameraObject.AddComponent<Camera>();
            _previewCamera.clearFlags = CameraClearFlags.SolidColor;
            _previewCamera.backgroundColor = new Color(0.675f, 0.675f, 0.675f, 1f);
            _previewCamera.cullingMask = 1 << PREVIEW_LAYER;
            _previewCamera.fieldOfView = 40f;
            _previewCamera.allowHDR = false;
            _previewCamera.allowMSAA = true;
            _previewCamera.enabled = false;

            GameObject lightObject = new("Map Reconstruction Preview Light");
            lightObject.transform.SetParent(transform, false);
            lightObject.transform.localRotation = new Quaternion(0.40821794f, -0.23456973f, 0.10938166f, 0.8754261f);
            lightObject.layer = PREVIEW_LAYER;

            Light previewLight = lightObject.AddComponent<Light>();
            previewLight.type = LightType.Directional;
            previewLight.intensity = 0.6f;
            previewLight.cullingMask = 1 << PREVIEW_LAYER;
            previewLight.shadows = LightShadows.None;
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
                float radius = Mathf.Max(_mesh.bounds.extents.magnitude, 0.1f);
                _distance = Mathf.Clamp(CalculateFitDistance(radius), _minimumDistance, _maximumDistance);
                UpdateCameraTransform();
            }
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
                float pinchDistance = Vector2.Distance(_firstPointerPosition, _secondPointerPosition);
                _distance = Mathf.Clamp(
                    _pinchStartCameraDistance * _pinchStartDistance / Mathf.Max(pinchDistance, 1f),
                    _minimumDistance,
                    _maximumDistance);
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
            _isDragging = _firstPointerId >= 0;
            _previousPointerPosition = _firstPointerPosition;
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
            Vector3 center = _previewSurface.TransformPoint(_mesh.bounds.center);
            Quaternion orbit = Quaternion.Euler(_pitch, _yaw, 0f);
            _previewCamera.transform.position = center - orbit * Vector3.forward * _distance;
            _previewCamera.transform.rotation = Quaternion.LookRotation(center - _previewCamera.transform.position, Vector3.up);

            float radius = Mathf.Max(_mesh.bounds.extents.magnitude, 0.1f);
            _previewCamera.nearClipPlane = Mathf.Max(0.01f, _distance - radius * 2f);
            _previewCamera.farClipPlane = _distance + radius * 4f;
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
            if (_mesh)
            {
                Destroy(_mesh);
            }

            _previewRoot = null;
            _previewSurface = null;
            _material = null;
            _mesh = null;
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
            _target.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            _target.UnregisterCallback<PointerDownEvent>(OnPointerDown);
            _target.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
            _target.UnregisterCallback<PointerUpEvent>(OnPointerUp);
            _target.UnregisterCallback<PointerCancelEvent>(OnPointerCancel);
            _target.UnregisterCallback<WheelEvent>(OnWheel);
            _memoCard.clicked -= OpenSelectedMemoDetail;
            ClearMesh();
            ReleaseRenderTexture();
        }
    }
}
