using UnityEngine;
using UnityEngine.UIElements;

namespace MemoAnchor.UI
{
    public sealed class MapReconstructionPreviewRenderer : MonoBehaviour
    {
        private const int PREVIEW_LAYER = 31;
        private const int MAX_RENDER_TEXTURE_SIZE = 2048;

        private Image _target;
        private Camera _previewCamera;
        private RenderTexture _renderTexture;
        private GameObject _previewRoot;
        private Mesh _mesh;
        private Material _material;
        private Vector3 _previousPointerPosition;
        private float _yaw = -35f;
        private float _pitch = 28f;
        private float _distance;
        private float _minimumDistance;
        private float _maximumDistance;
        private bool _isDragging;
        private bool _isViewActive;

        public void Initialize(Image target)
        {
            _target = target;
            _target.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            _target.RegisterCallback<PointerDownEvent>(OnPointerDown);
            _target.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            _target.RegisterCallback<PointerUpEvent>(OnPointerUp);
            _target.RegisterCallback<PointerCancelEvent>(OnPointerCancel);
            _target.RegisterCallback<WheelEvent>(OnWheel);

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
            surface.layer = PREVIEW_LAYER;
            surface.AddComponent<MeshFilter>().sharedMesh = _mesh;

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
            return radius / Mathf.Tan(fitFov * 0.5f) * 1.15f;
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (!_mesh || evt.button != 0)
            {
                return;
            }

            _isDragging = true;
            _previousPointerPosition = evt.position;
            _target.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (!_isDragging || !_target.HasPointerCapture(evt.pointerId))
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
            EndPointerDrag(evt.pointerId);
        }

        private void OnPointerCancel(PointerCancelEvent evt)
        {
            EndPointerDrag(evt.pointerId);
        }

        private void EndPointerDrag(int pointerId)
        {
            if (_target.HasPointerCapture(pointerId))
            {
                _target.ReleasePointer(pointerId);
            }
            _isDragging = false;
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
            Vector3 center = _previewRoot.transform.TransformPoint(_mesh.bounds.center);
            Quaternion orbit = Quaternion.Euler(_pitch, _yaw, 0f);
            _previewCamera.transform.position = center - orbit * Vector3.forward * _distance;
            _previewCamera.transform.rotation = Quaternion.LookRotation(center - _previewCamera.transform.position, Vector3.up);

            float radius = Mathf.Max(_mesh.bounds.extents.magnitude, 0.1f);
            _previewCamera.nearClipPlane = Mathf.Max(0.01f, _distance - radius * 2f);
            _previewCamera.farClipPlane = _distance + radius * 4f;
        }

        private void ClearMesh()
        {
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
            _material = null;
            _mesh = null;
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
            ClearMesh();
            ReleaseRenderTexture();
        }
    }
}
