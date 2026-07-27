using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO.Compression;
using System.IO;
using System.Text;
using MemoAnchor;
using Unity.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class ARKitMeshScanController : MonoBehaviour
{
    private const int RECONSTRUCTION_REQUEST_TIMEOUT_SECONDS = 900;
    private const int RECONSTRUCTION_STATUS_POLL_ATTEMPTS = 300;

    [Header("AR")]
    [SerializeField] private ARSession arSession;
    [SerializeField] private ARMeshManager meshManager;
    [SerializeField] private ARPlaneManager planeManager;
    [SerializeField] private Camera arCamera;
    [SerializeField] private ARCameraManager arCameraManager;
    [SerializeField] private AROcclusionManager arOcclusionManager;
    [SerializeField] private Material meshMaterial;

    [Header("UI")]
    [SerializeField] private Text sessionStateText;
    [SerializeField] private Text meshStatsText;
    [SerializeField] private Text exportStatusText;
    [SerializeField] private Button resetButton;
    [SerializeField] private Button exportButton;
    [SerializeField] private Button backButton;

    [Header("Scene")]
    [SerializeField] private string fallbackSceneName = "Main";

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
    [SerializeField] private int maxKeyframes = 160;
    [SerializeField] private float keyframeIntervalSeconds = 0.45f;
    [SerializeField] private float keyframeMinDistance = 0.16f;
    [SerializeField] private float keyframeMinAngle = 7f;
    [SerializeField] private int keyframeTextureWidth = 1440;
    [SerializeField] private float surfacePointMaxDistance = 8f;
    [SerializeField] private LayerMask surfacePointLayers = ~0;
    [SerializeField] private bool colorizePreviewFromKeyframes = false;
    [SerializeField] private bool showScanPathInPreview = true;
    [SerializeField] private bool showSurfaceColorPointsInPreview = false;
    [SerializeField] private bool showPhotoCardsInPreview = false;

    [Header("Reconstruction Capture")]
    [SerializeField] private bool captureDepthForReconstruction = true;
    [SerializeField] private bool captureDetectedPlanesForReconstruction = true;
    [SerializeField] private bool packageReconstructionScanOnStop = true;
    [SerializeField] private bool uploadReconstructionPackageOnStop = false;
    [SerializeField] private string reconstructionUploadUrl = "";

    [Header("RGB-D Recorder")]
    [SerializeField] private bool recordRgbdDatasetOnScan = true;
    [SerializeField] private float rgbdRecorderFrameIntervalSeconds = 0.2f;
    [SerializeField] private int rgbdRecorderMaxQueue = 4;

    [Header("Scan Guidance")]
    [SerializeField] private bool showScanQualityGuidance = true;
    [SerializeField] private bool requireMinimumQualityToStop = true;
    [SerializeField] private float minimumStopQualityScore = 78f;
    [SerializeField] private float recommendedMinScanSeconds = 75f;
    [SerializeField] private int recommendedMinKeyframes = 90;
    [SerializeField] private float recommendedMinCameraPathMeters = 3f;
    [SerializeField] private int recommendedMinMeshTriangles = 75000;
    [SerializeField] private int recommendedMinDetectedPlanes = 2;
    [SerializeField] private float recommendedMinDepthConfidenceRatio = 0.65f;
    [SerializeField] private float recommendedMinSurfaceHitRatio = 0.7f;
    [SerializeField] private float maxKeyframeCaptureSpeed = 0.55f;
    [SerializeField] private float maxKeyframeAngularSpeed = 38f;
    [SerializeField] private float minimumKeyframeDepthConfidenceRatio = 0.55f;
    [SerializeField] private bool requireSynchronizedRgbdKeyframes = true;
    [SerializeField] private float maxRgbDepthTimestampDeltaSeconds = 0.033f;
    [SerializeField] private float maxRgbConfidenceTimestampDeltaSeconds = 0.033f;

    [Header("Scan Coverage Overlay")]
    [SerializeField] private bool showCoverageOverlayWhileScanning = true;
    [SerializeField] private bool showCoverageOverlayInPreview = true;
    [SerializeField] private float coverageCellSize = 0.32f;
    [SerializeField] private int fairCoverageViewCount = 2;
    [SerializeField] private int goodCoverageViewCount = 3;
    [SerializeField] private int maxCoverageMarkers = 220;
    [SerializeField] private float coverageOverlayRefreshSeconds = 0.8f;
    [SerializeField] private float coverageMarkerSurfaceOffset = 0.018f;

    [Header("Gemini Semantic Map")]
    [SerializeField] private bool enhancePreviewWithGemini;
    [SerializeField] private GeminiSemanticMapClient geminiClient;
    [SerializeField] private int geminiMaxImages = 4;
    [SerializeField] private int geminiMeshSampleLimit = 180;
    [SerializeField] private float geminiMinimumConfidence = 0.35f;
    [SerializeField] private bool showGeminiLabelsInPreview = false;

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
    private GameObject liveCoverageRoot;
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
    private Material weakCoverageMaterial;
    private Material fairCoverageMaterial;
    private Material goodCoverageMaterial;
    private bool previewHasProjectedColors;
    private float previewProjectedColorCoverage;
    private bool reconstructionPackageRunning;
    private bool mapConfirmationRunning;
    private readonly ScanMapService scanMapService = new();
    private static Mesh surfacePointMesh;
    private static Mesh thumbnailQuadMesh;
    private static Mesh coverageQuadMesh;
    private readonly List<VisualKeyframe> keyframes = new List<VisualKeyframe>(32);
    private float nextKeyframeCaptureTime;
    private float nextCoverageOverlayRefreshTime;
    private Vector3 lastKeyframePosition;
    private Quaternion lastKeyframeRotation = Quaternion.identity;
    private bool hasLastKeyframePose;
    private string scanId;
    private float scanStartTime;
    private bool geminiEnhancementRunning;
    private ScanQualityReportDto latestScanQuality;
    private CoverageSummary latestCoverageSummary;
    private int skippedFastMotionFrames;
    private int skippedUnsyncedDepthFrames;
    private int skippedLowConfidenceFrames;
    private RgbdDatasetRecorder rgbdRecorder;
    private float nextRgbdRecorderFrameTime;
    private int nextRgbdRecorderFrameId;
    private string lastRgbdRecorderDatasetPath = string.Empty;

    private void Awake()
    {
        if (!arSession)
            arSession = FindFirstObjectByType<ARSession>();

        if (!meshManager)
            meshManager = FindFirstObjectByType<ARMeshManager>();

        if (!planeManager)
            planeManager = FindFirstObjectByType<ARPlaneManager>();

        if (!planeManager && meshManager)
        {
            var planeManagerGo = new GameObject("AR Plane Manager");
            planeManagerGo.transform.SetParent(meshManager.transform.parent, false);
            planeManager = planeManagerGo.AddComponent<ARPlaneManager>();
        }

        if (!arCamera)
            arCamera = Camera.main;

        if (!arCameraManager)
        {
            if (!arCamera || !arCamera.TryGetComponent<ARCameraManager>(out arCameraManager))
                arCameraManager = FindFirstObjectByType<ARCameraManager>();
        }

        if (!arOcclusionManager)
        {
            if (!arCamera || !arCamera.TryGetComponent<AROcclusionManager>(out arOcclusionManager))
                arOcclusionManager = FindFirstObjectByType<AROcclusionManager>();
        }

        if (!arOcclusionManager && captureDepthForReconstruction && arCamera)
            arOcclusionManager = arCamera.gameObject.AddComponent<AROcclusionManager>();

        if (arOcclusionManager)
        {
            arOcclusionManager.requestedEnvironmentDepthMode = EnvironmentDepthMode.Best;
            arOcclusionManager.environmentDepthTemporalSmoothingRequested = true;
        }

        if (planeManager)
            planeManager.requestedDetectionMode = PlaneDetectionMode.Horizontal | PlaneDetectionMode.Vertical;

        if (arCamera)
            arCamera.TryGetComponent<ARCameraBackground>(out arCameraBackground);
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
        StopRgbdRecorder();
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
        StopRgbdRecorder();
        DestroyLiveCoverageOverlay();
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

        if (MapScanSession.IsViewingStoredResult)
            _ = LoadStoredReconstructionAsync();
        else if (MapScanSession.HasScanTarget)
            StartScan();
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
        {
            TryCaptureRgbdRecorderFrameIfNeeded();
            TryCaptureVisualKeyframe();
            UpdateLiveCoverageOverlayIfNeeded();
        }
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
        nextCoverageOverlayRefreshTime = 0f;
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

            if (meshFilter.TryGetComponent<MeshRenderer>(out var meshRenderer) && meshMaterial)
                meshRenderer.sharedMaterial = meshMaterial;
        }
    }

    public void StartScan()
    {
        if (MapScanSession.IsViewingStoredResult || reconstructionPackageRunning)
            return;

        scanMode = ScanMode.Scanning;
        meshesAdded = 0;
        meshesUpdated = 0;
        meshesRemoved = 0;
        previewMeshCount = 0;
        previewVertexCount = 0;
        previewTriangleCount = 0;
        ClearKeyframes();
        nextKeyframeCaptureTime = 0f;
        nextCoverageOverlayRefreshTime = 0f;
        hasLastKeyframePose = false;
        scanId = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        scanStartTime = Time.time;
        geminiEnhancementRunning = false;
        reconstructionPackageRunning = false;
        latestScanQuality = null;
        latestCoverageSummary = null;
        skippedFastMotionFrames = 0;
        skippedUnsyncedDepthFrames = 0;
        skippedLowConfidenceFrames = 0;
        nextRgbdRecorderFrameTime = 0f;
        nextRgbdRecorderFrameId = 1;
        lastRgbdRecorderDatasetPath = string.Empty;

        DestroyPreview();
        DestroyLiveCoverageOverlay();

        if (meshManager)
        {
            meshManager.gameObject.SetActive(true);
            meshManager.enabled = true;
            meshManager.DestroyAllMeshes();
        }

        if (planeManager)
        {
            planeManager.gameObject.SetActive(true);
            planeManager.requestedDetectionMode = PlaneDetectionMode.Horizontal | PlaneDetectionMode.Vertical;
            planeManager.enabled = true;
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
        StartRgbdRecorder();
        UpdateSessionState(ARSession.state);
        UpdateButtonStates();
        UpdateStats(force: true);
    }

    public void StopScanAndShowMap()
    {
        if (MapScanSession.IsViewingStoredResult || reconstructionPackageRunning)
            return;

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

        latestScanQuality = BuildScanQualityReport(bounds.Value);
        if (requireMinimumQualityToStop && latestScanQuality != null && latestScanQuality.score < minimumStopQualityScore)
        {
            DestroyPreview();
            SetExportStatus(
                "Scan needs more coverage before stopping.\n" +
                latestScanQuality.primaryGuidance);
            UpdateButtonStates();
            return;
        }

        scanMode = ScanMode.Preview;
        SetScanningEnabled(false);
        StopRgbdRecorder();
        SetLiveMeshesVisible(false);
        DestroyLiveCoverageOverlay();
        ShowPreviewCamera(bounds.Value);

        BuildVisualKeyframePreview(bounds.Value);

        SetExportStatus(previewHasProjectedColors
            ? $"Scan stopped. Showing photo-projected LiDAR map.\nCoverage: {previewProjectedColorCoverage:P0}"
            : "Scan stopped. Showing clean structural map.");

        _ = FinalizeCompletedScanAsync(bounds.Value);

        if (enhancePreviewWithGemini && !geminiEnhancementRunning)
            _ = EnhancePreviewWithGeminiAsync(bounds.Value);

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

    private async Awaitable FinalizeCompletedScanAsync(Bounds bounds)
    {
        reconstructionPackageRunning = true;
        mapConfirmationRunning = MapScanSession.HasPendingMap;
        UpdateButtonStates();

        try
        {
            string zipPath = string.Empty;
            if (packageReconstructionScanOnStop)
            {
                SetExportStatus("Packaging RGB-D reconstruction scan...");
                zipPath = BuildReconstructionPackage(bounds);
                Debug.Log($"[ARKitMeshScanController] Reconstruction package: {zipPath}");
                SetExportStatus($"Reconstruction package ready.\n{Path.GetFileName(zipPath)}");
            }

            if (MapScanSession.HasPendingMap)
            {
                SetExportStatus("Scan complete. Saving map...");
                ScanMapCreateResult result = await scanMapService.CreateMapAsync(MapScanSession.PendingMapRequest);
                if (!result.IsSuccess)
                {
                    SetExportStatus("Scan completed, but the map could not be saved. Return and try again.");
                    return;
                }

                MapScanSession.ConfirmMap(result.CreatedMapId);
                mapConfirmationRunning = false;
                UpdateButtonStates();
            }

            if (!packageReconstructionScanOnStop)
            {
                SetExportStatus("Scan complete. Map saved.");
                return;
            }

            if (uploadReconstructionPackageOnStop
                && (MapScanSession.HasActiveMap || !string.IsNullOrWhiteSpace(reconstructionUploadUrl)))
            {
                await UploadReconstructionPackageAsync(zipPath);
            }
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            SetExportStatus("Reconstruction package failed. See console.");
        }
        finally
        {
            mapConfirmationRunning = false;
            reconstructionPackageRunning = false;
            UpdateButtonStates();
        }
    }

    private string BuildReconstructionPackage(Bounds bounds)
    {
        var id = string.IsNullOrWhiteSpace(scanId) ? DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") : scanId;
        var root = Path.Combine(Application.persistentDataPath, "ReconstructionScans");
        var scanFolder = Path.Combine(root, id);
        var framesFolder = Path.Combine(scanFolder, "frames");

        if (Directory.Exists(scanFolder))
            Directory.Delete(scanFolder, true);

        Directory.CreateDirectory(framesFolder);

        if (meshManager && meshManager.meshes != null)
            WriteObj(Path.Combine(scanFolder, "raw_mesh.obj"), meshManager.meshes);

        var rgbdDatasetFolder = string.Empty;
        if (TryCopyRgbdRecorderDataset(scanFolder, out var copiedRgbdDatasetFolder))
            rgbdDatasetFolder = copiedRgbdDatasetFolder;

        var frameDtos = new List<ReconstructionFrameDto>(keyframes.Count);
        for (var i = 0; i < keyframes.Count; i++)
        {
            var frameFolderName = $"frame_{i + 1:D4}";
            var frameFolder = Path.Combine(framesFolder, frameFolderName);
            Directory.CreateDirectory(frameFolder);

            frameDtos.Add(WriteReconstructionFrame(keyframes[i], i + 1, frameFolderName, frameFolder));
        }

        var manifest = new ReconstructionScanManifest
        {
            schemaVersion = "memoanchor.reconstruction-scan.v1",
            scanId = id,
            capturedAtUtc = DateTime.UtcNow.ToString("o"),
            durationSeconds = Mathf.Max(0f, Time.time - scanStartTime),
            coordinateSystem = "Unity world space, meters",
            hasRawMeshObj = File.Exists(Path.Combine(scanFolder, "raw_mesh.obj")),
            hasRgbdRecorderDataset = !string.IsNullOrWhiteSpace(rgbdDatasetFolder),
            rgbdRecorderDatasetFolder = rgbdDatasetFolder,
            depthCaptureRequested = captureDepthForReconstruction,
            planeCaptureRequested = captureDetectedPlanesForReconstruction,
            bounds = new BoundsDto
            {
                center = Vector3Dto.From(bounds.center),
                size = Vector3Dto.From(bounds.size),
                min = Vector3Dto.From(bounds.min),
                max = Vector3Dto.From(bounds.max)
            },
            mesh = BuildMeshSummary(),
            quality = latestScanQuality ?? BuildScanQualityReport(bounds),
            planes = BuildDetectedPlaneDtos().ToArray(),
            frames = frameDtos.ToArray()
        };

        File.WriteAllText(Path.Combine(scanFolder, "manifest.json"), JsonUtility.ToJson(manifest, true));

        var zipPath = Path.Combine(root, $"{id}.zip");
        if (File.Exists(zipPath))
            File.Delete(zipPath);

        ZipFile.CreateFromDirectory(scanFolder, zipPath, System.IO.Compression.CompressionLevel.Fastest, false);
        return zipPath;
    }

    private bool TryCopyRgbdRecorderDataset(string scanFolder, out string relativeDatasetFolder)
    {
        relativeDatasetFolder = string.Empty;

        if (string.IsNullOrWhiteSpace(lastRgbdRecorderDatasetPath) || !Directory.Exists(lastRgbdRecorderDatasetPath))
            return false;

        if (!File.Exists(Path.Combine(lastRgbdRecorderDatasetPath, "session.json")) ||
            !File.Exists(Path.Combine(lastRgbdRecorderDatasetPath, "frames.jsonl")))
            return false;

        relativeDatasetFolder = "rgbd_dataset";
        var destination = Path.Combine(scanFolder, relativeDatasetFolder);
        if (Directory.Exists(destination))
            Directory.Delete(destination, true);

        CopyDirectory(lastRgbdRecorderDatasetPath, destination);
        return true;
    }

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);

        foreach (var file in Directory.GetFiles(sourceDirectory))
        {
            var destination = Path.Combine(destinationDirectory, Path.GetFileName(file));
            File.Copy(file, destination, true);
        }

        foreach (var directory in Directory.GetDirectories(sourceDirectory))
        {
            var destination = Path.Combine(destinationDirectory, Path.GetFileName(directory));
            CopyDirectory(directory, destination);
        }
    }

    private ReconstructionFrameDto WriteReconstructionFrame(VisualKeyframe keyframe, int index, string relativeFrameFolder, string frameFolder)
    {
        var rgbFile = $"rgb_{index:D4}.jpg";
        if (keyframe.Texture)
            File.WriteAllBytes(Path.Combine(frameFolder, rgbFile), keyframe.Texture.EncodeToJPG(92));

        var depthDto = WriteCpuImageFrame(keyframe.Depth, frameFolder, $"depth_{index:D4}");
        var confidenceDto = WriteCpuImageFrame(keyframe.Confidence, frameFolder, $"confidence_{index:D4}");

        var dto = new ReconstructionFrameDto
        {
            id = $"frame_{index:D4}",
            folder = relativeFrameFolder,
            rgbFile = keyframe.Texture ? rgbFile : string.Empty,
            timestampSeconds = keyframe.Timestamp,
            cameraTimestampSeconds = keyframe.CameraTimestamp,
            rgbDepthTimestampDeltaSeconds = keyframe.RgbDepthTimestampDeltaSeconds,
            rgbConfidenceTimestampDeltaSeconds = keyframe.RgbConfidenceTimestampDeltaSeconds,
            position = Vector3Dto.From(keyframe.Position),
            rotation = QuaternionDto.From(keyframe.Rotation),
            hasIntrinsics = keyframe.HasIntrinsics,
            focalLength = Vector2Dto.From(keyframe.FocalLength),
            principalPoint = Vector2Dto.From(keyframe.PrincipalPoint),
            imageResolution = Vector2Dto.From(keyframe.ImageResolution),
            fieldOfView = keyframe.FieldOfView,
            aspect = keyframe.Aspect,
            hasSurfacePoint = keyframe.HasSurfacePoint,
            surfacePoint = Vector3Dto.From(keyframe.HasSurfacePoint ? keyframe.SurfacePoint : keyframe.Position),
            depth = depthDto,
            confidence = confidenceDto,
            depthTimestampSeconds = keyframe.Depth != null ? keyframe.Depth.Timestamp : 0d,
            depthConfidenceRatio = keyframe.DepthConfidenceRatio
        };

        File.WriteAllText(Path.Combine(frameFolder, "frame.json"), JsonUtility.ToJson(dto, true));
        return dto;
    }

    private static CpuImageFrameDto WriteCpuImageFrame(CpuImageFrame frame, string frameFolder, string baseName)
    {
        if (frame == null || frame.Planes == null || frame.Planes.Length == 0)
            return null;

        var planeDtos = new CpuImagePlaneDto[frame.Planes.Length];
        for (var i = 0; i < frame.Planes.Length; i++)
        {
            var plane = frame.Planes[i];
            var fileName = $"{baseName}_plane{i}.raw";
            File.WriteAllBytes(Path.Combine(frameFolder, fileName), plane.Data ?? Array.Empty<byte>());
            planeDtos[i] = new CpuImagePlaneDto
            {
                file = fileName,
                rowStride = plane.RowStride,
                pixelStride = plane.PixelStride,
                byteLength = plane.Data?.Length ?? 0
            };
        }

        return new CpuImageFrameDto
        {
            kind = frame.Kind,
            width = frame.Width,
            height = frame.Height,
            format = frame.Format,
            timestamp = frame.Timestamp,
            planes = planeDtos
        };
    }

    private List<DetectedPlaneDto> BuildDetectedPlaneDtos()
    {
        var planes = new List<DetectedPlaneDto>();
        if (!captureDetectedPlanesForReconstruction || !planeManager)
            return planes;

        foreach (var plane in planeManager.trackables)
        {
            if (!plane || plane.trackingState == TrackingState.None)
                continue;

            var boundary = plane.boundary;
            Vector3Dto[] boundaryWorld;
            if (boundary.IsCreated && boundary.Length >= 3)
            {
                boundaryWorld = new Vector3Dto[boundary.Length];
                for (var i = 0; i < boundary.Length; i++)
                {
                    var local = new Vector3(boundary[i].x, 0f, boundary[i].y);
                    boundaryWorld[i] = Vector3Dto.From(plane.transform.TransformPoint(local));
                }
            }
            else
            {
                var halfExtents = plane.extents * 0.5f;
                if (halfExtents.x <= 0.01f || halfExtents.y <= 0.01f)
                    continue;

                boundaryWorld = new[]
                {
                    Vector3Dto.From(plane.transform.TransformPoint(new Vector3(-halfExtents.x, 0f, -halfExtents.y))),
                    Vector3Dto.From(plane.transform.TransformPoint(new Vector3(halfExtents.x, 0f, -halfExtents.y))),
                    Vector3Dto.From(plane.transform.TransformPoint(new Vector3(halfExtents.x, 0f, halfExtents.y))),
                    Vector3Dto.From(plane.transform.TransformPoint(new Vector3(-halfExtents.x, 0f, halfExtents.y)))
                };
            }

            planes.Add(new DetectedPlaneDto
            {
                id = plane.trackableId.ToString(),
                alignment = plane.alignment.ToString(),
                trackingState = plane.trackingState.ToString(),
                center = Vector3Dto.From(plane.transform.TransformPoint(plane.center)),
                normal = Vector3Dto.From(plane.transform.up),
                extents = Vector2Dto.From(plane.extents),
                size = Vector2Dto.From(plane.size),
                boundaryWorld = boundaryWorld
            });
        }

        return planes;
    }

    private async Awaitable UploadReconstructionPackageAsync(string zipPath)
    {
        if (string.IsNullOrWhiteSpace(zipPath) || !File.Exists(zipPath))
            return;

        SetExportStatus("Uploading reconstruction package...");

        bool useMemoAnchorServer = MapScanSession.HasActiveMap;
        string uploadUrl = useMemoAnchorServer
            ? ServicesManager.BuildServerUrl(MapScanSession.BuildUploadPath())
            : reconstructionUploadUrl.Trim();
        if (!Uri.TryCreate(uploadUrl, UriKind.Absolute, out _))
        {
            SetExportStatus("Upload URL is invalid. Package saved locally.");
            Debug.LogWarning($"[ARKitMeshScanController] Invalid reconstruction upload URL: '{uploadUrl}'");
            return;
        }

        using (var request = new UnityWebRequest(uploadUrl, UnityWebRequest.kHttpVerbPOST))
        {
            request.uploadHandler = new UploadHandlerFile(zipPath);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = RECONSTRUCTION_REQUEST_TIMEOUT_SECONDS;
            request.SetRequestHeader("Content-Type", "application/zip");
            request.SetRequestHeader("X-MemoAnchor-Scan-Id", string.IsNullOrWhiteSpace(scanId) ? "unknown" : scanId);
            request.SetRequestHeader("X-MemoAnchor-Filename", Path.GetFileName(zipPath));
            if (useMemoAnchorServer)
                ServicesManager.Authorize(request);

            try
            {
                await ServicesManager.SendRequestAsync(request);
            }
            catch (InvalidOperationException ex)
            {
                SetExportStatus("Upload blocked by Player Settings. Package saved locally.");
                Debug.LogError($"[ARKitMeshScanController] Reconstruction upload blocked before request was sent: {ex.Message}");
                return;
            }

            if (request.result == UnityWebRequest.Result.Success)
            {
                var serverScanId = ParseUploadScanId(request.downloadHandler.text);
                SetExportStatus($"Reconstruction package uploaded.\nWaiting for server: {serverScanId}");
                Debug.Log($"[ARKitMeshScanController] Reconstruction upload complete: {request.downloadHandler.text}");
                await PollReconstructionStatusAsync(serverScanId);
            }
            else
            {
                SetExportStatus($"Upload failed ({request.responseCode}). Package saved locally.");
                Debug.LogWarning($"[ARKitMeshScanController] Reconstruction upload failed ({request.responseCode}): {request.error}\n{request.downloadHandler.text}");
            }
        }
    }

    private async Awaitable LoadStoredReconstructionAsync()
    {
        SetExportStatus("Loading saved server reconstruction...");
        await PollReconstructionStatusAsync(MapScanSession.ReconstructionScanId);
    }

    private async Awaitable PollReconstructionStatusAsync(string serverScanId)
    {
        if (string.IsNullOrWhiteSpace(serverScanId))
            return;

        bool useMemoAnchorServer = MapScanSession.HasActiveMap;
        string statusUrl = useMemoAnchorServer
            ? ServicesManager.BuildServerUrl(MapScanSession.BuildStatusPath(serverScanId))
            : BuildReconstructionEndpointUrl("status", serverScanId);
        if (string.IsNullOrWhiteSpace(statusUrl))
            return;

        for (var attempt = 0; attempt < RECONSTRUCTION_STATUS_POLL_ATTEMPTS; attempt++)
        {
            using (var request = UnityWebRequest.Get(statusUrl))
            {
                request.timeout = 10;
                if (useMemoAnchorServer)
                    ServicesManager.Authorize(request);
                await ServicesManager.SendRequestAsync(request);

                if (request.result != UnityWebRequest.Result.Success)
                {
                    SetExportStatus($"Server status check failed ({request.responseCode}).");
                    Debug.LogWarning($"[ARKitMeshScanController] Reconstruction status failed ({request.responseCode}): {request.error}");
                    return;
                }

                var status = ParseReconstructionStatus(request.downloadHandler.text);
                if (status == null || string.IsNullOrWhiteSpace(status.state))
                {
                    SetExportStatus("Server status response was invalid.");
                    Debug.LogWarning($"[ARKitMeshScanController] Invalid reconstruction status: {request.downloadHandler.text}");
                    return;
                }

                if (status.state == "done")
                {
                    string resultUrl = useMemoAnchorServer
                        ? ServicesManager.BuildServerUrl(MapScanSession.BuildResultPath(serverScanId))
                        : BuildReconstructionEndpointUrl("result", serverScanId);
                    SetExportStatus($"Server reconstruction done.\nDownloading {status.resultFile}...");
                    Debug.Log($"[ARKitMeshScanController] Reconstruction result ready: {resultUrl}");
                    await DownloadAndShowReconstructionResultAsync(resultUrl, status.resultFile, serverScanId, useMemoAnchorServer);
                    return;
                }

                if (status.state == "failed")
                {
                    SetExportStatus($"Server reconstruction failed.\n{status.message}");
                    Debug.LogWarning($"[ARKitMeshScanController] Reconstruction failed: {request.downloadHandler.text}");
                    return;
                }

                SetExportStatus($"Server reconstruction {status.state}...\n{status.message}");
            }

            await Awaitable.WaitForSecondsAsync(2f);
        }

        SetExportStatus("Server reconstruction still running. Check server status manually.");
    }

    private async Awaitable DownloadAndShowReconstructionResultAsync(
        string resultUrl,
        string resultFile,
        string serverScanId,
        bool useMemoAnchorServer)
    {
        if (string.IsNullOrWhiteSpace(resultUrl))
            return;

        using (var request = UnityWebRequest.Get(resultUrl))
        {
            request.timeout = RECONSTRUCTION_REQUEST_TIMEOUT_SECONDS;
            if (useMemoAnchorServer)
                ServicesManager.Authorize(request);
            await ServicesManager.SendRequestAsync(request);

            if (request.result != UnityWebRequest.Result.Success)
            {
                SetExportStatus($"Result download failed ({request.responseCode}).");
                Debug.LogWarning($"[ARKitMeshScanController] Reconstruction result download failed ({request.responseCode}): {request.error}");
                return;
            }

            var bytes = request.downloadHandler.data;
            var fileName = string.IsNullOrWhiteSpace(resultFile) ? "result.ply" : resultFile;
            var resultFolder = Path.Combine(Application.persistentDataPath, "ReconstructionResults", serverScanId);
            Directory.CreateDirectory(resultFolder);
            var localPath = Path.Combine(resultFolder, fileName);
            File.WriteAllBytes(localPath, bytes);

            if (!fileName.EndsWith(".ply", StringComparison.OrdinalIgnoreCase))
            {
                SetExportStatus($"Result downloaded, but app preview supports PLY first.\n{fileName}");
                Debug.Log($"[ARKitMeshScanController] Reconstruction result saved: {localPath}");
                return;
            }

            if (!TryCreateMeshFromPly(bytes, out var mesh, out var error))
            {
                SetExportStatus($"Result downloaded, but PLY preview failed.\n{error}");
                Debug.LogWarning($"[ARKitMeshScanController] PLY preview failed: {error}\n{localPath}");
                return;
            }

            ShowServerReconstructionPreview(mesh, serverScanId, localPath);
            SetExportStatus($"Server reconstruction loaded in app.\n{mesh.vertexCount:N0} vertices / {mesh.triangles.Length / 3:N0} triangles");
        }
    }

    private string BuildReconstructionEndpointUrl(string endpoint, string serverScanId)
    {
        try
        {
            var builder = new UriBuilder(reconstructionUploadUrl.Trim())
            {
                Path = $"/{endpoint}/{Uri.EscapeDataString(serverScanId)}",
                Query = string.Empty
            };
            return builder.Uri.ToString();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ARKitMeshScanController] Could not build reconstruction {endpoint} URL: {ex.Message}");
            return string.Empty;
        }
    }

    private string ParseUploadScanId(string json)
    {
        try
        {
            var response = JsonUtility.FromJson<ReconstructionUploadResponseDto>(json);
            if (response != null && !string.IsNullOrWhiteSpace(response.scanId))
                return response.scanId;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ARKitMeshScanController] Could not parse upload response: {ex.Message}");
        }

        return string.IsNullOrWhiteSpace(scanId) ? "unknown" : scanId;
    }

    private ReconstructionStatusDto ParseReconstructionStatus(string json)
    {
        try
        {
            return JsonUtility.FromJson<ReconstructionStatusDto>(json);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ARKitMeshScanController] Could not parse reconstruction status: {ex.Message}");
            return null;
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

        MapScanSession.Clear();
        SceneManager.LoadScene(targetScene);
    }

    private void StartRgbdRecorder()
    {
        if (!recordRgbdDatasetOnScan)
            return;

        StopRgbdRecorder();
        rgbdRecorder = new RgbdDatasetRecorder();
        var root = Path.Combine(Application.persistentDataPath, "RgbdRecorder");
        var frameRate = 1f / Mathf.Max(0.05f, rgbdRecorderFrameIntervalSeconds);
        var metadata = new RgbdSessionMetadata
        {
            schema_version = "memoanchor.rgbd-recorder.v1",
            scan_id = string.IsNullOrWhiteSpace(scanId) ? DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") : scanId,
            capture_start_time_utc = DateTime.UtcNow.ToString("o"),
            unity_version = Application.unityVersion,
            ar_foundation_version = typeof(ARSession).Assembly.GetName().Version?.ToString() ?? "unknown",
            operating_system = SystemInfo.operatingSystem,
            device_model = SystemInfo.deviceModel,
            target_frame_rate_hz = frameRate,
            max_rgb_depth_timestamp_difference_ms = Mathf.Max(0.001f, maxRgbDepthTimestampDeltaSeconds) * 1000d,
            rgb_format = "jpg",
            depth_format = "raw XRCpuImage plane 0",
            depth_unit = "meters when AR Foundation provider reports a float depth image; raw format is stored per frame",
            coordinate_system = "Unity world space, meters, left-handed scene convention: +X right, +Y up",
            camera_forward_convention = "Unity camera Transform.forward is local +Z in world direction; view space convention is not converted here",
            pose_convention = "camera-to-world transform from AR camera transform",
            matrix_serialization_order = "row-major JSON array: m00,m01,m02,m03,m10,...,m33 read from UnityEngine.Matrix4x4[row,column]",
            quaternion_order = "x,y,z,w",
            world_scale = "1 Unity unit = 1 meter",
            timestamp_policy = "RGB/depth/confidence XRCpuImage timestamps are recorded as provider timestamps. Timestamp difference is used as a guard but not treated as absolute proof of synchronization across providers."
        };

        rgbdRecorder.Start(root, metadata, rgbdRecorderMaxQueue);
        lastRgbdRecorderDatasetPath = rgbdRecorder.DatasetPath;
        nextRgbdRecorderFrameTime = 0f;
        nextRgbdRecorderFrameId = 1;
    }

    private void StopRgbdRecorder()
    {
        if (rgbdRecorder == null)
            return;

        lastRgbdRecorderDatasetPath = rgbdRecorder.DatasetPath;
        rgbdRecorder.Stop();
        rgbdRecorder.Dispose();
        rgbdRecorder = null;
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

        if (scanMode == ScanMode.Scanning)
            latestScanQuality = BuildScanQualityReport(null, meshCount, vertexCount, triangleCount);
        else if (scanMode == ScanMode.Ready)
            latestScanQuality = null;

        var qualityText = string.Empty;
        if (showScanQualityGuidance && latestScanQuality != null)
        {
            qualityText =
                $"\nQuality: {latestScanQuality.score:0}% ({latestScanQuality.grade})\n" +
                $"Depth: {latestScanQuality.averageDepthConfidence:P0}\n" +
                $"Sync: avg {latestScanQuality.averageRgbdTimestampDeltaMs:0}ms / max {latestScanQuality.maxRgbdTimestampDeltaMs:0}ms\n" +
                $"Skipped: fast {latestScanQuality.skippedFastMotionFrames} / sync {latestScanQuality.skippedUnsyncedDepthFrames} / depth {latestScanQuality.skippedLowConfidenceFrames}\n" +
                $"Guide: {latestScanQuality.primaryGuidance}";
        }

        var coverageText = string.Empty;
        if (latestCoverageSummary != null && latestCoverageSummary.totalCells > 0)
        {
            coverageText =
                $"\nCoverage: weak {latestCoverageSummary.weakCells} / fair {latestCoverageSummary.fairCells} / good {latestCoverageSummary.goodCells}";
        }

        var recorderText = BuildRecorderStatsText();

        meshStatsText.text =
            $"Mode: {scanMode}\n" +
            $"Meshes: {meshCount}\n" +
            $"Vertices: {vertexCount:N0}\n" +
            $"Triangles: {triangleCount:N0}\n" +
            $"Keyframes: {keyframes.Count}\n" +
            $"Changed: +{meshesAdded} / ~{meshesUpdated} / -{meshesRemoved}" +
            qualityText +
            coverageText +
            recorderText;
    }

    private string BuildRecorderStatsText()
    {
        if (rgbdRecorder == null)
        {
            return string.IsNullOrWhiteSpace(lastRgbdRecorderDatasetPath)
                ? "\nRGB-D Recorder: stopped"
                : $"\nRGB-D Recorder: stopped\nDataset: {lastRgbdRecorderDatasetPath}";
        }

        var diagnostics = rgbdRecorder.SnapshotDiagnostics();
        return
            $"\nRGB-D Recorder: {diagnostics.recorder_state}" +
            $"\nFrames: captured {diagnostics.captured_frames} / saved {diagnostics.saved_frames} / dropped {diagnostics.dropped_frames}" +
            $"\nFailures: rgb {diagnostics.rgb_acquisition_failures} / depth {diagnostics.depth_acquisition_failures} / conf {diagnostics.confidence_acquisition_failures} / intr {diagnostics.intrinsics_failures}" +
            $"\nSync: {diagnostics.last_timestamp_difference_ms:0.0}ms, queue {diagnostics.pending_write_queue}" +
            $"\nRGB: {diagnostics.last_rgb_width}x{diagnostics.last_rgb_height}, Depth: {diagnostics.last_depth_width}x{diagnostics.last_depth_height}" +
            $"\nTracking: {diagnostics.tracking_state}" +
            (string.IsNullOrWhiteSpace(diagnostics.last_error) ? string.Empty : $"\nRecorder error: {diagnostics.last_error}") +
            $"\nDataset: {diagnostics.dataset_path}";
    }

    private void SetExportStatus(string message)
    {
        if (exportStatusText)
            exportStatusText.text = message;
    }

    private Bounds? BuildPreviewFromLiveMeshes()
    {
        DestroyPreview();
        previewHasProjectedColors = false;
        previewProjectedColorCoverage = 0f;

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
        previewHasProjectedColors = colorizePreviewFromKeyframes && TryApplyProjectedKeyframeColors(previewMesh);

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

    private void ShowServerReconstructionPreview(Mesh mesh, string serverScanId, string localPath)
    {
        if (!mesh)
            return;

        DestroyPreview();
        previewHasProjectedColors = mesh.colors != null && mesh.colors.Length == mesh.vertexCount;
        previewProjectedColorCoverage = 0f;

        previewRoot = new GameObject($"Server Reconstruction Preview {serverScanId}");

        var previewGo = new GameObject("Server Reconstruction Surface");
        previewGo.transform.SetParent(previewRoot.transform, false);
        previewGo.AddComponent<MeshFilter>().sharedMesh = mesh;

        var renderer = previewGo.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = GetPreviewMaterial();

        previewMeshCount = 1;
        previewVertexCount = mesh.vertexCount;
        previewTriangleCount = mesh.triangles.Length / 3;
        previewBounds = mesh.bounds;
        previewCenter = mesh.bounds.center;

        scanMode = ScanMode.Preview;
        ShowPreviewCamera(mesh.bounds);
        BuildVisualKeyframePreview(mesh.bounds);
        UpdateButtonStates();
        UpdateStats(force: true);

        Debug.Log($"[ARKitMeshScanController] Showing server reconstruction: {localPath}");
    }

    private static bool TryCreateMeshFromPly(byte[] data, out Mesh mesh, out string error)
    {
        mesh = null;
        error = string.Empty;

        if (data == null || data.Length == 0)
        {
            error = "empty file";
            return false;
        }

        if (!TryParsePlyHeader(data, out var header, out var dataOffset, out error))
            return false;

        if (!header.IsBinaryLittleEndian)
        {
            error = $"unsupported PLY format: {header.Format}";
            return false;
        }

        var offset = dataOffset;
        var vertices = new List<Vector3>(Mathf.Max(0, header.VertexCount));
        var normals = header.HasNormals ? new List<Vector3>(Mathf.Max(0, header.VertexCount)) : null;
        var colors = header.HasColors ? new List<Color>(Mathf.Max(0, header.VertexCount)) : null;

        for (var i = 0; i < header.VertexCount; i++)
        {
            var x = 0f;
            var y = 0f;
            var z = 0f;
            var nx = 0f;
            var ny = 0f;
            var nz = 0f;
            var red = 255f;
            var green = 255f;
            var blue = 255f;
            var alpha = 255f;

            foreach (var property in header.VertexProperties)
            {
                if (!TryReadPlyScalarAsFloat(data, ref offset, property.Type, out var value))
                {
                    error = $"unexpected end of vertex data at vertex {i}";
                    return false;
                }

                switch (property.Name)
                {
                    case "x":
                        x = value;
                        break;
                    case "y":
                        y = value;
                        break;
                    case "z":
                        z = value;
                        break;
                    case "nx":
                        nx = value;
                        break;
                    case "ny":
                        ny = value;
                        break;
                    case "nz":
                        nz = value;
                        break;
                    case "red":
                    case "r":
                        red = value;
                        break;
                    case "green":
                    case "g":
                        green = value;
                        break;
                    case "blue":
                    case "b":
                        blue = value;
                        break;
                    case "alpha":
                    case "a":
                        alpha = value;
                        break;
                }
            }

            vertices.Add(new Vector3(x, y, z));
            normals?.Add(new Vector3(nx, ny, nz).normalized);
            colors?.Add(new Color(
                Mathf.Clamp01(red / 255f),
                Mathf.Clamp01(green / 255f),
                Mathf.Clamp01(blue / 255f),
                Mathf.Clamp01(alpha / 255f)));
        }

        var triangles = new List<int>(Mathf.Max(0, header.FaceCount * 3));
        for (var i = 0; i < header.FaceCount; i++)
        {
            if (!TryReadPlyListCount(data, ref offset, header.FaceCountType, out var count))
            {
                error = $"unexpected end of face list at face {i}";
                return false;
            }

            var indices = new int[count];
            for (var j = 0; j < count; j++)
            {
                if (!TryReadPlyScalarAsInt(data, ref offset, header.FaceIndexType, out indices[j]))
                {
                    error = $"unexpected end of face index data at face {i}";
                    return false;
                }
            }

            if (count < 3)
                continue;

            for (var j = 1; j < count - 1; j++)
            {
                triangles.Add(indices[0]);
                triangles.Add(indices[j]);
                triangles.Add(indices[j + 1]);
            }
        }

        if (vertices.Count == 0 || triangles.Count == 0)
        {
            error = "PLY had no renderable triangles";
            return false;
        }

        mesh = new Mesh
        {
            name = "Server_Reconstruction_PLY",
            indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16
        };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);

        if (normals != null && normals.Count == vertices.Count)
            mesh.SetNormals(normals);
        else
            mesh.RecalculateNormals();

        if (colors != null && colors.Count == vertices.Count)
            mesh.SetColors(colors);

        mesh.RecalculateBounds();
        return true;
    }

    private static bool TryParsePlyHeader(byte[] data, out PlyHeader header, out int dataOffset, out string error)
    {
        header = new PlyHeader();
        dataOffset = 0;
        error = string.Empty;

        var headerText = Encoding.ASCII.GetString(data, 0, Mathf.Min(data.Length, 64 * 1024));
        var markerIndex = headerText.IndexOf("end_header", StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            error = "PLY header missing end_header";
            return false;
        }

        var newlineIndex = headerText.IndexOf('\n', markerIndex);
        if (newlineIndex < 0)
        {
            error = "PLY header missing newline after end_header";
            return false;
        }

        dataOffset = Encoding.ASCII.GetByteCount(headerText.Substring(0, newlineIndex + 1));
        var lines = headerText.Substring(0, newlineIndex + 1)
            .Replace("\r\n", "\n")
            .Split('\n');

        var currentElement = string.Empty;
        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line) || line == "ply" || line == "end_header" || line.StartsWith("comment ", StringComparison.Ordinal))
                continue;

            var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                continue;

            if (parts[0] == "format" && parts.Length >= 2)
            {
                header.Format = parts[1];
                continue;
            }

            if (parts[0] == "element" && parts.Length >= 3)
            {
                currentElement = parts[1];
                if (currentElement == "vertex")
                    int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out header.VertexCount);
                else if (currentElement == "face")
                    int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out header.FaceCount);
                continue;
            }

            if (parts[0] == "property" && currentElement == "vertex" && parts.Length >= 3 && parts[1] != "list")
            {
                header.VertexProperties.Add(new PlyProperty(parts[2], parts[1]));
                continue;
            }

            if (parts[0] == "property" && currentElement == "face" && parts.Length >= 5 && parts[1] == "list")
            {
                header.FaceCountType = parts[2];
                header.FaceIndexType = parts[3];
            }
        }

        if (header.VertexCount <= 0 || header.FaceCount <= 0 || header.VertexProperties.Count == 0)
        {
            error = "PLY header missing vertex/face data";
            return false;
        }

        if (string.IsNullOrWhiteSpace(header.FaceCountType) || string.IsNullOrWhiteSpace(header.FaceIndexType))
        {
            error = "PLY face list property missing";
            return false;
        }

        return true;
    }

    private static bool TryReadPlyListCount(byte[] data, ref int offset, string type, out int value)
    {
        return TryReadPlyScalarAsInt(data, ref offset, type, out value);
    }

    private static bool TryReadPlyScalarAsInt(byte[] data, ref int offset, string type, out int value)
    {
        value = 0;
        if (!TryReadPlyScalarAsDouble(data, ref offset, type, out var doubleValue))
            return false;

        value = Mathf.RoundToInt((float)doubleValue);
        return true;
    }

    private static bool TryReadPlyScalarAsFloat(byte[] data, ref int offset, string type, out float value)
    {
        value = 0f;
        if (!TryReadPlyScalarAsDouble(data, ref offset, type, out var doubleValue))
            return false;

        value = (float)doubleValue;
        return true;
    }

    private static bool TryReadPlyScalarAsDouble(byte[] data, ref int offset, string type, out double value)
    {
        value = 0d;
        switch (type)
        {
            case "char":
            case "int8":
                if (offset + 1 > data.Length)
                    return false;
                value = unchecked((sbyte)data[offset]);
                offset += 1;
                return true;
            case "uchar":
            case "uint8":
                if (offset + 1 > data.Length)
                    return false;
                value = data[offset];
                offset += 1;
                return true;
            case "short":
            case "int16":
                if (offset + 2 > data.Length)
                    return false;
                value = BitConverter.ToInt16(data, offset);
                offset += 2;
                return true;
            case "ushort":
            case "uint16":
                if (offset + 2 > data.Length)
                    return false;
                value = BitConverter.ToUInt16(data, offset);
                offset += 2;
                return true;
            case "int":
            case "int32":
                if (offset + 4 > data.Length)
                    return false;
                value = BitConverter.ToInt32(data, offset);
                offset += 4;
                return true;
            case "uint":
            case "uint32":
                if (offset + 4 > data.Length)
                    return false;
                value = BitConverter.ToUInt32(data, offset);
                offset += 4;
                return true;
            case "float":
            case "float32":
                if (offset + 4 > data.Length)
                    return false;
                value = BitConverter.ToSingle(data, offset);
                offset += 4;
                return true;
            case "double":
            case "float64":
                if (offset + 8 > data.Length)
                    return false;
                value = BitConverter.ToDouble(data, offset);
                offset += 8;
                return true;
            default:
                return false;
        }
    }

    private void TryCaptureRgbdRecorderFrameIfNeeded()
    {
        if (rgbdRecorder == null || !rgbdRecorder.IsRecording)
            return;

        if (Time.unscaledTime < nextRgbdRecorderFrameTime)
            return;

        nextRgbdRecorderFrameTime = Time.unscaledTime + Mathf.Max(0.05f, rgbdRecorderFrameIntervalSeconds);

        if (!TryCreateCameraTexture(out var frame))
        {
            rgbdRecorder.RecordRgbAcquisitionFailure("RGB-D capture failed or did not pass synchronization guard.");
            return;
        }

        try
        {
            if (frame.Depth == null || frame.Depth.Planes == null || frame.Depth.Planes.Length == 0)
            {
                rgbdRecorder.RecordDepthAcquisitionFailure("Depth image was not available for recorder frame.");
                return;
            }

            if (frame.Confidence == null || frame.Confidence.Planes == null || frame.Confidence.Planes.Length == 0)
            {
                rgbdRecorder.RecordConfidenceAcquisitionFailure("Depth confidence image was not available for recorder frame.");
                return;
            }

            if (!frame.HasIntrinsics)
                rgbdRecorder.RecordIntrinsicsFailure("Camera intrinsics were not available for recorder frame.");

            var maxDeltaMs = Mathf.Max(0.001f, maxRgbDepthTimestampDeltaSeconds) * 1000d;
            var deltaMs = Math.Abs(frame.RgbDepthTimestampDeltaSeconds) * 1000d;
            if (deltaMs > maxDeltaMs)
            {
                rgbdRecorder.RecordTimestampRejection(deltaMs);
                return;
            }

            var rgbBytes = frame.Texture ? frame.Texture.EncodeToJPG(92) : null;
            if (rgbBytes == null || rgbBytes.Length == 0)
            {
                rgbdRecorder.RecordRgbAcquisitionFailure("RGB JPEG encoding failed.");
                return;
            }

            var id = nextRgbdRecorderFrameId;
            var rgbFile = $"rgb/{id:D6}.jpg";
            var depthFile = $"depth/{id:D6}.bin";
            var confidenceFile = $"confidence/{id:D6}.bin";
            var depthPlane = frame.Depth.Planes[0];
            var confidencePlane = frame.Confidence.Planes[0];
            var cameraTransform = arCamera ? arCamera.transform : null;
            var position = cameraTransform ? cameraTransform.position : Vector3.zero;
            var rotation = cameraTransform ? cameraTransform.rotation : Quaternion.identity;
            var cameraToWorld = cameraTransform ? cameraTransform.localToWorldMatrix : Matrix4x4.identity;

            var recorded = new RgbdRecordedFrame
            {
                Metadata = new RgbdFrameMetadata
                {
                    frame_id = id,
                    rgb_timestamp = frame.CameraTimestamp,
                    depth_timestamp = frame.Depth.Timestamp,
                    confidence_timestamp = frame.Confidence.Timestamp,
                    timestamp_difference_ms = deltaMs,
                    pose_timestamp = Time.time,
                    rgb_width = Mathf.RoundToInt(frame.ImageResolution.x),
                    rgb_height = Mathf.RoundToInt(frame.ImageResolution.y),
                    rgb_row_stride = Mathf.RoundToInt(frame.ImageResolution.x) * 4,
                    depth_width = frame.Depth.Width,
                    depth_height = frame.Depth.Height,
                    depth_row_stride = depthPlane.RowStride,
                    depth_pixel_stride = depthPlane.PixelStride,
                    confidence_width = frame.Confidence.Width,
                    confidence_height = frame.Confidence.Height,
                    confidence_row_stride = confidencePlane.RowStride,
                    confidence_pixel_stride = confidencePlane.PixelStride,
                    fx = frame.FocalLength.x,
                    fy = frame.FocalLength.y,
                    cx = frame.PrincipalPoint.x,
                    cy = frame.PrincipalPoint.y,
                    has_intrinsics = frame.HasIntrinsics,
                    tracking_state = ARSession.state.ToString(),
                    camera_position = ToArray(position),
                    camera_rotation = ToArray(rotation),
                    camera_to_world_matrix = ToRowMajorArray(cameraToWorld),
                    rgb_file = rgbFile,
                    depth_file = depthFile,
                    confidence_file = confidenceFile,
                    rgb_format = "jpg",
                    depth_format = frame.Depth.Format,
                    confidence_format = frame.Confidence.Format,
                    depth_unit = "meters when provider format is float depth; raw XRCpuImage format is preserved",
                    depth_little_endian = BitConverter.IsLittleEndian,
                    invalid_depth_policy = "provider raw value preserved; consumers should treat 0, NaN, or Inf as invalid depending on depth format",
                    confidence_value_meaning = "ARKit environment depth confidence raw values: 0 low, 1 medium, 2 high when provider uses ARConfidenceLevel",
                    image_orientation = Screen.orientation.ToString(),
                    applied_rotation_flip = "RGB converted to RGBA32 with XRCpuImage.Transformation.MirrorY, then JPEG encoded; depth/confidence raw planes are unrotated and unflipped"
                },
                RgbBytes = rgbBytes,
                DepthBytes = depthPlane.Data,
                ConfidenceBytes = confidencePlane.Data
            };

            if (rgbdRecorder.TryEnqueue(recorded))
            {
                nextRgbdRecorderFrameId++;
                if ((id % 10) == 0)
                    Debug.Log($"[RGBDRecorder] Captured frame {id} rgb={recorded.Metadata.rgb_width}x{recorded.Metadata.rgb_height} depth={recorded.Metadata.depth_width}x{recorded.Metadata.depth_height} delta={deltaMs:0.0}ms");
            }
        }
        finally
        {
            if (frame.Texture)
                Destroy(frame.Texture);
        }
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

            var lastTimestamp = keyframes.Count > 0 ? keyframes[keyframes.Count - 1].Timestamp : Time.time;
            var elapsed = Mathf.Max(0.001f, Time.time - lastTimestamp);
            var speed = distance / elapsed;
            var angularSpeed = angle / elapsed;
            if (speed > maxKeyframeCaptureSpeed || angularSpeed > maxKeyframeAngularSpeed)
            {
                skippedFastMotionFrames++;
                nextKeyframeCaptureTime = Time.unscaledTime + 0.15f;
                SetExportStatus("Move slower for stable depth capture.");
                return;
            }
        }

        if (!TryCreateCameraTexture(out var frame))
            return;

        if (captureDepthForReconstruction &&
            frame.Confidence != null &&
            frame.DepthConfidenceRatio < minimumKeyframeDepthConfidenceRatio)
        {
            if (frame.Texture)
                Destroy(frame.Texture);

            skippedLowConfidenceFrames++;
            nextKeyframeCaptureTime = Time.unscaledTime + 0.15f;
            SetExportStatus("Depth confidence is low. Slow down or aim at brighter matte surfaces.");
            return;
        }

        TrimOldestKeyframeIfNeeded();

        var keyframe = new VisualKeyframe
        {
            Texture = frame.Texture,
            Position = position,
            Rotation = rotation,
            Timestamp = Time.time,
            CameraTimestamp = frame.CameraTimestamp,
            RgbDepthTimestampDeltaSeconds = frame.RgbDepthTimestampDeltaSeconds,
            RgbConfidenceTimestampDeltaSeconds = frame.RgbConfidenceTimestampDeltaSeconds,
            SampleColor = SampleTextureCenter(frame.Texture),
            FieldOfView = frame.FieldOfView,
            Aspect = frame.Aspect,
            HasIntrinsics = frame.HasIntrinsics,
            FocalLength = frame.FocalLength,
            PrincipalPoint = frame.PrincipalPoint,
            ImageResolution = frame.ImageResolution,
            Depth = frame.Depth,
            Confidence = frame.Confidence,
            DepthConfidenceRatio = frame.DepthConfidenceRatio
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

    private bool TryCreateCameraTexture(out CameraTextureFrame frame)
    {
        frame = default;

        if (!arCameraManager.TryAcquireLatestCpuImage(out var image))
            return false;

        Texture2D texture = null;
        CpuImageFrame depth = null;
        CpuImageFrame confidence = null;
        var depthConfidenceRatio = 0f;
        try
        {
            var cameraTimestamp = image.timestamp;
            var rgbDepthDelta = 0d;
            var rgbConfidenceDelta = 0d;

            if (captureDepthForReconstruction && arOcclusionManager)
            {
                if (TryCaptureDepthImage(out depth))
                    rgbDepthDelta = Math.Abs(depth.Timestamp - cameraTimestamp);
                if (TryCaptureConfidenceImage(out confidence))
                {
                    rgbConfidenceDelta = Math.Abs(confidence.Timestamp - cameraTimestamp);
                    depthConfidenceRatio = CalculateMediumOrHighConfidenceRatio(confidence);
                }

                if (requireSynchronizedRgbdKeyframes)
                {
                    if (depth == null || confidence == null)
                    {
                        skippedUnsyncedDepthFrames++;
                        nextKeyframeCaptureTime = Time.unscaledTime + 0.12f;
                        SetExportStatus("Waiting for synchronized RGB-D frames.");
                        return false;
                    }

                    if (rgbDepthDelta > maxRgbDepthTimestampDeltaSeconds ||
                        rgbConfidenceDelta > maxRgbConfidenceTimestampDeltaSeconds)
                    {
                        skippedUnsyncedDepthFrames++;
                        nextKeyframeCaptureTime = Time.unscaledTime + 0.12f;
                        SetExportStatus(
                            $"RGB-D not synchronized yet.\n" +
                            $"rgb-depth {rgbDepthDelta * 1000d:0}ms / rgb-confidence {rgbConfidenceDelta * 1000d:0}ms");
                        return false;
                    }
                }
            }

            var targetWidth = Mathf.Clamp(keyframeTextureWidth, 64, 1920);
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

            frame = new CameraTextureFrame
            {
                Texture = texture,
                CameraTimestamp = cameraTimestamp,
                RgbDepthTimestampDeltaSeconds = rgbDepthDelta,
                RgbConfidenceTimestampDeltaSeconds = rgbConfidenceDelta,
                FieldOfView = arCamera ? arCamera.fieldOfView : 60f,
                Aspect = targetWidth / (float)targetHeight,
                ImageResolution = new Vector2(targetWidth, targetHeight),
                Depth = depth,
                Confidence = confidence,
                DepthConfidenceRatio = depthConfidenceRatio
            };

            if (arCameraManager.TryGetIntrinsics(out var intrinsics) &&
                intrinsics.resolution.x > 0 &&
                intrinsics.resolution.y > 0)
            {
                var scaleX = targetWidth / (float)intrinsics.resolution.x;
                var scaleY = targetHeight / (float)intrinsics.resolution.y;

                frame.HasIntrinsics = true;
                frame.FocalLength = new Vector2(
                    intrinsics.focalLength.x * scaleX,
                    intrinsics.focalLength.y * scaleY);
                frame.PrincipalPoint = new Vector2(
                    intrinsics.principalPoint.x * scaleX,
                    intrinsics.principalPoint.y * scaleY);
            }

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

    private bool TryCaptureDepthImage(out CpuImageFrame frame)
    {
        frame = null;

        if (!arOcclusionManager)
            return false;

        if (!arOcclusionManager.TryAcquireEnvironmentDepthCpuImage(out var image))
            return false;

        try
        {
            frame = CopyCpuImage(image, "environment_depth");
            return frame != null;
        }
        finally
        {
            image.Dispose();
        }
    }

    private bool TryCaptureConfidenceImage(out CpuImageFrame frame)
    {
        frame = null;

        if (!arOcclusionManager)
            return false;

        if (!arOcclusionManager.TryAcquireEnvironmentDepthConfidenceCpuImage(out var image))
            return false;

        try
        {
            frame = CopyCpuImage(image, "environment_depth_confidence");
            return frame != null;
        }
        finally
        {
            image.Dispose();
        }
    }

    private static CpuImageFrame CopyCpuImage(XRCpuImage image, string kind)
    {
        if (!image.valid || image.planeCount <= 0)
            return null;

        var planes = new CpuImagePlaneFrame[image.planeCount];
        for (var i = 0; i < image.planeCount; i++)
        {
            var plane = image.GetPlane(i);
            var data = new byte[plane.data.Length];
            plane.data.CopyTo(data);
            planes[i] = new CpuImagePlaneFrame
            {
                RowStride = plane.rowStride,
                PixelStride = plane.pixelStride,
                Data = data
            };
        }

        return new CpuImageFrame
        {
            Kind = kind,
            Width = image.width,
            Height = image.height,
            Format = image.format.ToString(),
            Timestamp = image.timestamp,
            Planes = planes
        };
    }

    private static float[] ToArray(Vector3 value)
    {
        return new[] { value.x, value.y, value.z };
    }

    private static float[] ToArray(Quaternion value)
    {
        return new[] { value.x, value.y, value.z, value.w };
    }

    private static float[] ToRowMajorArray(Matrix4x4 value)
    {
        return new[]
        {
            value[0, 0], value[0, 1], value[0, 2], value[0, 3],
            value[1, 0], value[1, 1], value[1, 2], value[1, 3],
            value[2, 0], value[2, 1], value[2, 2], value[2, 3],
            value[3, 0], value[3, 1], value[3, 2], value[3, 3]
        };
    }

    private static float CalculateMediumOrHighConfidenceRatio(CpuImageFrame confidence)
    {
        if (confidence == null ||
            confidence.Width <= 0 ||
            confidence.Height <= 0 ||
            confidence.Planes == null ||
            confidence.Planes.Length == 0 ||
            confidence.Planes[0].Data == null)
        {
            return 0f;
        }

        var plane = confidence.Planes[0];
        var data = plane.Data;
        var rowStride = Mathf.Max(1, plane.RowStride);
        var pixelStride = Mathf.Max(1, plane.PixelStride);
        var total = 0;
        var mediumOrHigh = 0;

        for (var y = 0; y < confidence.Height; y++)
        {
            var row = y * rowStride;
            for (var x = 0; x < confidence.Width; x++)
            {
                var offset = row + x * pixelStride;
                if (offset < 0 || offset >= data.Length)
                    continue;

                total++;
                if (data[offset] >= 1)
                    mediumOrHigh++;
            }
        }

        return total > 0 ? mediumOrHigh / (float)total : 0f;
    }

    private bool TryGetCenterSurfacePoint(out Vector3 surfacePoint)
    {
        surfacePoint = default;

        if (!arCamera)
            return false;

        var ray = arCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (!Physics.Raycast(ray, out var hit, surfacePointMaxDistance, surfacePointLayers, QueryTriggerInteraction.Ignore))
            return TryRaycastLiveMesh(ray, surfacePointMaxDistance, out surfacePoint);

        surfacePoint = hit.point;
        return true;
    }

    private bool TryRaycastLiveMesh(Ray ray, float maxDistance, out Vector3 surfacePoint)
    {
        surfacePoint = default;

        if (!meshManager)
            return false;

        var bestDistance = maxDistance;
        var found = false;

        foreach (var meshFilter in meshManager.meshes)
        {
            if (!meshFilter || !meshFilter.sharedMesh)
                continue;

            var mesh = meshFilter.sharedMesh;
            var vertices = mesh.vertices;
            var triangles = mesh.triangles;
            if (vertices == null || triangles == null || triangles.Length < 3)
                continue;

            for (var i = 0; i < triangles.Length; i += 3)
            {
                var a = meshFilter.transform.TransformPoint(vertices[triangles[i]]);
                var b = meshFilter.transform.TransformPoint(vertices[triangles[i + 1]]);
                var c = meshFilter.transform.TransformPoint(vertices[triangles[i + 2]]);
                if (!RayIntersectsTriangle(ray, a, b, c, out var distance))
                    continue;

                if (distance <= 0f || distance >= bestDistance)
                    continue;

                bestDistance = distance;
                surfacePoint = ray.GetPoint(distance);
                found = true;
            }
        }

        return found;
    }

    private static bool RayIntersectsTriangle(Ray ray, Vector3 a, Vector3 b, Vector3 c, out float distance)
    {
        distance = 0f;
        var edge1 = b - a;
        var edge2 = c - a;
        var h = Vector3.Cross(ray.direction, edge2);
        var determinant = Vector3.Dot(edge1, h);
        if (Mathf.Abs(determinant) < 0.000001f)
            return false;

        var inverseDeterminant = 1f / determinant;
        var s = ray.origin - a;
        var u = inverseDeterminant * Vector3.Dot(s, h);
        if (u < 0f || u > 1f)
            return false;

        var q = Vector3.Cross(s, edge1);
        var v = inverseDeterminant * Vector3.Dot(ray.direction, q);
        if (v < 0f || u + v > 1f)
            return false;

        distance = inverseDeterminant * Vector3.Dot(edge2, q);
        return distance > 0.0001f;
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
        if (!previewRoot)
            return;

        var visualRoot = new GameObject("Visual Keyframe Overlay");
        visualRoot.transform.SetParent(previewRoot.transform, false);

        if (showCoverageOverlayInPreview)
            BuildCoverageOverlay(visualRoot.transform, bounds, isLiveOverlay: false);

        if (keyframes.Count == 0)
            return;

        if (showScanPathInPreview)
            BuildScanPath(visualRoot.transform);

        if (showSurfaceColorPointsInPreview)
            BuildSurfacePoints(visualRoot.transform, bounds);

        if (showPhotoCardsInPreview)
            BuildThumbnailCards(visualRoot.transform, bounds);
    }

    private void UpdateLiveCoverageOverlayIfNeeded()
    {
        if (!showCoverageOverlayWhileScanning || Time.unscaledTime < nextCoverageOverlayRefreshTime)
            return;

        nextCoverageOverlayRefreshTime = Time.unscaledTime + Mathf.Max(0.2f, coverageOverlayRefreshSeconds);

        if (keyframes.Count == 0 || !TryCalculateLiveMeshBounds(out var bounds))
            return;

        BuildCoverageOverlay(null, bounds, isLiveOverlay: true);
        UpdateStats(force: true);
    }

    private void BuildCoverageOverlay(Transform parent, Bounds bounds, bool isLiveOverlay)
    {
        if (isLiveOverlay)
            DestroyLiveCoverageOverlay();

        var cellSize = Mathf.Clamp(coverageCellSize, 0.08f, 1.2f);
        var cells = BuildCoverageCells(bounds, cellSize);
        var orderedCells = new List<CoverageCell>(cells.Values);
        orderedCells.Sort((left, right) =>
        {
            var viewCompare = left.ViewCount.CompareTo(right.ViewCount);
            if (viewCompare != 0)
                return viewCompare;

            return right.SampleCount.CompareTo(left.SampleCount);
        });

        var root = new GameObject(isLiveOverlay ? "Live Scan Coverage Overlay" : "Preview Scan Coverage Overlay");
        if (parent)
            root.transform.SetParent(parent, false);

        if (isLiveOverlay)
            liveCoverageRoot = root;

        var summary = new CoverageSummary();
        var markerLimit = Mathf.Max(0, maxCoverageMarkers);
        for (var i = 0; i < orderedCells.Count; i++)
        {
            var cell = orderedCells[i];
            summary.totalCells++;
            summary.totalObservedViews += cell.ViewCount;

            if (cell.ViewCount >= Mathf.Max(goodCoverageViewCount, fairCoverageViewCount))
                summary.goodCells++;
            else if (cell.ViewCount >= Mathf.Max(1, fairCoverageViewCount))
                summary.fairCells++;
            else
                summary.weakCells++;

            if (i >= markerLimit)
                continue;

            CreateCoverageMarker(root.transform, cell, cellSize);
        }

        latestCoverageSummary = summary;

        if (orderedCells.Count == 0 && isLiveOverlay)
            DestroyLiveCoverageOverlay();
    }

    private Dictionary<CoverageCellKey, CoverageCell> BuildCoverageCells(Bounds bounds, float cellSize)
    {
        var cells = new Dictionary<CoverageCellKey, CoverageCell>(Mathf.Max(32, maxCoverageMarkers * 2));
        AddDetectedPlaneCoverageCells(cells, bounds, cellSize);
        AddMeshCoverageCells(cells, bounds, cellSize);

        foreach (var item in cells.Values)
        {
            item.Position = item.PositionSum / Mathf.Max(1, item.SampleCount);
            item.Normal = item.NormalSum.sqrMagnitude > 0.0001f ? item.NormalSum.normalized : Vector3.up;
            item.ViewCount = CountObservedViews(item.Position, item.Normal);
        }

        return cells;
    }

    private void AddDetectedPlaneCoverageCells(Dictionary<CoverageCellKey, CoverageCell> cells, Bounds bounds, float cellSize)
    {
        if (!planeManager)
            return;

        var candidateLimit = Mathf.Max(maxCoverageMarkers * 5, maxCoverageMarkers + 20);
        foreach (var plane in planeManager.trackables)
        {
            if (!plane || plane.trackingState == TrackingState.None || cells.Count >= candidateLimit)
                continue;

            var size = plane.size;
            if (size.x <= 0.01f || size.y <= 0.01f)
                size = plane.extents * 2f;

            if (size.x <= 0.01f || size.y <= 0.01f)
                continue;

            var xCells = Mathf.Clamp(Mathf.CeilToInt(size.x / cellSize), 1, 32);
            var zCells = Mathf.Clamp(Mathf.CeilToInt(size.y / cellSize), 1, 32);
            var normal = plane.transform.up;

            for (var x = 0; x < xCells && cells.Count < candidateLimit; x++)
            {
                for (var z = 0; z < zCells && cells.Count < candidateLimit; z++)
                {
                    var localX = plane.center.x - size.x * 0.5f + (x + 0.5f) * (size.x / xCells);
                    var localZ = plane.center.y - size.y * 0.5f + (z + 0.5f) * (size.y / zCells);
                    var position = plane.transform.TransformPoint(new Vector3(localX, 0f, localZ));

                    if (!IsNearBounds(bounds, position, cellSize * 2f))
                        continue;

                    AddCoverageCell(cells, position, normal, cellSize, fromPlane: true);
                }
            }
        }
    }

    private void AddMeshCoverageCells(Dictionary<CoverageCellKey, CoverageCell> cells, Bounds bounds, float cellSize)
    {
        if (!meshManager)
            return;

        var totalTriangles = 0;
        foreach (var meshFilter in meshManager.meshes)
        {
            if (meshFilter && meshFilter.sharedMesh)
                totalTriangles += meshFilter.sharedMesh.triangles.Length / 3;
        }

        if (totalTriangles == 0)
            return;

        var candidateLimit = Mathf.Max(maxCoverageMarkers * 5, maxCoverageMarkers + 20);
        var stride = Mathf.Max(1, Mathf.CeilToInt(totalTriangles / (float)Mathf.Max(1, candidateLimit)));

        foreach (var meshFilter in meshManager.meshes)
        {
            if (!meshFilter || !meshFilter.sharedMesh || cells.Count >= candidateLimit)
                continue;

            var mesh = meshFilter.sharedMesh;
            var vertices = mesh.vertices;
            var triangles = mesh.triangles;
            if (vertices == null || triangles == null || triangles.Length < 3)
                continue;

            for (var triangleIndex = 0; triangleIndex < triangles.Length / 3 && cells.Count < candidateLimit; triangleIndex += stride)
            {
                var i = triangleIndex * 3;
                var a = meshFilter.transform.TransformPoint(vertices[triangles[i]]);
                var b = meshFilter.transform.TransformPoint(vertices[triangles[i + 1]]);
                var c = meshFilter.transform.TransformPoint(vertices[triangles[i + 2]]);

                if (TriangleArea(a, b, c) < minimumTriangleArea)
                    continue;

                var normal = Vector3.Cross(b - a, c - a).normalized;
                if (!IsFinite(normal))
                    continue;

                var position = (a + b + c) / 3f;
                if (!IsNearBounds(bounds, position, cellSize))
                    continue;

                AddCoverageCell(cells, position, normal, cellSize, fromPlane: false);
            }
        }
    }

    private static void AddCoverageCell(Dictionary<CoverageCellKey, CoverageCell> cells, Vector3 position, Vector3 normal, float cellSize, bool fromPlane)
    {
        if (!IsFinite(position) || !IsFinite(normal))
            return;

        var key = new CoverageCellKey(position, normal, cellSize);
        if (!cells.TryGetValue(key, out var cell))
        {
            cell = new CoverageCell();
            cells.Add(key, cell);
        }

        cell.PositionSum += position;
        cell.NormalSum += normal;
        cell.SampleCount++;
        cell.FromPlane |= fromPlane;
    }

    private int CountObservedViews(Vector3 position, Vector3 normal)
    {
        var count = 0;
        var normalizedNormal = normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.up;
        var maxDistance = Mathf.Max(surfacePointMaxDistance, 4f);

        foreach (var keyframe in keyframes)
        {
            var cameraLocal = Quaternion.Inverse(keyframe.Rotation) * (position - keyframe.Position);
            if (cameraLocal.z <= 0.15f || cameraLocal.z > maxDistance)
                continue;

            if (!TryProjectCoverageUv(cameraLocal, keyframe, out var u, out var v))
                continue;

            if (u < 0.04f || u > 0.96f || v < 0.04f || v > 0.96f)
                continue;

            var viewDirection = (keyframe.Position - position).normalized;
            if (Mathf.Abs(Vector3.Dot(normalizedNormal, viewDirection)) < 0.08f)
                continue;

            count++;
        }

        return count;
    }

    private static bool TryProjectCoverageUv(Vector3 cameraLocal, VisualKeyframe keyframe, out float u, out float v)
    {
        u = 0f;
        v = 0f;

        if (keyframe.HasIntrinsics && keyframe.ImageResolution.x > 1f && keyframe.ImageResolution.y > 1f)
        {
            var pixelX = keyframe.FocalLength.x * (cameraLocal.x / cameraLocal.z) + keyframe.PrincipalPoint.x;
            var pixelYFromTop = keyframe.PrincipalPoint.y - keyframe.FocalLength.y * (cameraLocal.y / cameraLocal.z);
            u = pixelX / keyframe.ImageResolution.x;
            v = 1f - (pixelYFromTop / keyframe.ImageResolution.y);
            return true;
        }

        var aspect = keyframe.Aspect > 0.01f ? keyframe.Aspect : 1f;
        var fov = Mathf.Clamp(keyframe.FieldOfView > 1f ? keyframe.FieldOfView : 60f, 20f, 120f);
        var tanY = Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
        var tanX = tanY * aspect;
        u = 0.5f + cameraLocal.x / (2f * cameraLocal.z * tanX);
        v = 0.5f + cameraLocal.y / (2f * cameraLocal.z * tanY);
        return true;
    }

    private void CreateCoverageMarker(Transform parent, CoverageCell cell, float cellSize)
    {
        var marker = new GameObject($"Coverage {cell.ViewCount}");
        marker.transform.SetParent(parent, false);
        marker.transform.position = cell.Position + cell.Normal * Mathf.Max(0f, coverageMarkerSurfaceOffset);
        marker.transform.rotation = Quaternion.LookRotation(cell.Normal, GetCoverageMarkerUp(cell.Normal));
        marker.transform.localScale = Vector3.one * Mathf.Clamp(cellSize * 0.86f, 0.06f, 1.1f);

        marker.AddComponent<MeshFilter>().sharedMesh = GetCoverageQuadMesh();
        var renderer = marker.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = GetCoverageMaterial(cell.ViewCount);
    }

    private Material GetCoverageMaterial(int viewCount)
    {
        if (viewCount >= Mathf.Max(goodCoverageViewCount, fairCoverageViewCount))
            return goodCoverageMaterial ? goodCoverageMaterial : (goodCoverageMaterial = CreateCoverageMaterial(new Color(0.18f, 0.95f, 0.42f, 0.42f), "Good"));

        if (viewCount >= Mathf.Max(1, fairCoverageViewCount))
            return fairCoverageMaterial ? fairCoverageMaterial : (fairCoverageMaterial = CreateCoverageMaterial(new Color(1f, 0.75f, 0.16f, 0.52f), "Fair"));

        return weakCoverageMaterial ? weakCoverageMaterial : (weakCoverageMaterial = CreateCoverageMaterial(new Color(1f, 0.14f, 0.08f, 0.62f), "Weak"));
    }

    private static Material CreateCoverageMaterial(Color color, string label)
    {
        var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default");
        var material = new Material(shader)
        {
            name = $"Runtime Coverage {label} Material",
            renderQueue = (int)RenderQueue.Transparent
        };

        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);

        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
        if (material.HasProperty("_Cull"))
            material.SetFloat("_Cull", (float)CullMode.Off);

        return material;
    }

    private static Vector3 GetCoverageMarkerUp(Vector3 normal)
    {
        return Mathf.Abs(Vector3.Dot(normal.normalized, Vector3.up)) > 0.92f ? Vector3.forward : Vector3.up;
    }

    private static Mesh GetCoverageQuadMesh()
    {
        if (coverageQuadMesh)
            return coverageQuadMesh;

        coverageQuadMesh = new Mesh
        {
            name = "Runtime Coverage Quad Mesh"
        };

        coverageQuadMesh.SetVertices(new[]
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3(0.5f, -0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f),
            new Vector3(0.5f, 0.5f, 0f)
        });
        coverageQuadMesh.SetTriangles(new[] { 0, 2, 1, 2, 3, 1 }, 0);
        coverageQuadMesh.RecalculateNormals();
        coverageQuadMesh.RecalculateBounds();
        return coverageQuadMesh;
    }

    private bool TryCalculateLiveMeshBounds(out Bounds bounds)
    {
        bounds = default;
        var hasBounds = false;

        if (!meshManager)
            return false;

        foreach (var meshFilter in meshManager.meshes)
        {
            if (!meshFilter || !meshFilter.sharedMesh)
                continue;

            var mesh = meshFilter.sharedMesh;
            var vertices = mesh.vertices;
            if (vertices == null || vertices.Length == 0)
                continue;

            foreach (var vertex in vertices)
            {
                var world = meshFilter.transform.TransformPoint(vertex);
                if (!hasBounds)
                {
                    bounds = new Bounds(world, Vector3.zero);
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(world);
                }
            }
        }

        return hasBounds;
    }

    private static bool IsNearBounds(Bounds bounds, Vector3 position, float tolerance)
    {
        var expanded = bounds;
        expanded.Expand(Mathf.Max(0f, tolerance) * 2f);
        return expanded.Contains(position);
    }

    private void DestroyLiveCoverageOverlay()
    {
        if (liveCoverageRoot)
            Destroy(liveCoverageRoot);

        liveCoverageRoot = null;
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

            var marker = new GameObject("Visual Surface Point");
            marker.name = "Visual Surface Point";
            marker.transform.SetParent(parent, false);
            marker.transform.position = keyframe.SurfacePoint;
            marker.transform.localScale = Vector3.one * radius;

            marker.AddComponent<MeshFilter>().sharedMesh = GetSurfacePointMesh();

            var renderer = marker.AddComponent<MeshRenderer>();
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

            var card = new GameObject("Visual Keyframe Card");
            card.name = "Visual Keyframe Card";
            card.transform.SetParent(parent, false);
            card.transform.position = GetPreviewCardPosition(keyframe, bounds, radius);

            var aspect = keyframe.Texture.width / (float)keyframe.Texture.height;
            card.transform.localScale = new Vector3(cardHeight * aspect, cardHeight, 1f);
            card.transform.rotation = Quaternion.LookRotation(previewCenter - card.transform.position, Vector3.up);

            card.AddComponent<MeshFilter>().sharedMesh = GetThumbnailQuadMesh();

            var renderer = card.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = CreateKeyframeMaterial(keyframe.Texture);
        }
    }

    private static Mesh GetSurfacePointMesh()
    {
        if (surfacePointMesh)
            return surfacePointMesh;

        surfacePointMesh = new Mesh
        {
            name = "Runtime Surface Point Mesh"
        };

        var vertices = new[]
        {
            Vector3.up,
            Vector3.down,
            Vector3.left,
            Vector3.right,
            Vector3.forward,
            Vector3.back
        };
        var triangles = new[]
        {
            0, 4, 3,
            0, 3, 5,
            0, 5, 2,
            0, 2, 4,
            1, 3, 4,
            1, 5, 3,
            1, 2, 5,
            1, 4, 2
        };

        surfacePointMesh.SetVertices(vertices);
        surfacePointMesh.SetTriangles(triangles, 0);
        surfacePointMesh.RecalculateNormals();
        surfacePointMesh.RecalculateBounds();
        return surfacePointMesh;
    }

    private static Mesh GetThumbnailQuadMesh()
    {
        if (thumbnailQuadMesh)
            return thumbnailQuadMesh;

        thumbnailQuadMesh = new Mesh
        {
            name = "Runtime Thumbnail Quad Mesh"
        };

        thumbnailQuadMesh.SetVertices(new[]
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3(0.5f, -0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f),
            new Vector3(0.5f, 0.5f, 0f)
        });
        thumbnailQuadMesh.SetUVs(0, new[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f)
        });
        thumbnailQuadMesh.SetTriangles(new[] { 0, 2, 1, 2, 3, 1 }, 0);
        thumbnailQuadMesh.RecalculateNormals();
        thumbnailQuadMesh.RecalculateBounds();
        return thumbnailQuadMesh;
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
        var desiredShader = previewHasProjectedColors
            ? Shader.Find("MemoAnchor/Preview Vertex Colors") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit")
            : Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Unlit");

        if (previewMaterial && previewMaterial.shader == desiredShader)
            return previewMaterial;

        previewMaterial = new Material(desiredShader)
        {
            name = "Runtime Clean Map Preview Material"
        };

        var color = previewHasProjectedColors ? Color.white : new Color(0.58f, 0.7f, 0.74f, 1f);
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

    private bool TryApplyProjectedKeyframeColors(Mesh mesh)
    {
        if (!mesh || keyframes.Count == 0)
            return false;

        var vertices = mesh.vertices;
        var normals = mesh.normals;
        if (vertices == null || vertices.Length == 0 || normals == null || normals.Length != vertices.Length)
            return false;

        var colors = new Color[vertices.Length];
        var coloredCount = 0;
        var fallback = EstimateAverageKeyframeColor();

        for (var i = 0; i < vertices.Length; i++)
        {
            if (TrySampleProjectedKeyframeColor(vertices[i], normals[i], out var color))
            {
                colors[i] = color;
                coloredCount++;
            }
            else
            {
                colors[i] = fallback;
            }
        }

        if (coloredCount == 0)
            return false;

        previewProjectedColorCoverage = coloredCount / (float)vertices.Length;
        mesh.colors = colors;
        return true;
    }

    private Color EstimateAverageKeyframeColor()
    {
        var sum = Vector3.zero;
        var count = 0;

        foreach (var keyframe in keyframes)
        {
            if (!keyframe.Texture)
                continue;

            var color = keyframe.SampleColor;
            sum += new Vector3(color.r, color.g, color.b);
            count++;
        }

        if (count == 0)
            return new Color(0.58f, 0.7f, 0.74f, 1f);

        var average = sum / count;
        return new Color(average.x, average.y, average.z, 1f);
    }

    private bool TrySampleProjectedKeyframeColor(Vector3 worldPosition, Vector3 normal, out Color color)
    {
        color = Color.white;
        var bestScore = float.NegativeInfinity;
        var found = false;

        foreach (var keyframe in keyframes)
        {
            if (!keyframe.Texture)
                continue;

            var cameraLocal = Quaternion.Inverse(keyframe.Rotation) * (worldPosition - keyframe.Position);
            if (cameraLocal.z <= 0.12f || cameraLocal.z > 6.5f)
                continue;

            if (!TryProjectToKeyframeUv(cameraLocal, keyframe, out var u, out var v))
                continue;

            if (u < 0.06f || u > 0.94f || v < 0.06f || v > 0.94f)
                continue;

            var centerDistance = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f));
            var centerScore = 1f - Mathf.Clamp01(centerDistance / 0.7f);
            var distanceScore = 1f / (0.25f + cameraLocal.z);
            var viewDirection = (keyframe.Position - worldPosition).normalized;
            var facingScore = normal.sqrMagnitude > 0.0001f ? Mathf.Clamp01(Mathf.Abs(Vector3.Dot(normal.normalized, viewDirection))) : 0.5f;
            if (facingScore < 0.18f)
                continue;

            var score = centerScore * 2.2f + distanceScore + facingScore * 0.6f;

            if (score <= bestScore)
                continue;

            bestScore = score;
            color = keyframe.Texture.GetPixelBilinear(u, v);
            color.a = 1f;
            found = true;
        }

        return found;
    }

    private static bool TryProjectToKeyframeUv(Vector3 cameraLocal, VisualKeyframe keyframe, out float u, out float v)
    {
        u = 0f;
        v = 0f;

        if (keyframe.HasIntrinsics && keyframe.ImageResolution.x > 1f && keyframe.ImageResolution.y > 1f)
        {
            var pixelX = keyframe.FocalLength.x * (cameraLocal.x / cameraLocal.z) + keyframe.PrincipalPoint.x;
            var pixelYFromTop = keyframe.PrincipalPoint.y - keyframe.FocalLength.y * (cameraLocal.y / cameraLocal.z);

            u = pixelX / keyframe.ImageResolution.x;
            v = 1f - (pixelYFromTop / keyframe.ImageResolution.y);
            return true;
        }

        var fov = Mathf.Clamp(keyframe.FieldOfView > 1f ? keyframe.FieldOfView : 60f, 20f, 120f);
        var aspect = keyframe.Aspect > 0.01f ? keyframe.Aspect : keyframe.Texture.width / (float)keyframe.Texture.height;
        var tanY = Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
        var tanX = tanY * aspect;

        u = 0.5f + cameraLocal.x / (2f * cameraLocal.z * tanX);
        v = 0.5f + cameraLocal.y / (2f * cameraLocal.z * tanY);
        return true;
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

        if (planeManager)
            planeManager.enabled = enabled;

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

            if (meshFilter.TryGetComponent<MeshRenderer>(out var renderer))
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

    private async Awaitable EnhancePreviewWithGeminiAsync(Bounds bounds)
    {
        geminiEnhancementRunning = true;

        var client = GetOrCreateGeminiClient();
        if (!client)
        {
            SetExportStatus("Gemini skipped: client missing.");
            geminiEnhancementRunning = false;
            return;
        }

        SemanticScanPackage scanPackage;
        List<GeminiImageInput> images;
        string scanFolder;

        try
        {
            SetExportStatus("Saving scan JSON and keyframes...");
            scanPackage = BuildSemanticScanPackage(bounds, out images, out scanFolder);
            Directory.CreateDirectory(scanFolder);
            File.WriteAllText(Path.Combine(scanFolder, "scan_payload.json"), JsonUtility.ToJson(scanPackage, true));
        }
        catch (Exception ex)
        {
            SetExportStatus("Gemini skipped: scan package save failed.");
            Debug.LogException(ex);
            geminiEnhancementRunning = false;
            return;
        }

        var rawJson = string.Empty;
        GeminiSemanticMapResult semanticResult = null;
        var error = string.Empty;

        SetExportStatus($"Gemini analyzing semantic map...\nImages: {images.Count}");
        await client.GenerateSemanticMapAsync(
            scanPackage,
            images,
            (json, result) =>
            {
                rawJson = json;
                semanticResult = result;
            },
            message => error = message,
            (attempt, maxAttempts, responseCode, delaySeconds) =>
            {
                SetExportStatus(
                    $"Gemini temporarily unavailable ({responseCode}).\n" +
                    $"Retry {attempt + 1}/{maxAttempts} in {delaySeconds:0.0}s...");
            });

        if (!string.IsNullOrWhiteSpace(error))
        {
            SetExportStatus("Gemini failed. Showing LiDAR map only.");
            Debug.LogWarning($"[ARKitMeshScanController] {error}");
            geminiEnhancementRunning = false;
            return;
        }

        try
        {
            File.WriteAllText(Path.Combine(scanFolder, "gemini_semantic_map.json"), rawJson);
            ApplyGeminiSemanticMap(semanticResult, bounds);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            SetExportStatus("Gemini parsed, but overlay failed.");
            geminiEnhancementRunning = false;
            return;
        }

        var labelCount = (semanticResult?.objects?.Length ?? 0) + (semanticResult?.zones?.Length ?? 0);
        SetExportStatus($"Gemini semantic map ready.\nLabels: {labelCount}");
        geminiEnhancementRunning = false;
    }

    private GeminiSemanticMapClient GetOrCreateGeminiClient()
    {
        if (geminiClient)
            return geminiClient;

        if (!TryGetComponent<GeminiSemanticMapClient>(out geminiClient))
            geminiClient = gameObject.AddComponent<GeminiSemanticMapClient>();

        return geminiClient;
    }

    private SemanticScanPackage BuildSemanticScanPackage(
        Bounds bounds,
        out List<GeminiImageInput> images,
        out string scanFolder)
    {
        var id = string.IsNullOrWhiteSpace(scanId) ? DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") : scanId;
        scanFolder = Path.Combine(Application.persistentDataPath, "GeminiSemanticScans", id);
        Directory.CreateDirectory(scanFolder);

        var keyframeDtos = new List<CameraKeyframeDto>(keyframes.Count);
        images = new List<GeminiImageInput>(Mathf.Min(keyframes.Count, Mathf.Max(0, geminiMaxImages)));

        var imageLimit = Mathf.Clamp(geminiMaxImages, 0, Mathf.Max(0, keyframes.Count));
        var keyframeStride = imageLimit > 0 ? Mathf.Max(1, Mathf.CeilToInt(keyframes.Count / (float)imageLimit)) : 1;

        for (var i = 0; i < keyframes.Count; i++)
        {
            var keyframe = keyframes[i];
            var fileName = $"keyframe_{i + 1:D3}.jpg";
            var imagePath = Path.Combine(scanFolder, fileName);

            if (keyframe.Texture)
            {
                var jpg = keyframe.Texture.EncodeToJPG(75);
                File.WriteAllBytes(imagePath, jpg);

                if (images.Count < imageLimit && i % keyframeStride == 0)
                {
                    images.Add(new GeminiImageInput
                    {
                        FileName = fileName,
                        FilePath = imagePath,
                        MimeType = "image/jpeg",
                        Bytes = jpg
                    });
                }
            }

            keyframeDtos.Add(new CameraKeyframeDto
            {
                id = $"keyframe_{i + 1:D3}",
                imageFileName = fileName,
                imagePath = imagePath,
                timestampSeconds = keyframe.Timestamp,
                position = Vector3Dto.From(keyframe.Position),
                rotation = QuaternionDto.From(keyframe.Rotation),
                sampleColor = ColorDto.From(keyframe.SampleColor),
                hasSurfacePoint = keyframe.HasSurfacePoint,
                surfacePoint = Vector3Dto.From(keyframe.HasSurfacePoint ? keyframe.SurfacePoint : keyframe.Position)
            });
        }

        var meshSummary = BuildMeshSummary();

        return new SemanticScanPackage
        {
            schemaVersion = "memoanchor.semantic-scan.v1",
            scanId = id,
            capturedAtUtc = DateTime.UtcNow.ToString("o"),
            durationSeconds = Mathf.Max(0f, Time.time - scanStartTime),
            bounds = new BoundsDto
            {
                center = Vector3Dto.From(bounds.center),
                size = Vector3Dto.From(bounds.size),
                min = Vector3Dto.From(bounds.min),
                max = Vector3Dto.From(bounds.max)
            },
            mesh = meshSummary,
            keyframes = keyframeDtos.ToArray(),
            meshSamples = BuildMeshSamples(Mathf.Max(0, geminiMeshSampleLimit)).ToArray()
        };
    }

    private ScanQualityReportDto BuildScanQualityReport(Bounds bounds)
    {
        var meshSummary = BuildMeshSummary();
        return BuildScanQualityReport(bounds, meshSummary.meshCount, meshSummary.vertexCount, meshSummary.triangleCount);
    }

    private ScanQualityReportDto BuildScanQualityReport(Bounds? bounds, int meshCount, int vertexCount, int triangleCount)
    {
        var duration = scanMode == ScanMode.Ready ? 0f : Mathf.Max(0f, Time.time - scanStartTime);
        var keyframeCount = keyframes.Count;
        var depthFrameCount = 0;
        var confidenceFrameCount = 0;
        var surfacePointCount = 0;
        var depthConfidenceSum = 0f;
        var rgbDepthSyncDeltaSumMs = 0f;
        var maxRgbDepthSyncDeltaMs = 0f;
        var synchronizedFrameCount = 0;

        foreach (var keyframe in keyframes)
        {
            if (keyframe.Depth != null)
                depthFrameCount++;
            if (keyframe.Confidence != null)
                confidenceFrameCount++;
            if (keyframe.HasSurfacePoint)
                surfacePointCount++;
            depthConfidenceSum += keyframe.DepthConfidenceRatio;

            if (keyframe.CameraTimestamp > 0d && keyframe.Depth != null)
            {
                var deltaMs = (float)(Math.Abs(keyframe.RgbDepthTimestampDeltaSeconds) * 1000d);
                rgbDepthSyncDeltaSumMs += deltaMs;
                maxRgbDepthSyncDeltaMs = Mathf.Max(maxRgbDepthSyncDeltaMs, deltaMs);
                synchronizedFrameCount++;
            }
        }

        var averageDepthConfidence = keyframeCount > 0 ? depthConfidenceSum / keyframeCount : 0f;
        var averageRgbDepthSyncDeltaMs = synchronizedFrameCount > 0 ? rgbDepthSyncDeltaSumMs / synchronizedFrameCount : 0f;
        var surfaceHitRatio = keyframeCount > 0 ? surfacePointCount / (float)keyframeCount : 0f;
        var cameraPathMeters = CalculateKeyframePathLength();
        var cameraSpreadMeters = CalculateKeyframeSpread();
        var planeCount = CountDetectedPlanes();
        var coverageTotalCells = latestCoverageSummary?.totalCells ?? 0;
        var coverageWeakCells = latestCoverageSummary?.weakCells ?? 0;
        var coverageFairCells = latestCoverageSummary?.fairCells ?? 0;
        var coverageGoodCells = latestCoverageSummary?.goodCells ?? 0;
        var coverageWeakRatio = coverageTotalCells > 0 ? coverageWeakCells / (float)coverageTotalCells : 0f;

        var durationScore = ScoreRatio(duration, recommendedMinScanSeconds) * 10f;
        var keyframeScore = ScoreRatio(keyframeCount, recommendedMinKeyframes) * 20f;
        var pathScore = ScoreRatio(cameraPathMeters, recommendedMinCameraPathMeters) * 15f;
        var confidenceScore = ScoreRatio(averageDepthConfidence, recommendedMinDepthConfidenceRatio) * 20f;
        var surfaceScore = ScoreRatio(surfaceHitRatio, recommendedMinSurfaceHitRatio) * 10f;
        var meshScore = ScoreRatio(triangleCount, recommendedMinMeshTriangles) * 15f;
        var planeScore = ScoreRatio(planeCount, recommendedMinDetectedPlanes) * 10f;
        var score = Mathf.Clamp(durationScore + keyframeScore + pathScore + confidenceScore + surfaceScore + meshScore + planeScore, 0f, 100f);

        var guidance = BuildScanGuidance(
            duration,
            keyframeCount,
            cameraPathMeters,
            averageDepthConfidence,
            surfaceHitRatio,
            triangleCount,
            planeCount,
            coverageTotalCells,
            coverageWeakRatio,
            averageRgbDepthSyncDeltaMs);

        return new ScanQualityReportDto
        {
            score = score,
            grade = score >= 78f ? "good" : score >= 55f ? "fair" : "poor",
            primaryGuidance = guidance.Count > 0 ? guidance[0] : "Coverage looks usable. Stop when all target surfaces are scanned.",
            guidance = guidance.ToArray(),
            durationSeconds = duration,
            keyframeCount = keyframeCount,
            depthFrameCount = depthFrameCount,
            confidenceFrameCount = confidenceFrameCount,
            averageDepthConfidence = averageDepthConfidence,
            averageRgbdTimestampDeltaMs = averageRgbDepthSyncDeltaMs,
            maxRgbdTimestampDeltaMs = maxRgbDepthSyncDeltaMs,
            skippedFastMotionFrames = skippedFastMotionFrames,
            skippedUnsyncedDepthFrames = skippedUnsyncedDepthFrames,
            skippedLowConfidenceFrames = skippedLowConfidenceFrames,
            surfacePointRatio = surfaceHitRatio,
            cameraPathMeters = cameraPathMeters,
            cameraSpreadMeters = cameraSpreadMeters,
            meshCount = meshCount,
            vertexCount = vertexCount,
            triangleCount = triangleCount,
            detectedPlaneCount = planeCount,
            coverageCellCount = coverageTotalCells,
            coverageWeakCellCount = coverageWeakCells,
            coverageFairCellCount = coverageFairCells,
            coverageGoodCellCount = coverageGoodCells,
            coverageWeakCellRatio = coverageWeakRatio,
            bounds = bounds.HasValue
                ? new BoundsDto
                {
                    center = Vector3Dto.From(bounds.Value.center),
                    size = Vector3Dto.From(bounds.Value.size),
                    min = Vector3Dto.From(bounds.Value.min),
                    max = Vector3Dto.From(bounds.Value.max)
                }
                : null
        };
    }

    private List<string> BuildScanGuidance(
        float duration,
        int keyframeCount,
        float cameraPathMeters,
        float averageDepthConfidence,
        float surfaceHitRatio,
        int triangleCount,
        int planeCount,
        int coverageCellCount,
        float coverageWeakCellRatio,
        float averageRgbdTimestampDeltaMs)
    {
        var guidance = new List<string>(6);

        if (duration < recommendedMinScanSeconds)
            guidance.Add($"Keep scanning {recommendedMinScanSeconds - duration:0}s more.");

        if (keyframeCount < recommendedMinKeyframes)
            guidance.Add("Move slowly across each wall to collect more overlapping views.");

        if (cameraPathMeters < recommendedMinCameraPathMeters)
            guidance.Add("Walk along the wall/corners; avoid only rotating in place.");

        if (averageDepthConfidence < recommendedMinDepthConfidenceRatio)
            guidance.Add("Point at well-lit matte surfaces and slow down until depth is stable.");

        if (requireSynchronizedRgbdKeyframes && averageRgbdTimestampDeltaMs > maxRgbDepthTimestampDeltaSeconds * 750f)
            guidance.Add("Keep moving slowly; RGB-D synchronization is drifting.");

        if (surfaceHitRatio < recommendedMinSurfaceHitRatio)
            guidance.Add("Keep the center of the camera on real surfaces, not empty space.");

        if (coverageCellCount > 0 && coverageWeakCellRatio > 0.35f)
            guidance.Add("Rescan the red coverage cells from a second angle before stopping.");

        if (triangleCount < recommendedMinMeshTriangles)
            guidance.Add("Rescan sparse surfaces until the mesh becomes denser.");

        if (planeCount < recommendedMinDetectedPlanes)
            guidance.Add("Sweep the floor and large walls slowly so AR planes are detected.");

        return guidance;
    }

    private static float ScoreRatio(float value, float target)
    {
        if (target <= 0f)
            return 1f;
        return Mathf.Clamp01(value / target);
    }

    private float CalculateKeyframePathLength()
    {
        if (keyframes.Count < 2)
            return 0f;

        var length = 0f;
        for (var i = 1; i < keyframes.Count; i++)
            length += Vector3.Distance(keyframes[i - 1].Position, keyframes[i].Position);
        return length;
    }

    private float CalculateKeyframeSpread()
    {
        if (keyframes.Count == 0)
            return 0f;

        var min = keyframes[0].Position;
        var max = keyframes[0].Position;
        for (var i = 1; i < keyframes.Count; i++)
        {
            min = Vector3.Min(min, keyframes[i].Position);
            max = Vector3.Max(max, keyframes[i].Position);
        }

        return (max - min).magnitude;
    }

    private int CountDetectedPlanes()
    {
        if (!captureDetectedPlanesForReconstruction || !planeManager)
            return 0;

        var count = 0;
        foreach (var plane in planeManager.trackables)
        {
            if (plane && plane.trackingState != TrackingState.None)
                count++;
        }

        return count;
    }

    private MeshSummaryDto BuildMeshSummary()
    {
        var summary = new MeshSummaryDto();
        if (!meshManager)
            return summary;

        foreach (var meshFilter in meshManager.meshes)
        {
            if (!meshFilter || !meshFilter.sharedMesh)
                continue;

            summary.meshCount++;
            summary.vertexCount += meshFilter.sharedMesh.vertexCount;
            summary.triangleCount += meshFilter.sharedMesh.triangles.Length / 3;
        }

        return summary;
    }

    private List<MeshSampleDto> BuildMeshSamples(int sampleLimit)
    {
        var samples = new List<MeshSampleDto>(sampleLimit);
        if (!meshManager || sampleLimit == 0)
            return samples;

        foreach (var meshFilter in meshManager.meshes)
        {
            if (!meshFilter || !meshFilter.sharedMesh)
                continue;

            var mesh = meshFilter.sharedMesh;
            var vertices = mesh.vertices;
            var triangles = mesh.triangles;
            if (vertices == null || triangles == null || triangles.Length < 3)
                continue;

            var remaining = sampleLimit - samples.Count;
            var triangleCount = triangles.Length / 3;
            var stride = Mathf.Max(1, Mathf.CeilToInt(triangleCount / (float)Mathf.Max(1, remaining)));

            for (var triangleIndex = 0; triangleIndex < triangleCount && samples.Count < sampleLimit; triangleIndex += stride)
            {
                var i = triangleIndex * 3;
                var a = meshFilter.transform.TransformPoint(vertices[triangles[i]]);
                var b = meshFilter.transform.TransformPoint(vertices[triangles[i + 1]]);
                var c = meshFilter.transform.TransformPoint(vertices[triangles[i + 2]]);
                var normal = Vector3.Cross(b - a, c - a).normalized;
                var centroid = (a + b + c) / 3f;

                samples.Add(new MeshSampleDto
                {
                    centroid = Vector3Dto.From(centroid),
                    normal = Vector3Dto.From(normal),
                    surfaceHint = GuessSurfaceHint(normal)
                });
            }

            if (samples.Count >= sampleLimit)
                break;
        }

        return samples;
    }

    private static string GuessSurfaceHint(Vector3 normal)
    {
        var verticality = Mathf.Abs(normal.y);
        if (verticality >= 0.65f)
            return normal.y > 0f ? "floor_or_table" : "ceiling_or_underside";

        if (verticality <= 0.35f)
            return "wall_or_vertical_surface";

        return "slanted_surface";
    }

    private void ApplyGeminiSemanticMap(GeminiSemanticMapResult result, Bounds bounds)
    {
        if (result == null || !previewRoot)
            return;

        if (result.dominantPalette != null && result.dominantPalette.Length > 0)
            ApplyDominantPalette(result.dominantPalette[0]);

        if (!showGeminiLabelsInPreview)
            return;

        var overlayRoot = new GameObject("Gemini Semantic Overlay");
        overlayRoot.transform.SetParent(previewRoot.transform, false);

        var radius = Mathf.Max(bounds.extents.magnitude, 0.75f);
        var labelScale = Mathf.Clamp(radius * 0.025f, 0.018f, 0.06f);
        var labelOffset = Vector3.up * Mathf.Clamp(radius * 0.08f, 0.08f, 0.28f);

        if (result.zones != null)
        {
            foreach (var zone in result.zones)
            {
                if (zone == null || zone.confidence < geminiMinimumConfidence)
                    continue;

                var position = ResolveSemanticPosition(zone.position, bounds) + labelOffset;
                CreateSemanticLabel(overlayRoot.transform, zone.name, position, labelScale, new Color(0.22f, 0.72f, 1f, 1f));
            }
        }

        if (result.objects != null)
        {
            foreach (var item in result.objects)
            {
                if (item == null || item.confidence < geminiMinimumConfidence)
                    continue;

                var label = string.IsNullOrWhiteSpace(item.color) ? item.name : $"{item.color} {item.name}";
                var position = ResolveSemanticPosition(item.position, bounds) + labelOffset * 1.35f;
                CreateSemanticLabel(overlayRoot.transform, label, position, labelScale, new Color(1f, 0.82f, 0.25f, 1f));
            }
        }

        if (result.surfaces != null)
        {
            foreach (var surface in result.surfaces)
            {
                if (surface == null || surface.confidence < geminiMinimumConfidence)
                    continue;

                var label = $"{surface.color} {surface.type}".Trim();
                var position = ResolveSemanticPosition(surface.position, bounds) + labelOffset * 0.8f;
                CreateSemanticLabel(overlayRoot.transform, label, position, labelScale * 0.85f, new Color(0.76f, 1f, 0.55f, 1f));
            }
        }
    }

    private void ApplyDominantPalette(PaletteColorDto colorDto)
    {
        if (previewHasProjectedColors)
            return;

        if (colorDto == null || string.IsNullOrWhiteSpace(colorDto.hex))
            return;

        if (!ColorUtility.TryParseHtmlString(colorDto.hex, out var color))
            return;

        color = Color.Lerp(color, new Color(0.58f, 0.7f, 0.74f, 1f), 0.55f);
        color.a = 1f;

        var material = GetPreviewMaterial();
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
    }

    private Vector3 ResolveSemanticPosition(Vector3Dto dto, Bounds bounds)
    {
        if (dto == null)
            return bounds.center;

        var position = dto.ToVector3();
        if (!IsFinite(position) || position.sqrMagnitude < 0.0001f)
            return bounds.center;

        return bounds.ClosestPoint(position);
    }

    private static bool IsFinite(Vector3 value)
    {
        return !(float.IsNaN(value.x) || float.IsInfinity(value.x) ||
                 float.IsNaN(value.y) || float.IsInfinity(value.y) ||
                 float.IsNaN(value.z) || float.IsInfinity(value.z));
    }

    private void CreateSemanticLabel(Transform parent, string label, Vector3 position, float scale, Color color)
    {
        if (string.IsNullOrWhiteSpace(label))
            return;

        var labelGo = new GameObject($"Gemini Label - {label}");
        labelGo.transform.SetParent(parent, false);
        labelGo.transform.position = position;
        labelGo.transform.localScale = Vector3.one * scale;

        var text = labelGo.AddComponent<TextMesh>();
        text.text = label;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.fontSize = 48;
        text.color = color;

        var billboard = labelGo.AddComponent<GeminiSemanticLabelBillboard>();
        billboard.TargetCamera = previewCamera;
    }

    private sealed class VisualKeyframe
    {
        public Texture2D Texture;
        public Vector3 Position;
        public Quaternion Rotation;
        public float Timestamp;
        public double CameraTimestamp;
        public double RgbDepthTimestampDeltaSeconds;
        public double RgbConfidenceTimestampDeltaSeconds;
        public Color SampleColor = Color.white;
        public bool HasSurfacePoint;
        public Vector3 SurfacePoint;
        public float FieldOfView;
        public float Aspect;
        public bool HasIntrinsics;
        public Vector2 FocalLength;
        public Vector2 PrincipalPoint;
        public Vector2 ImageResolution;
        public CpuImageFrame Depth;
        public CpuImageFrame Confidence;
        public float DepthConfidenceRatio;
    }

    private sealed class CoverageSummary
    {
        public int totalCells;
        public int weakCells;
        public int fairCells;
        public int goodCells;
        public int totalObservedViews;
    }

    private sealed class CoverageCell
    {
        public Vector3 PositionSum;
        public Vector3 NormalSum;
        public Vector3 Position;
        public Vector3 Normal;
        public int SampleCount;
        public int ViewCount;
        public bool FromPlane;
    }

    private readonly struct CoverageCellKey : IEquatable<CoverageCellKey>
    {
        private readonly int axis;
        private readonly int u;
        private readonly int v;
        private readonly int w;

        public CoverageCellKey(Vector3 position, Vector3 normal, float cellSize)
        {
            var size = Mathf.Max(cellSize, 0.001f);
            var absX = Mathf.Abs(normal.x);
            var absY = Mathf.Abs(normal.y);
            var absZ = Mathf.Abs(normal.z);

            if (absY >= absX && absY >= absZ)
            {
                axis = 0;
                u = Mathf.RoundToInt(position.x / size);
                v = Mathf.RoundToInt(position.z / size);
                w = Mathf.RoundToInt(position.y / (size * 2f));
            }
            else if (absX >= absZ)
            {
                axis = 1;
                u = Mathf.RoundToInt(position.z / size);
                v = Mathf.RoundToInt(position.y / size);
                w = Mathf.RoundToInt(position.x / (size * 2f));
            }
            else
            {
                axis = 2;
                u = Mathf.RoundToInt(position.x / size);
                v = Mathf.RoundToInt(position.y / size);
                w = Mathf.RoundToInt(position.z / (size * 2f));
            }
        }

        public bool Equals(CoverageCellKey other)
        {
            return axis == other.axis && u == other.u && v == other.v && w == other.w;
        }

        public override bool Equals(object obj)
        {
            return obj is CoverageCellKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = axis;
                hash = (hash * 397) ^ u;
                hash = (hash * 397) ^ v;
                hash = (hash * 397) ^ w;
                return hash;
            }
        }
    }

    private struct CameraTextureFrame
    {
        public Texture2D Texture;
        public double CameraTimestamp;
        public double RgbDepthTimestampDeltaSeconds;
        public double RgbConfidenceTimestampDeltaSeconds;
        public float FieldOfView;
        public float Aspect;
        public bool HasIntrinsics;
        public Vector2 FocalLength;
        public Vector2 PrincipalPoint;
        public Vector2 ImageResolution;
        public CpuImageFrame Depth;
        public CpuImageFrame Confidence;
        public float DepthConfidenceRatio;
    }

    private sealed class CpuImageFrame
    {
        public string Kind;
        public int Width;
        public int Height;
        public string Format;
        public double Timestamp;
        public CpuImagePlaneFrame[] Planes;
    }

    private sealed class CpuImagePlaneFrame
    {
        public int RowStride;
        public int PixelStride;
        public byte[] Data;
    }

    [Serializable]
    private sealed class ReconstructionScanManifest
    {
        public string schemaVersion;
        public string scanId;
        public string capturedAtUtc;
        public float durationSeconds;
        public string coordinateSystem;
        public bool hasRawMeshObj;
        public bool hasRgbdRecorderDataset;
        public string rgbdRecorderDatasetFolder;
        public bool depthCaptureRequested;
        public bool planeCaptureRequested;
        public BoundsDto bounds;
        public MeshSummaryDto mesh;
        public ScanQualityReportDto quality;
        public DetectedPlaneDto[] planes;
        public ReconstructionFrameDto[] frames;
    }

    [Serializable]
    private sealed class ScanQualityReportDto
    {
        public float score;
        public string grade;
        public string primaryGuidance;
        public string[] guidance;
        public float durationSeconds;
        public int keyframeCount;
        public int depthFrameCount;
        public int confidenceFrameCount;
        public float averageDepthConfidence;
        public float averageRgbdTimestampDeltaMs;
        public float maxRgbdTimestampDeltaMs;
        public int skippedFastMotionFrames;
        public int skippedUnsyncedDepthFrames;
        public int skippedLowConfidenceFrames;
        public float surfacePointRatio;
        public float cameraPathMeters;
        public float cameraSpreadMeters;
        public int meshCount;
        public int vertexCount;
        public int triangleCount;
        public int detectedPlaneCount;
        public int coverageCellCount;
        public int coverageWeakCellCount;
        public int coverageFairCellCount;
        public int coverageGoodCellCount;
        public float coverageWeakCellRatio;
        public BoundsDto bounds;
    }

    [Serializable]
    private sealed class DetectedPlaneDto
    {
        public string id;
        public string alignment;
        public string trackingState;
        public Vector3Dto center;
        public Vector3Dto normal;
        public Vector2Dto extents;
        public Vector2Dto size;
        public Vector3Dto[] boundaryWorld;
    }

    [Serializable]
    private sealed class ReconstructionUploadResponseDto
    {
        public string scanId;
        public string state;
    }

    [Serializable]
    private sealed class ReconstructionStatusDto
    {
        public string scanId;
        public string state;
        public string message;
        public string resultFile;
        public string updatedAt;
    }

    private sealed class PlyHeader
    {
        public string Format;
        public int VertexCount;
        public int FaceCount;
        public string FaceCountType;
        public string FaceIndexType;
        public readonly List<PlyProperty> VertexProperties = new List<PlyProperty>();

        public bool IsBinaryLittleEndian => string.Equals(Format, "binary_little_endian", StringComparison.OrdinalIgnoreCase);

        public bool HasNormals =>
            VertexProperties.Exists(property => property.Name == "nx") &&
            VertexProperties.Exists(property => property.Name == "ny") &&
            VertexProperties.Exists(property => property.Name == "nz");

        public bool HasColors =>
            (VertexProperties.Exists(property => property.Name == "red") ||
             VertexProperties.Exists(property => property.Name == "r")) &&
            (VertexProperties.Exists(property => property.Name == "green") ||
             VertexProperties.Exists(property => property.Name == "g")) &&
            (VertexProperties.Exists(property => property.Name == "blue") ||
             VertexProperties.Exists(property => property.Name == "b"));
    }

    private readonly struct PlyProperty
    {
        public readonly string Name;
        public readonly string Type;

        public PlyProperty(string name, string type)
        {
            Name = name;
            Type = type;
        }
    }

    [Serializable]
    private sealed class ReconstructionFrameDto
    {
        public string id;
        public string folder;
        public string rgbFile;
        public float timestampSeconds;
        public double cameraTimestampSeconds;
        public double rgbDepthTimestampDeltaSeconds;
        public double rgbConfidenceTimestampDeltaSeconds;
        public Vector3Dto position;
        public QuaternionDto rotation;
        public bool hasIntrinsics;
        public Vector2Dto focalLength;
        public Vector2Dto principalPoint;
        public Vector2Dto imageResolution;
        public float fieldOfView;
        public float aspect;
        public bool hasSurfacePoint;
        public Vector3Dto surfacePoint;
        public CpuImageFrameDto depth;
        public CpuImageFrameDto confidence;
        public double depthTimestampSeconds;
        public float depthConfidenceRatio;
    }

    [Serializable]
    private sealed class CpuImageFrameDto
    {
        public string kind;
        public int width;
        public int height;
        public string format;
        public double timestamp;
        public CpuImagePlaneDto[] planes;
    }

    [Serializable]
    private sealed class CpuImagePlaneDto
    {
        public string file;
        public int rowStride;
        public int pixelStride;
        public int byteLength;
    }

    [Serializable]
    private sealed class Vector2Dto
    {
        public float x;
        public float y;

        public static Vector2Dto From(Vector2 value)
        {
            return new Vector2Dto
            {
                x = value.x,
                y = value.y
            };
        }
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
            resetButton.interactable = !MapScanSession.IsViewingStoredResult
                && !reconstructionPackageRunning
                && scanMode != ScanMode.Scanning;

        if (exportButton)
            exportButton.interactable = !MapScanSession.IsViewingStoredResult
                && !reconstructionPackageRunning
                && scanMode == ScanMode.Scanning;

        if (backButton)
            backButton.interactable = !mapConfirmationRunning;
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
