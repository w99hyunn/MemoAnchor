using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class ARKitMeshScanController : MonoBehaviour
{
    [Header("AR")]
    [SerializeField] private ARSession arSession;
    [SerializeField] private ARMeshManager meshManager;
    [SerializeField] private Camera arCamera;
    [SerializeField] private ARCameraManager arCameraManager;
    [SerializeField] private Material meshMaterial;

    [Header("UI")]
    [SerializeField] private Text sessionStateText;
    [SerializeField] private Text meshStatsText;
    [SerializeField] private Text exportStatusText;
    [SerializeField] private Button resetButton;
    [SerializeField] private Button exportButton;
    [SerializeField] private Button backButton;

    [Header("Scene")]
    [SerializeField] private string fallbackSceneName = "Home";

    [Header("Preview Cleanup")]
    [SerializeField] private float vertexWeldSize = 0.04f;
    [SerializeField] private float minimumTriangleArea = 0.00018f;
    [SerializeField] private int smoothingPasses = 2;
    [SerializeField] private float smoothingStrength = 0.22f;
    [SerializeField] private bool structuralPreviewOnly = true;
    [SerializeField] private bool removeCeilingFaces = true;
    [SerializeField] private float floorNormalYThreshold = 0.5f;
    [SerializeField] private float wallNormalYMax = 0.42f;

    [Header("Preview Controls")]
    [SerializeField] private float rotationSensitivity = 0.18f;
    [SerializeField] private float pinchZoomSensitivity = 0.008f;
    [SerializeField] private float wheelZoomSensitivity = 0.4f;

    [Header("Visual Keyframes")]
    [SerializeField] private int maxKeyframes = 28;
    [SerializeField] private float keyframeIntervalSeconds = 0.8f;
    [SerializeField] private float keyframeMinDistance = 0.35f;
    [SerializeField] private float keyframeMinAngle = 18f;
    [SerializeField] private int keyframeTextureWidth = 256;
    [SerializeField] private float surfacePointMaxDistance = 8f;
    [SerializeField] private LayerMask surfacePointLayers = ~0;
    [SerializeField] private bool showScanPathInPreview = true;
    [SerializeField] private bool showSurfaceColorPointsInPreview = true;
    [SerializeField] private bool showPhotoCardsInPreview = false;

    private enum ScanMode
    {
        Ready,
        Scanning,
        Preview
    }

    private float nextStatsRefreshTime;
    private int meshesAdded;
    private int meshesUpdated;
    private int meshesRemoved;
    private int previewMeshCount;
    private int previewVertexCount;
    private int previewTriangleCount;
    private ScanMode scanMode = ScanMode.Ready;
    private GameObject previewRoot;
    private Camera previewCamera;
    private ARCameraBackground arCameraBackground;
    private Bounds previewBounds;
    private Vector3 previewCenter;
    private float previewDistance = 2f;
    private float previewMinDistance = 0.5f;
    private float previewMaxDistance = 8f;
    private float previewYaw = -35f;
    private float previewPitch = 28f;
    private float previousPinchDistance;
    private bool previewMouseDragging;
    private Vector3 previousMousePosition;
    private Material previewMaterial;
    private Material pathMaterial;
    private readonly List<VisualKeyframe> keyframes = new List<VisualKeyframe>(32);
    private float nextKeyframeCaptureTime;
    private Vector3 lastKeyframePosition;
    private Quaternion lastKeyframeRotation = Quaternion.identity;
    private bool hasLastKeyframePose;

    private void Awake()
    {
        if (!arSession)
            arSession = FindFirstObjectByType<ARSession>();

        if (!meshManager)
            meshManager = FindFirstObjectByType<ARMeshManager>();

        if (!arCamera)
            arCamera = Camera.main;

        if (!arCameraManager)
            arCameraManager = arCamera ? arCamera.GetComponent<ARCameraManager>() : FindFirstObjectByType<ARCameraManager>();

        if (arCamera)
            arCameraBackground = arCamera.GetComponent<ARCameraBackground>();
    }

    private void OnEnable()
    {
        ARSession.stateChanged += OnARSessionStateChanged;

        if (meshManager)
            meshManager.meshesChanged += OnMeshesChanged;

        if (resetButton)
            resetButton.onClick.AddListener(StartScan);

        if (exportButton)
            exportButton.onClick.AddListener(StopScanAndShowMap);

        if (backButton)
            backButton.onClick.AddListener(GoBack);
    }

    private void OnDisable()
    {
        ARSession.stateChanged -= OnARSessionStateChanged;

        if (meshManager)
            meshManager.meshesChanged -= OnMeshesChanged;

        if (resetButton)
            resetButton.onClick.RemoveListener(StartScan);

        if (exportButton)
            exportButton.onClick.RemoveListener(StopScanAndShowMap);

        if (backButton)
            backButton.onClick.RemoveListener(GoBack);
    }

    private void OnDestroy()
    {
        ClearKeyframes();
    }

    private void Start()
    {
        SetButtonLabel(resetButton, "Scan");
        SetButtonLabel(exportButton, "Stop");
        SetExportStatus("Ready. Tap Scan to start ARKit mesh mapping.");
        SetScanningEnabled(false);
        SetLiveMeshesVisible(false);
        EnsurePreviewCamera();
        ApplyMaterialToExistingMeshes();
        UpdateSessionState(ARSession.state);
        UpdateButtonStates();
        UpdateStats(force: true);
    }

    private void Update()
    {
        if (Time.unscaledTime >= nextStatsRefreshTime)
        {
            nextStatsRefreshTime = Time.unscaledTime + 0.25f;
            UpdateStats(force: false);
        }

        if (scanMode == ScanMode.Preview)
            HandlePreviewInput();
        else if (scanMode == ScanMode.Scanning)
            TryCaptureVisualKeyframe();
    }

    private void OnARSessionStateChanged(ARSessionStateChangedEventArgs args)
    {
        UpdateSessionState(args.state);
    }

    private void OnMeshesChanged(ARMeshesChangedEventArgs args)
    {
        meshesAdded += args.added?.Count ?? 0;
        meshesUpdated += args.updated?.Count ?? 0;
        meshesRemoved += args.removed?.Count ?? 0;

        ApplyMaterial(args.added);
        ApplyMaterial(args.updated);
        UpdateStats(force: true);
    }

    private void ApplyMaterialToExistingMeshes()
    {
        if (!meshManager)
            return;

        ApplyMaterial(meshManager.meshes);
    }

    private void ApplyMaterial(System.Collections.Generic.IEnumerable<MeshFilter> meshFilters)
    {
        if (meshFilters == null)
            return;

        foreach (var meshFilter in meshFilters)
        {
            if (!meshFilter)
                continue;

            var meshRenderer = meshFilter.GetComponent<MeshRenderer>();
            if (meshRenderer && meshMaterial)
                meshRenderer.sharedMaterial = meshMaterial;
        }
    }

    public void StartScan()
    {
        scanMode = ScanMode.Scanning;
        meshesAdded = 0;
        meshesUpdated = 0;
        meshesRemoved = 0;
        previewMeshCount = 0;
        previewVertexCount = 0;
        previewTriangleCount = 0;
        ClearKeyframes();
        nextKeyframeCaptureTime = 0f;
        hasLastKeyframePose = false;

        DestroyPreview();

        if (meshManager)
        {
            meshManager.gameObject.SetActive(true);
            meshManager.enabled = true;
            meshManager.DestroyAllMeshes();
        }

        if (arSession)
        {
            arSession.enabled = true;
            arSession.Reset();
        }

        if (arCamera)
            arCamera.enabled = true;

        if (arCameraBackground)
            arCameraBackground.enabled = true;

        if (previewCamera)
            previewCamera.enabled = false;

        previewMouseDragging = false;
        previousPinchDistance = 0f;

        SetExportStatus("Scanning... move slowly around the space.");
        UpdateSessionState(ARSession.state);
        UpdateButtonStates();
        UpdateStats(force: true);
    }

    public void StopScanAndShowMap()
    {
        if (!meshManager)
        {
            SetExportStatus("Stop failed: ARMeshManager missing");
            return;
        }

        if (scanMode != ScanMode.Scanning)
            return;

        var bounds = BuildPreviewFromLiveMeshes();
        if (!bounds.HasValue)
        {
            SetExportStatus("No map data yet. Move the phone to scan first.");
            UpdateButtonStates();
            return;
        }

        scanMode = ScanMode.Preview;
        SetScanningEnabled(false);
        SetLiveMeshesVisible(false);
        ShowPreviewCamera(bounds.Value);

        BuildVisualKeyframePreview(bounds.Value);

        SetExportStatus("Scan stopped. Showing clean structural map.");
        UpdateSessionState(ARSession.state);
        UpdateButtonStates();
        UpdateStats(force: true);
    }

    public void ResetScan()
    {
        StartScan();
    }

    public void ExportCurrentMeshToObj()
    {
        if (!meshManager)
        {
            SetExportStatus("Export failed: ARMeshManager missing");
            return;
        }

        var meshes = meshManager.meshes;
        if (meshes == null || meshes.Count == 0)
        {
            SetExportStatus("Export skipped: no mesh yet");
            return;
        }

        var fileName = $"ARKitMeshScan_{DateTime.Now:yyyyMMdd_HHmmss}.obj";
        var filePath = Path.Combine(Application.persistentDataPath, fileName);

        try
        {
            WriteObj(filePath, meshes);
            SetExportStatus($"Exported: {fileName}");
            Debug.Log($"[ARKitMeshScanController] Exported mesh OBJ: {filePath}");
        }
        catch (Exception ex)
        {
            SetExportStatus("Export failed. See console.");
            Debug.LogException(ex);
        }
    }

    private void WriteObj(string filePath, System.Collections.Generic.IList<MeshFilter> meshFilters)
    {
        var builder = new StringBuilder(1024 * 128);
        builder.AppendLine("# MemoAnchor ARKit mesh scan export");

        var vertexOffset = 0;
        var normalOffset = 0;

        foreach (var meshFilter in meshFilters)
        {
            if (!meshFilter || !meshFilter.sharedMesh)
                continue;

            var mesh = meshFilter.sharedMesh;
            builder.AppendLine($"o {meshFilter.name}");

            foreach (var vertex in mesh.vertices)
            {
                var world = meshFilter.transform.TransformPoint(vertex);
                builder.Append("v ");
                builder.Append(world.x.ToString("F6", CultureInfo.InvariantCulture)).Append(' ');
                builder.Append(world.y.ToString("F6", CultureInfo.InvariantCulture)).Append(' ');
                builder.Append(world.z.ToString("F6", CultureInfo.InvariantCulture)).AppendLine();
            }

            foreach (var normal in mesh.normals)
            {
                var worldNormal = meshFilter.transform.TransformDirection(normal).normalized;
                builder.Append("vn ");
                builder.Append(worldNormal.x.ToString("F6", CultureInfo.InvariantCulture)).Append(' ');
                builder.Append(worldNormal.y.ToString("F6", CultureInfo.InvariantCulture)).Append(' ');
                builder.Append(worldNormal.z.ToString("F6", CultureInfo.InvariantCulture)).AppendLine();
            }

            var triangles = mesh.triangles;
            var hasNormals = mesh.normals != null && mesh.normals.Length == mesh.vertexCount;

            for (var i = 0; i < triangles.Length; i += 3)
            {
                var a = triangles[i] + 1 + vertexOffset;
                var b = triangles[i + 1] + 1 + vertexOffset;
                var c = triangles[i + 2] + 1 + vertexOffset;

                if (hasNormals)
                {
                    var na = triangles[i] + 1 + normalOffset;
                    var nb = triangles[i + 1] + 1 + normalOffset;
                    var nc = triangles[i + 2] + 1 + normalOffset;
                    builder.AppendLine($"f {a}//{na} {b}//{nb} {c}//{nc}");
                }
                else
                {
                    builder.AppendLine($"f {a} {b} {c}");
                }
            }

            vertexOffset += mesh.vertexCount;
            if (hasNormals)
                normalOffset += mesh.normals.Length;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(filePath));
        File.WriteAllText(filePath, builder.ToString());
    }

    private void GoBack()
    {
        var targetScene = fallbackSceneName;

        try
        {
            targetScene = global::SceneHistoryManager.GetPreviousScene(fallbackSceneName);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ARKitMeshScanController] Could not read previous scene. Using fallback. {ex.Message}");
        }

        if (string.IsNullOrWhiteSpace(targetScene))
            targetScene = fallbackSceneName;

        if (!Application.CanStreamedLevelBeLoaded(targetScene))
        {
            Debug.LogWarning($"[ARKitMeshScanController] Scene '{targetScene}' is not in Build Settings.");
            return;
        }

        SceneManager.LoadScene(targetScene);
    }

    private void UpdateSessionState(ARSessionState state)
    {
        if (sessionStateText)
            sessionStateText.text = $"Mode: {scanMode}\nAR Session: {state}";
    }

    private void UpdateStats(bool force)
    {
        if (!meshStatsText)
            return;

        var meshCount = 0;
        var vertexCount = 0;
        var triangleCount = 0;

        if (scanMode == ScanMode.Preview)
        {
            meshCount = previewMeshCount;
            vertexCount = previewVertexCount;
            triangleCount = previewTriangleCount;
        }
        else if (meshManager)
        {
            foreach (var meshFilter in meshManager.meshes)
            {
                if (!meshFilter || !meshFilter.sharedMesh)
                    continue;

                meshCount++;
                vertexCount += meshFilter.sharedMesh.vertexCount;
                triangleCount += meshFilter.sharedMesh.triangles.Length / 3;
            }
        }

        meshStatsText.text =
            $"Mode: {scanMode}\n" +
            $"Meshes: {meshCount}\n" +
            $"Vertices: {vertexCount:N0}\n" +
            $"Triangles: {triangleCount:N0}\n" +
            $"Keyframes: {keyframes.Count}\n" +
            $"Changed: +{meshesAdded} / ~{meshesUpdated} / -{meshesRemoved}";
    }

    private void SetExportStatus(string message)
    {
        if (exportStatusText)
            exportStatusText.text = message;
    }

    private Bounds? BuildPreviewFromLiveMeshes()
    {
        DestroyPreview();

        previewRoot = new GameObject("Scanned Map Preview");
        previewMeshCount = 0;
        previewVertexCount = 0;
        previewTriangleCount = 0;

        var vertices = new List<Vector3>(8192);
        var triangles = new List<int>(16384);
        var vertexMap = new Dictionary<GridKey, int>(8192);
        var liveMeshCount = 0;

        foreach (var sourceFilter in meshManager.meshes)
        {
            if (!sourceFilter || !sourceFilter.sharedMesh)
                continue;

            var sourceMesh = sourceFilter.sharedMesh;
            if (sourceMesh.vertexCount == 0 || sourceMesh.triangles.Length == 0)
                continue;

            liveMeshCount++;
            AppendCleanTriangles(sourceFilter, vertices, triangles, vertexMap);
        }

        if (vertices.Count == 0 || triangles.Count == 0)
        {
            DestroyPreview();
            return null;
        }

        SmoothVertices(vertices, triangles);

        var previewMesh = new Mesh
        {
            name = "Cleaned_ARKit_Map",
            indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16
        };

        previewMesh.SetVertices(vertices);
        previewMesh.SetTriangles(triangles, 0);
        previewMesh.RecalculateNormals();
        previewMesh.RecalculateBounds();

        var previewGo = new GameObject("Clean Map Surface");
        previewGo.transform.SetParent(previewRoot.transform, false);
        previewGo.AddComponent<MeshFilter>().sharedMesh = previewMesh;

        var renderer = previewGo.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = GetPreviewMaterial();

        previewMeshCount = liveMeshCount;
        previewVertexCount = previewMesh.vertexCount;
        previewTriangleCount = previewMesh.triangles.Length / 3;

        return previewMesh.bounds;
    }

    private void TryCaptureVisualKeyframe()
    {
        if (!arCamera || !arCameraManager || !arCameraManager.enabled)
            return;

        if (Time.unscaledTime < nextKeyframeCaptureTime)
            return;

        var cameraTransform = arCamera.transform;
        var position = cameraTransform.position;
        var rotation = cameraTransform.rotation;

        if (hasLastKeyframePose)
        {
            var distance = Vector3.Distance(position, lastKeyframePosition);
            var angle = Quaternion.Angle(rotation, lastKeyframeRotation);
            if (distance < keyframeMinDistance && angle < keyframeMinAngle)
                return;
        }

        if (!TryCreateCameraTexture(out var texture))
            return;

        TrimOldestKeyframeIfNeeded();

        var keyframe = new VisualKeyframe
        {
            Texture = texture,
            Position = position,
            Rotation = rotation,
            Timestamp = Time.time,
            SampleColor = SampleTextureCenter(texture)
        };

        if (TryGetCenterSurfacePoint(out var surfacePoint))
        {
            keyframe.HasSurfacePoint = true;
            keyframe.SurfacePoint = surfacePoint;
        }

        keyframes.Add(keyframe);
        lastKeyframePosition = position;
        lastKeyframeRotation = rotation;
        hasLastKeyframePose = true;
        nextKeyframeCaptureTime = Time.unscaledTime + keyframeIntervalSeconds;

        SetExportStatus($"Scanning... visual keyframes: {keyframes.Count}");
    }

    private bool TryCreateCameraTexture(out Texture2D texture)
    {
        texture = null;

        if (!arCameraManager.TryAcquireLatestCpuImage(out var image))
            return false;

        try
        {
            var targetWidth = Mathf.Clamp(keyframeTextureWidth, 64, 512);
            var aspect = image.height / (float)image.width;
            var targetHeight = Mathf.Max(64, Mathf.RoundToInt(targetWidth * aspect));
            var conversionParams = new XRCpuImage.ConversionParams
            {
                inputRect = new RectInt(0, 0, image.width, image.height),
                outputDimensions = new Vector2Int(targetWidth, targetHeight),
                outputFormat = TextureFormat.RGBA32,
                transformation = XRCpuImage.Transformation.MirrorY
            };

            texture = new Texture2D(targetWidth, targetHeight, TextureFormat.RGBA32, false)
            {
                name = $"ARKit_Keyframe_{keyframes.Count + 1:D2}",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            var rawTextureData = texture.GetRawTextureData<byte>();
            image.Convert(conversionParams, rawTextureData);
            texture.Apply(false, false);
            return true;
        }
        catch (Exception ex)
        {
            if (texture)
                Destroy(texture);

            Debug.LogWarning($"[ARKitMeshScanController] Keyframe capture failed: {ex.Message}");
            texture = null;
            return false;
        }
        finally
        {
            image.Dispose();
        }
    }

    private bool TryGetCenterSurfacePoint(out Vector3 surfacePoint)
    {
        surfacePoint = default;

        if (!arCamera)
            return false;

        var ray = arCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (!Physics.Raycast(ray, out var hit, surfacePointMaxDistance, surfacePointLayers, QueryTriggerInteraction.Ignore))
            return false;

        surfacePoint = hit.point;
        return true;
    }

    private static Color SampleTextureCenter(Texture2D texture)
    {
        if (!texture)
            return Color.white;

        return texture.GetPixel(texture.width / 2, texture.height / 2);
    }

    private void TrimOldestKeyframeIfNeeded()
    {
        while (keyframes.Count >= Mathf.Max(1, maxKeyframes))
        {
            var old = keyframes[0];
            keyframes.RemoveAt(0);
            if (old.Texture)
                Destroy(old.Texture);
        }
    }

    private void BuildVisualKeyframePreview(Bounds bounds)
    {
        if (!previewRoot || keyframes.Count == 0)
            return;

        var visualRoot = new GameObject("Visual Keyframe Overlay");
        visualRoot.transform.SetParent(previewRoot.transform, false);

        if (showScanPathInPreview)
            BuildScanPath(visualRoot.transform);

        if (showSurfaceColorPointsInPreview)
            BuildSurfacePoints(visualRoot.transform, bounds);

        if (showPhotoCardsInPreview)
            BuildThumbnailCards(visualRoot.transform, bounds);
    }

    private void BuildScanPath(Transform parent)
    {
        if (keyframes.Count < 2)
            return;

        var pathGo = new GameObject("Camera Scan Path");
        pathGo.transform.SetParent(parent, false);
        var line = pathGo.AddComponent<LineRenderer>();
        line.positionCount = keyframes.Count;
        line.useWorldSpace = true;
        line.widthMultiplier = 0.018f;
        line.numCornerVertices = 3;
        line.numCapVertices = 3;
        line.sharedMaterial = GetPathMaterial();

        for (var i = 0; i < keyframes.Count; i++)
            line.SetPosition(i, keyframes[i].Position);
    }

    private void BuildSurfacePoints(Transform parent, Bounds bounds)
    {
        var radius = Mathf.Clamp(bounds.extents.magnitude * 0.018f, 0.018f, 0.055f);

        foreach (var keyframe in keyframes)
        {
            if (!keyframe.HasSurfacePoint)
                continue;

            var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = "Visual Surface Point";
            marker.transform.SetParent(parent, false);
            marker.transform.position = keyframe.SurfacePoint;
            marker.transform.localScale = Vector3.one * radius;

            var collider = marker.GetComponent<Collider>();
            if (collider)
                Destroy(collider);

            var renderer = marker.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = CreatePointMaterial(keyframe.SampleColor);
        }
    }

    private void BuildThumbnailCards(Transform parent, Bounds bounds)
    {
        var radius = Mathf.Max(bounds.extents.magnitude, 0.75f);
        var cardHeight = Mathf.Clamp(radius * 0.18f, 0.12f, 0.36f);

        foreach (var keyframe in keyframes)
        {
            if (!keyframe.Texture)
                continue;

            var card = GameObject.CreatePrimitive(PrimitiveType.Quad);
            card.name = "Visual Keyframe Card";
            card.transform.SetParent(parent, false);
            card.transform.position = GetPreviewCardPosition(keyframe, bounds, radius);

            var aspect = keyframe.Texture.width / (float)keyframe.Texture.height;
            card.transform.localScale = new Vector3(cardHeight * aspect, cardHeight, 1f);
            card.transform.rotation = Quaternion.LookRotation(previewCenter - card.transform.position, Vector3.up);

            var collider = card.GetComponent<Collider>();
            if (collider)
                Destroy(collider);

            var renderer = card.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = CreateKeyframeMaterial(keyframe.Texture);
        }
    }

    private Vector3 GetPreviewCardPosition(VisualKeyframe keyframe, Bounds bounds, float radius)
    {
        if (keyframe.HasSurfacePoint)
        {
            var direction = (keyframe.Position - keyframe.SurfacePoint).normalized;
            if (direction.sqrMagnitude < 0.01f)
                direction = (keyframe.SurfacePoint - bounds.center).normalized;

            return keyframe.SurfacePoint + direction * Mathf.Clamp(radius * 0.16f, 0.16f, 0.5f);
        }

        var fromCenter = keyframe.Position - bounds.center;
        if (fromCenter.sqrMagnitude < 0.01f)
            fromCenter = Vector3.up;

        return bounds.center + Vector3.ClampMagnitude(fromCenter, radius * 1.2f);
    }

    private Material CreateKeyframeMaterial(Texture2D texture)
    {
        var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Texture") ?? Shader.Find("Sprites/Default");
        var material = new Material(shader)
        {
            name = $"Runtime Keyframe Material {texture.name}",
            mainTexture = texture
        };

        if (material.HasProperty("_BaseMap"))
            material.SetTexture("_BaseMap", texture);
        if (material.HasProperty("_MainTex"))
            material.SetTexture("_MainTex", texture);
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", Color.white);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", Color.white);
        if (material.HasProperty("_Cull"))
            material.SetFloat("_Cull", (float)CullMode.Off);

        return material;
    }

    private Material GetPathMaterial()
    {
        if (pathMaterial)
            return pathMaterial;

        var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default");
        pathMaterial = new Material(shader)
        {
            name = "Runtime Scan Path Material"
        };

        var color = new Color(1f, 0.78f, 0.22f, 1f);
        if (pathMaterial.HasProperty("_BaseColor"))
            pathMaterial.SetColor("_BaseColor", color);
        if (pathMaterial.HasProperty("_Color"))
            pathMaterial.SetColor("_Color", color);

        return pathMaterial;
    }

    private Material CreatePointMaterial(Color color)
    {
        var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default");
        var material = new Material(shader)
        {
            name = "Runtime Visual Point Material"
        };

        color.a = 1f;
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);

        return material;
    }

    private void ShowPreviewCamera(Bounds bounds)
    {
        EnsurePreviewCamera();
        previewBounds = bounds;
        previewCenter = bounds.center;

        if (arCamera)
            arCamera.enabled = false;

        if (arCameraBackground)
            arCameraBackground.enabled = false;

        if (previewCamera)
        {
            var radius = Mathf.Max(bounds.extents.magnitude, 0.75f);
            previewDistance = radius / Mathf.Tan(previewCamera.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.45f;
            previewMinDistance = Mathf.Max(radius * 0.35f, 0.2f);
            previewMaxDistance = Mathf.Max(radius * 5f, previewDistance * 2f);
            previewYaw = -35f;
            previewPitch = 28f;
            previewCamera.nearClipPlane = 0.01f;
            previewCamera.farClipPlane = Mathf.Max(30f, previewMaxDistance * 3f);
            previewCamera.enabled = true;
            UpdatePreviewCameraTransform();
        }
    }

    private void EnsurePreviewCamera()
    {
        if (previewCamera)
            return;

        var cameraGo = new GameObject("Scanned Map Preview Camera");
        previewCamera = cameraGo.AddComponent<Camera>();
        previewCamera.clearFlags = CameraClearFlags.SolidColor;
        previewCamera.backgroundColor = new Color(0.02f, 0.025f, 0.03f, 1f);
        previewCamera.fieldOfView = 55f;
        previewCamera.enabled = false;
    }

    private Material GetPreviewMaterial()
    {
        if (previewMaterial)
            return previewMaterial;

        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Unlit");
        previewMaterial = new Material(shader)
        {
            name = "Runtime Clean Map Preview Material"
        };

        var color = new Color(0.58f, 0.7f, 0.74f, 1f);
        if (previewMaterial.HasProperty("_BaseColor"))
            previewMaterial.SetColor("_BaseColor", color);
        if (previewMaterial.HasProperty("_Color"))
            previewMaterial.SetColor("_Color", color);
        if (previewMaterial.HasProperty("_Smoothness"))
            previewMaterial.SetFloat("_Smoothness", 0.2f);
        if (previewMaterial.HasProperty("_Metallic"))
            previewMaterial.SetFloat("_Metallic", 0f);
        if (previewMaterial.HasProperty("_Cull"))
            previewMaterial.SetFloat("_Cull", (float)CullMode.Off);

        return previewMaterial;
    }

    private void AppendCleanTriangles(
        MeshFilter sourceFilter,
        List<Vector3> vertices,
        List<int> triangles,
        Dictionary<GridKey, int> vertexMap)
    {
        var sourceMesh = sourceFilter.sharedMesh;
        var sourceVertices = sourceMesh.vertices;
        var sourceTriangles = sourceMesh.triangles;

        for (var i = 0; i < sourceTriangles.Length; i += 3)
        {
            var a = sourceFilter.transform.TransformPoint(sourceVertices[sourceTriangles[i]]);
            var b = sourceFilter.transform.TransformPoint(sourceVertices[sourceTriangles[i + 1]]);
            var c = sourceFilter.transform.TransformPoint(sourceVertices[sourceTriangles[i + 2]]);

            if (TriangleArea(a, b, c) < minimumTriangleArea)
                continue;

            if (!ShouldKeepPreviewTriangle(a, b, c))
                continue;

            var ia = AddWeldedVertex(a, vertices, vertexMap);
            var ib = AddWeldedVertex(b, vertices, vertexMap);
            var ic = AddWeldedVertex(c, vertices, vertexMap);

            if (ia == ib || ib == ic || ic == ia)
                continue;

            triangles.Add(ia);
            triangles.Add(ib);
            triangles.Add(ic);
        }
    }

    private bool ShouldKeepPreviewTriangle(Vector3 a, Vector3 b, Vector3 c)
    {
        var normal = Vector3.Cross(b - a, c - a).normalized;
        var verticality = Mathf.Abs(normal.y);

        if (removeCeilingFaces && verticality >= floorNormalYThreshold && arCamera)
        {
            var centroidY = (a.y + b.y + c.y) / 3f;
            if (centroidY > arCamera.transform.position.y + 0.4f)
                return false;
        }

        if (!structuralPreviewOnly)
            return true;

        var looksLikeFloor = verticality >= floorNormalYThreshold;
        var looksLikeWall = verticality <= wallNormalYMax;
        return looksLikeFloor || looksLikeWall;
    }

    private int AddWeldedVertex(Vector3 vertex, List<Vector3> vertices, Dictionary<GridKey, int> vertexMap)
    {
        var key = new GridKey(vertex, Mathf.Max(vertexWeldSize, 0.001f));
        if (vertexMap.TryGetValue(key, out var index))
            return index;

        index = vertices.Count;
        vertices.Add(vertex);
        vertexMap.Add(key, index);
        return index;
    }

    private void SmoothVertices(List<Vector3> vertices, List<int> triangles)
    {
        var passes = Mathf.Clamp(smoothingPasses, 0, 3);
        if (passes == 0 || vertices.Count == 0 || triangles.Count == 0)
            return;

        var strength = Mathf.Clamp01(smoothingStrength);
        for (var pass = 0; pass < passes; pass++)
        {
            var sums = new Vector3[vertices.Count];
            var counts = new int[vertices.Count];

            for (var i = 0; i < triangles.Count; i += 3)
            {
                var a = triangles[i];
                var b = triangles[i + 1];
                var c = triangles[i + 2];

                AddNeighbor(a, b, vertices, sums, counts);
                AddNeighbor(a, c, vertices, sums, counts);
                AddNeighbor(b, a, vertices, sums, counts);
                AddNeighbor(b, c, vertices, sums, counts);
                AddNeighbor(c, a, vertices, sums, counts);
                AddNeighbor(c, b, vertices, sums, counts);
            }

            for (var i = 0; i < vertices.Count; i++)
            {
                if (counts[i] == 0)
                    continue;

                vertices[i] = Vector3.Lerp(vertices[i], sums[i] / counts[i], strength);
            }
        }
    }

    private static void AddNeighbor(int vertexIndex, int neighborIndex, List<Vector3> vertices, Vector3[] sums, int[] counts)
    {
        sums[vertexIndex] += vertices[neighborIndex];
        counts[vertexIndex]++;
    }

    private static float TriangleArea(Vector3 a, Vector3 b, Vector3 c)
    {
        return Vector3.Cross(b - a, c - a).magnitude * 0.5f;
    }

    private void HandlePreviewInput()
    {
        HandleTouchPreviewInput();
        HandleMousePreviewInput();
    }

    private void HandleTouchPreviewInput()
    {
        if (Input.touchCount == 0)
        {
            previousPinchDistance = 0f;
            return;
        }

        if (Input.touchCount == 1)
        {
            var touch = Input.GetTouch(0);
            if (IsTouchOverUi(touch))
                return;

            if (touch.phase == TouchPhase.Moved)
            {
                previewYaw += touch.deltaPosition.x * rotationSensitivity;
                previewPitch -= touch.deltaPosition.y * rotationSensitivity;
                previewPitch = Mathf.Clamp(previewPitch, -80f, 80f);
                UpdatePreviewCameraTransform();
            }

            previousPinchDistance = 0f;
            return;
        }

        var firstTouch = Input.GetTouch(0);
        var secondTouch = Input.GetTouch(1);
        if (IsTouchOverUi(firstTouch) || IsTouchOverUi(secondTouch))
            return;

        var pinchDistance = Vector2.Distance(firstTouch.position, secondTouch.position);
        if (previousPinchDistance > 0f)
        {
            var delta = pinchDistance - previousPinchDistance;
            previewDistance = Mathf.Clamp(previewDistance - delta * pinchZoomSensitivity, previewMinDistance, previewMaxDistance);
            UpdatePreviewCameraTransform();
        }

        previousPinchDistance = pinchDistance;
    }

    private void HandleMousePreviewInput()
    {
        if (Input.touchCount > 0)
            return;

        if (Input.GetMouseButtonDown(0))
        {
            previewMouseDragging = !IsPointerOverUi();
            previousMousePosition = Input.mousePosition;
        }
        else if (Input.GetMouseButtonUp(0))
        {
            previewMouseDragging = false;
        }

        if (previewMouseDragging && Input.GetMouseButton(0))
        {
            var delta = Input.mousePosition - previousMousePosition;
            previewYaw += delta.x * rotationSensitivity;
            previewPitch -= delta.y * rotationSensitivity;
            previewPitch = Mathf.Clamp(previewPitch, -80f, 80f);
            previousMousePosition = Input.mousePosition;
            UpdatePreviewCameraTransform();
        }

        var scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.01f && !IsPointerOverUi())
        {
            previewDistance = Mathf.Clamp(previewDistance - scroll * wheelZoomSensitivity, previewMinDistance, previewMaxDistance);
            UpdatePreviewCameraTransform();
        }
    }

    private void UpdatePreviewCameraTransform()
    {
        if (!previewCamera)
            return;

        var rotation = Quaternion.Euler(previewPitch, previewYaw, 0f);
        previewCamera.transform.position = previewCenter + rotation * new Vector3(0f, 0f, -previewDistance);
        previewCamera.transform.rotation = Quaternion.LookRotation(previewCenter - previewCamera.transform.position, Vector3.up);
    }

    private static bool IsPointerOverUi()
    {
        return EventSystem.current && EventSystem.current.IsPointerOverGameObject();
    }

    private static bool IsTouchOverUi(Touch touch)
    {
        return EventSystem.current && EventSystem.current.IsPointerOverGameObject(touch.fingerId);
    }

    private void SetScanningEnabled(bool enabled)
    {
        if (meshManager)
            meshManager.enabled = enabled;

        if (arSession)
            arSession.enabled = enabled;
    }

    private void SetLiveMeshesVisible(bool visible)
    {
        if (!meshManager)
            return;

        foreach (var meshFilter in meshManager.meshes)
        {
            if (!meshFilter)
                continue;

            var renderer = meshFilter.GetComponent<MeshRenderer>();
            if (renderer)
                renderer.enabled = visible;
        }
    }

    private void DestroyPreview()
    {
        if (previewRoot)
            Destroy(previewRoot);

        previewRoot = null;
    }

    private void ClearKeyframes()
    {
        foreach (var keyframe in keyframes)
        {
            if (keyframe.Texture)
                Destroy(keyframe.Texture);
        }

        keyframes.Clear();
    }

    private sealed class VisualKeyframe
    {
        public Texture2D Texture;
        public Vector3 Position;
        public Quaternion Rotation;
        public float Timestamp;
        public Color SampleColor = Color.white;
        public bool HasSurfacePoint;
        public Vector3 SurfacePoint;
    }

    private readonly struct GridKey : IEquatable<GridKey>
    {
        private readonly int x;
        private readonly int y;
        private readonly int z;

        public GridKey(Vector3 position, float cellSize)
        {
            x = Mathf.RoundToInt(position.x / cellSize);
            y = Mathf.RoundToInt(position.y / cellSize);
            z = Mathf.RoundToInt(position.z / cellSize);
        }

        public bool Equals(GridKey other)
        {
            return x == other.x && y == other.y && z == other.z;
        }

        public override bool Equals(object obj)
        {
            return obj is GridKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + x;
                hash = hash * 31 + y;
                hash = hash * 31 + z;
                return hash;
            }
        }
    }

    private void UpdateButtonStates()
    {
        if (resetButton)
            resetButton.interactable = scanMode != ScanMode.Scanning;

        if (exportButton)
            exportButton.interactable = scanMode == ScanMode.Scanning;
    }

    private void SetButtonLabel(Button button, string label)
    {
        if (!button)
            return;

        var labelText = button.GetComponentInChildren<Text>();
        if (labelText)
            labelText.text = label;
    }
}
