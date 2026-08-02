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
using UnityEngine.UIElements;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

[RequireComponent(typeof(UIDocument))]
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
    [SerializeField] private UIDocument scanHudDocument;

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

    [Header("Android Depth Scan")]
    [SerializeField] private int androidDepthPreviewMaxFrames = 48;
    [SerializeField] private int androidDepthPreviewMaxPointsPerFrame = 1800;
    [SerializeField] private int androidDepthPreviewPixelStride = 6;
    [SerializeField] private float androidDepthMinMeters = 0.15f;
    [SerializeField] private float androidDepthMaxMeters = 5f;
    [SerializeField] private float androidDepthTriangleMaxDifferenceMeters = 0.18f;
    [SerializeField] private float androidMaxRgbDepthTimestampDeltaSeconds = 0.12f;

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
        Preview,
        MemoPlacement
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
    private Camera completionPreviewCamera;
    private RenderTexture completionPreviewTexture;
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
    private bool reconstructionCompletedSuccessfully;
    private bool mapConfirmationRunning;
    private bool mapSaveRunning;
    private readonly ScanMapService scanMapService = new();
    private static Mesh surfacePointMesh;
    private static Mesh thumbnailQuadMesh;
    private static Mesh coverageQuadMesh;
    private readonly List<VisualKeyframe> keyframes = new List<VisualKeyframe>(32);
    private float nextKeyframeCaptureTime;
    private float nextCoverageOverlayRefreshTime;
    private float stopGuidanceHoldUntil;
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
    private bool depthOnlyPreview;
    private VisualElement scanHudScanLayer;
    private VisualElement scanHudProcessingBlur;
    private VisualElement scanHudFrame;
    private VisualElement scanHudFrameGlow;
    private VisualElement scanHudProgressPill;
    private Label scanHudProgressLabel;
    private Label scanHudProcessingTitleLabel;
    private Label scanHudStatusLabel;
    private Button scanHudStartButton;
    private Button scanHudStopButton;
    private Button scanHudBackButton;
    private VisualElement scanHudCompletionLayer;
    private Image scanHudCompletionMapImage;
    private Button scanHudRegenerateButton;
    private Button scanHudSaveButton;
    private bool completionReviewVisible;
    private string scanHudStatusMessage = string.Empty;
    private VisualElement memoPlacementLayer;
    private VisualElement memoPlacementMapLoadingOverlay;
    private VisualElement memoPlacementGuidance;
    private Image memoPlacementFrozenCameraImage;
    private VisualElement memoPlacementExistingMarkers;
    private Button memoPlacementExistingToggleButton;
    private Label memoPlacementExistingToggleLabel;
    private VisualElement memoPlacementPin;
    private VisualElement memoPlacementKindBar;
    private Label memoPlacementStatusLabel;
    private Button memoPlacementConfirmButton;
    private Button memoPlacementTextButton;
    private Button memoPlacementImageButton;
    private Button memoPlacementVoiceButton;
    private Button memoPlacementChecklistButton;
    private GameObject memoPlacementRoot;
    private MeshCollider memoPlacementCollider;
    private bool memoPlacementSurfaceReady;
    private bool memoPlacementLocalized;
    private bool memoPlacementLocalizing;
    private bool memoPlacementHasCandidate;
    private bool memoPlacementPositionSelected;
    private bool memoPlacementWritingActive;
    private bool memoPlacementExistingMarkersVisible = true;
    private Texture2D memoPlacementFrozenCameraTexture;
    private readonly List<VisualElement> memoPlacementExistingMarkerElements = new();
    private float nextMemoPlacementLocalizationTime;
    private Vector3 memoPlacementCandidatePosition;
    private Quaternion memoPlacementCandidateRotation = Quaternion.identity;

    private static bool IsAndroidDepthScan => Application.platform == RuntimePlatform.Android;

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

        ConfigureScanHud();
    }

    private void OnEnable()
    {
        ARSession.stateChanged += OnARSessionStateChanged;

        if (meshManager)
            meshManager.meshesChanged += OnMeshesChanged;

        scanHudStartButton.clicked += StartScan;
        scanHudStopButton.clicked += StopScanAndShowMap;
        scanHudBackButton.clicked += GoBack;
        scanHudRegenerateButton.clicked += ShowRegenerateConfirm;
        scanHudSaveButton.clicked += SaveCompletedScan;
        memoPlacementConfirmButton.clicked += ConfirmMemoPlacementPosition;
        memoPlacementExistingToggleButton.clicked += ToggleExistingMemoMarkers;
        memoPlacementTextButton.clicked += CompleteTextMemoPlacement;
        memoPlacementImageButton.clicked += CompleteImageMemoPlacement;
        memoPlacementVoiceButton.clicked += CompleteVoiceMemoPlacement;
        memoPlacementChecklistButton.clicked += CompleteChecklistMemoPlacement;
        scanHudCompletionMapImage.RegisterCallback<GeometryChangedEvent>(OnCompletionPreviewGeometryChanged);
    }

    private void OnDisable()
    {
        StopRgbdRecorder();
        ReleaseMemoPlacementFrozenCamera();
        ARSession.stateChanged -= OnARSessionStateChanged;

        if (meshManager)
            meshManager.meshesChanged -= OnMeshesChanged;

        scanHudStartButton.clicked -= StartScan;
        scanHudStopButton.clicked -= StopScanAndShowMap;
        scanHudBackButton.clicked -= GoBack;
        scanHudRegenerateButton.clicked -= ShowRegenerateConfirm;
        scanHudSaveButton.clicked -= SaveCompletedScan;
        memoPlacementConfirmButton.clicked -= ConfirmMemoPlacementPosition;
        memoPlacementExistingToggleButton.clicked -= ToggleExistingMemoMarkers;
        memoPlacementTextButton.clicked -= CompleteTextMemoPlacement;
        memoPlacementImageButton.clicked -= CompleteImageMemoPlacement;
        memoPlacementVoiceButton.clicked -= CompleteVoiceMemoPlacement;
        memoPlacementChecklistButton.clicked -= CompleteChecklistMemoPlacement;
        scanHudCompletionMapImage.UnregisterCallback<GeometryChangedEvent>(OnCompletionPreviewGeometryChanged);
    }

    private void OnDestroy()
    {
        StopRgbdRecorder();
        DestroyLiveCoverageOverlay();
        ClearKeyframes();
        ReleaseCompletionPreviewTexture();
        if (completionPreviewCamera)
            Destroy(completionPreviewCamera.gameObject);
        DestroyMemoPlacementSurface();
    }

    private void Start()
    {
        SetExportStatus(IsAndroidDepthScan
            ? "Ready. Tap Scan to start ARCore Depth mapping."
            : "Ready. Tap Scan to start ARKit mesh mapping.");
        SetScanCaptureEnabled(false);
        if (arSession)
            arSession.enabled = true;
        if (arCamera)
            arCamera.enabled = true;
        if (arCameraBackground)
            arCameraBackground.enabled = true;
        SetLiveMeshesVisible(false);
        EnsurePreviewCamera();
        ApplyMaterialToExistingMeshes();
        UpdateSessionState(ARSession.state);
        UpdateButtonStates();
        UpdateStats(force: true);

        if (MapScanSession.IsMemoPlacement)
        {
            scanMode = ScanMode.MemoPlacement;
            UpdateScanHud();
            _ = LoadStoredReconstructionAsync();
        }
        else if (MapScanSession.IsViewingStoredResult)
            _ = LoadStoredReconstructionAsync();
    }

    private void Update()
    {
        if (memoPlacementWritingActive)
            return;

#if UNITY_ANDROID && !UNITY_EDITOR
        if (UnityEngine.InputSystem.Keyboard.current != null
            && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (MemoAnchor.UI.PopupManager.TryHandleSystemBack())
                return;

            if (scanHudBackButton.enabledSelf
                && !scanHudBackButton.ClassListContains("is-hidden")
                && scanHudBackButton.resolvedStyle.visibility == Visibility.Visible)
            {
                GoBack();
                return;
            }
        }
#endif

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
        else if (scanMode == ScanMode.MemoPlacement)
        {
            UpdateMemoPlacement();
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

        SetCompletionReviewVisible(false);
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
        stopGuidanceHoldUntil = 0f;
        geminiEnhancementRunning = false;
        reconstructionPackageRunning = false;
        reconstructionCompletedSuccessfully = false;
        mapSaveRunning = false;
        scanHudSaveButton.text = "저장하기";
        latestScanQuality = null;
        latestCoverageSummary = null;
        skippedFastMotionFrames = 0;
        skippedUnsyncedDepthFrames = 0;
        skippedLowConfidenceFrames = 0;
        nextRgbdRecorderFrameTime = 0f;
        nextRgbdRecorderFrameId = 1;
        lastRgbdRecorderDatasetPath = string.Empty;
        depthOnlyPreview = false;

        DestroyPreview();
        DestroyLiveCoverageOverlay();

        if (meshManager)
        {
            meshManager.gameObject.SetActive(true);
            if (!IsAndroidDepthScan)
                meshManager.DestroyAllMeshes();
        }

        if (planeManager)
        {
            planeManager.gameObject.SetActive(true);
            planeManager.requestedDetectionMode = PlaneDetectionMode.Horizontal | PlaneDetectionMode.Vertical;
        }

        SetScanCaptureEnabled(true);

        if (previewCamera)
            previewCamera.enabled = false;

        previewMouseDragging = false;
        previousPinchDistance = 0f;

        SetExportStatus(IsAndroidDepthScan
            ? "Scanning with ARCore Depth... move slowly around the space."
            : "Scanning... move slowly around the space.");
        StartRgbdRecorder();
        UpdateSessionState(ARSession.state);
        UpdateButtonStates();
        UpdateStats(force: true);
    }

    public void StopScanAndShowMap()
    {
        if (MapScanSession.IsViewingStoredResult || reconstructionPackageRunning)
            return;

        if (!meshManager && !IsAndroidDepthScan)
        {
            SetExportStatus("Stop failed: ARMeshManager missing");
            return;
        }

        if (scanMode != ScanMode.Scanning)
            return;

        UpdateStats(force: true);
        bool hasScanData = (meshManager && HasLiveMeshData())
            || (IsAndroidDepthScan && HasDepthKeyframeData());
        if (!hasScanData)
        {
            stopGuidanceHoldUntil = Time.unscaledTime + 8f;
            SetExportStatus(IsAndroidDepthScan
                ? IsEnvironmentDepthUnsupported()
                    ? "이 기기는 ARCore Depth 스캔을 지원하지 않습니다."
                    : "아직 Depth 데이터가 부족합니다.\n천천히 이동하며 벽과 바닥을 더 비춰주세요."
                : "아직 맵 데이터가 없습니다.\n휴대폰을 움직여 공간을 더 스캔해주세요.");
            UpdateButtonStates();
            return;
        }

        if (requireMinimumQualityToStop && latestScanQuality != null && latestScanQuality.score < minimumStopQualityScore)
        {
            stopGuidanceHoldUntil = Time.unscaledTime + 8f;
            SetExportStatus(
                $"아직 완료할 수 없습니다.\n품질 {latestScanQuality.score:0}% / 필요 {minimumStopQualityScore:0}%\n" +
                "계속 스캔한 뒤 Stop을 다시 눌러주세요.");
            UpdateButtonStates();
            return;
        }

        scanMode = ScanMode.Preview;
        reconstructionPackageRunning = true;
        mapConfirmationRunning = MapScanSession.HasPendingMap;
        SetScanCaptureEnabled(false);
        SetLiveMeshesVisible(false);
        DestroyLiveCoverageOverlay();
        SetExportStatus("스캔 데이터를 정리하고 있습니다...");
        UpdateSessionState(ARSession.state);
        UpdateButtonStates();

        _ = StopScanAndShowMapAsync();
    }

    private async Awaitable StopScanAndShowMapAsync()
    {
        try
        {
            await Awaitable.NextFrameAsync();
            await StopRgbdRecorderAsync();

            Bounds? bounds = null;
            depthOnlyPreview = false;
            if (meshManager && HasLiveMeshData())
                bounds = await BuildPreviewFromLiveMeshesAsync();
            if (!bounds.HasValue && IsAndroidDepthScan)
            {
                bounds = await BuildPreviewFromDepthKeyframesAsync();
                depthOnlyPreview = bounds.HasValue;
            }
            if (!bounds.HasValue)
            {
                reconstructionPackageRunning = false;
                mapConfirmationRunning = false;
                SetExportStatus("맵 미리보기를 생성하지 못했습니다. 다시 스캔해주세요.");
                UpdateButtonStates();
                return;
            }

            latestScanQuality = BuildScanQualityReport(bounds.Value);
            ShowPreviewCamera(bounds.Value, false);
            BuildVisualKeyframePreview(bounds.Value);

            SetExportStatus(depthOnlyPreview
                ? "Scan stopped. Showing ARCore Depth preview."
                : previewHasProjectedColors
                    ? $"Scan stopped. Showing photo-projected LiDAR map.\nCoverage: {previewProjectedColorCoverage:P0}"
                    : "Scan stopped. Showing clean structural map.");

            if (enhancePreviewWithGemini && !geminiEnhancementRunning)
                _ = EnhancePreviewWithGeminiAsync(bounds.Value);

            UpdateSessionState(ARSession.state);
            UpdateStats(force: true);
            await FinalizeCompletedScanAsync(bounds.Value);
        }
        catch (Exception ex)
        {
            reconstructionPackageRunning = false;
            mapConfirmationRunning = false;
            Debug.LogException(ex);
            SetExportStatus("스캔 데이터 정리에 실패했습니다. 다시 시도해주세요.");
            UpdateButtonStates();
        }
    }

    public void ResetScan()
    {
        StartScan();
    }

    private void ShowRegenerateConfirm()
    {
        if (!reconstructionCompletedSuccessfully || reconstructionPackageRunning || mapSaveRunning)
            return;

        MemoAnchor.UI.PopupManager.ShowConfirm(
            "맵 재생성",
            "현재 생성된 맵 결과는 저장되지 않습니다.\n다시 스캔하시겠습니까?",
            "취소",
            "재생성",
            RegenerateCompletedScan);
    }

    private void RegenerateCompletedScan()
    {
        if (!reconstructionCompletedSuccessfully || reconstructionPackageRunning || mapSaveRunning)
            return;

        Mesh completedMesh = MapScanSession.CompletedReconstructionMesh;
        Material completedMaterial = MapScanSession.CompletedReconstructionMaterial;
        MapScanSession.ClearCompletedReconstruction();
        DestroyPreview();

        if (previewMaterial == completedMaterial)
            previewMaterial = null;
        if (completedMesh)
            Destroy(completedMesh);
        if (completedMaterial)
            Destroy(completedMaterial);

        StartScan();
    }

    private void SaveCompletedScan()
    {
        if (!reconstructionCompletedSuccessfully
            || reconstructionPackageRunning
            || mapSaveRunning
            || !MapScanSession.HasActiveMap)
            return;

        _ = SaveCompletedScanAsync();
    }

    private async Awaitable SaveCompletedScanAsync()
    {
        mapSaveRunning = true;
        scanHudSaveButton.text = "저장 중...";
        UpdateButtonStates();

        ScanMapListResponse response = null;
        try
        {
            byte[] thumbnail = await CaptureCompletionThumbnailAsync();
            if (thumbnail != null && await UploadReconstructionThumbnailAsync(thumbnail))
                response = await scanMapService.ConfirmMapAsync(MapScanSession.MapId);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }

        if (response == null)
        {
            mapSaveRunning = false;
            scanHudSaveButton.text = "저장 실패 · 다시 시도";
            UpdateButtonStates();
            return;
        }

        MapScanSession.RequestReturnToMap();
        CloseScanScene();
    }

    private async Awaitable<byte[]> CaptureCompletionThumbnailAsync()
    {
        if (!completionPreviewTexture || !completionPreviewCamera)
        {
            EnsureCompletionPreviewTexture();
            await Awaitable.NextFrameAsync();
            if (!completionPreviewTexture || !completionPreviewCamera)
                return null;
        }

        Color previousBackground = completionPreviewCamera.backgroundColor;
        completionPreviewCamera.backgroundColor = new Color32(172, 172, 172, 255);
        completionPreviewCamera.enabled = true;
        await Awaitable.EndOfFrameAsync();

        RenderTexture resolvedTexture = RenderTexture.GetTemporary(
            completionPreviewTexture.width,
            completionPreviewTexture.height,
            0,
            RenderTextureFormat.ARGB32);
        Graphics.Blit(completionPreviewTexture, resolvedTexture);
        RenderTexture previousActive = RenderTexture.active;
        RenderTexture.active = resolvedTexture;
        Texture2D thumbnailTexture = new(
            completionPreviewTexture.width,
            completionPreviewTexture.height,
            TextureFormat.RGB24,
            false);
        thumbnailTexture.ReadPixels(
            new Rect(0f, 0f, completionPreviewTexture.width, completionPreviewTexture.height),
            0,
            0);
        thumbnailTexture.Apply(false, false);
        byte[] thumbnail = thumbnailTexture.EncodeToJPG(88);
        RenderTexture.active = previousActive;
        RenderTexture.ReleaseTemporary(resolvedTexture);
        Destroy(thumbnailTexture);
        completionPreviewCamera.backgroundColor = previousBackground;
        return thumbnail;
    }

    private async Awaitable<bool> UploadReconstructionThumbnailAsync(byte[] thumbnail)
    {
        if (string.IsNullOrWhiteSpace(MapScanSession.ReconstructionScanId))
            return false;

        using var request = ServicesManager.CreateAuthorizedRequest(
            MapScanSession.BuildThumbnailPath(MapScanSession.ReconstructionScanId),
            UnityWebRequest.kHttpVerbPUT);
        request.uploadHandler = new UploadHandlerRaw(thumbnail);
        request.SetRequestHeader("Content-Type", "image/jpeg");
        request.timeout = RECONSTRUCTION_REQUEST_TIMEOUT_SECONDS;
        await ServicesManager.SendRequestAsync(request);
        if (request.result == UnityWebRequest.Result.Success)
            return true;

        Debug.LogWarning(
            $"[ARKitMeshScanController] Reconstruction thumbnail upload failed ({request.responseCode}): {request.error}");
        return false;
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
                zipPath = await BuildReconstructionPackageAsync(bounds);
                Debug.Log($"[ARKitMeshScanController] Reconstruction package: {zipPath}");
                SetExportStatus($"Reconstruction package ready.\n{Path.GetFileName(zipPath)}");
            }

            if (MapScanSession.HasPendingMap)
            {
                SetExportStatus("Scan complete. Preparing reconstruction...");
                ScanMapCreateResult result = await scanMapService.CreateMapAsync(MapScanSession.PendingMapRequest);
                if (!result.IsSuccess)
                {
                    SetExportStatus("Scan completed, but map preparation failed. Return and try again.");
                    return;
                }

                MapScanSession.ConfirmMap(result.CreatedMapId);
                mapConfirmationRunning = false;
                UpdateButtonStates();
            }

            if (!packageReconstructionScanOnStop)
            {
                SetExportStatus("Scan complete. Map preview is ready.");
                reconstructionCompletedSuccessfully = MapScanSession.HasActiveMap;
                return;
            }

            if (uploadReconstructionPackageOnStop
                && (MapScanSession.HasActiveMap || !string.IsNullOrWhiteSpace(reconstructionUploadUrl)))
            {
                await UploadReconstructionPackageAsync(zipPath);
            }
            else
            {
                reconstructionCompletedSuccessfully = MapScanSession.HasActiveMap;
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

    private async Awaitable<string> BuildReconstructionPackageAsync(Bounds bounds)
    {
        var id = string.IsNullOrWhiteSpace(scanId) ? DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") : scanId;
        var root = Path.Combine(Application.persistentDataPath, "ReconstructionScans");
        var scanFolder = Path.Combine(root, id);
        var framesFolder = Path.Combine(scanFolder, "frames");

        await Awaitable.BackgroundThreadAsync();
        try
        {
            if (Directory.Exists(scanFolder))
                Directory.Delete(scanFolder, true);

            Directory.CreateDirectory(framesFolder);
        }
        finally
        {
            await Awaitable.MainThreadAsync();
        }

        if (HasLiveMeshData())
            await WriteObjAsync(Path.Combine(scanFolder, "raw_mesh.obj"), meshManager.meshes);

        string rgbdDatasetFolder = await CopyRgbdRecorderDatasetAsync(scanFolder);

        var frameDtos = new List<ReconstructionFrameDto>(keyframes.Count);
        for (var i = 0; i < keyframes.Count; i++)
        {
            var frameFolderName = $"frame_{i + 1:D4}";
            var frameFolder = Path.Combine(framesFolder, frameFolderName);
            Directory.CreateDirectory(frameFolder);

            frameDtos.Add(WriteReconstructionFrame(keyframes[i], i + 1, frameFolderName, frameFolder));
            await Awaitable.NextFrameAsync();
        }

        var manifest = new ReconstructionScanManifest
        {
            schemaVersion = "memoanchor.reconstruction-scan.v1",
            scanId = id,
            capturedAtUtc = DateTime.UtcNow.ToString("o"),
            durationSeconds = Mathf.Max(0f, Time.time - scanStartTime),
            runtimePlatform = Application.platform.ToString(),
            depthOnlyReconstruction = depthOnlyPreview,
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

        var zipPath = Path.Combine(root, $"{id}.zip");
        string manifestJson = JsonUtility.ToJson(manifest, true);
        await Awaitable.BackgroundThreadAsync();
        try
        {
            File.WriteAllText(Path.Combine(scanFolder, "manifest.json"), manifestJson);
            if (File.Exists(zipPath))
                File.Delete(zipPath);

            ZipFile.CreateFromDirectory(scanFolder, zipPath, System.IO.Compression.CompressionLevel.Fastest, false);
        }
        finally
        {
            await Awaitable.MainThreadAsync();
        }

        return zipPath;
    }

    private async Awaitable<string> CopyRgbdRecorderDatasetAsync(string scanFolder)
    {
        if (string.IsNullOrWhiteSpace(lastRgbdRecorderDatasetPath) || !Directory.Exists(lastRgbdRecorderDatasetPath))
            return string.Empty;

        if (!File.Exists(Path.Combine(lastRgbdRecorderDatasetPath, "session.json")) ||
            !File.Exists(Path.Combine(lastRgbdRecorderDatasetPath, "frames.jsonl")))
            return string.Empty;

        string relativeDatasetFolder = "rgbd_dataset";
        string source = lastRgbdRecorderDatasetPath;
        var destination = Path.Combine(scanFolder, relativeDatasetFolder);
        await Awaitable.BackgroundThreadAsync();
        try
        {
            if (Directory.Exists(destination))
                Directory.Delete(destination, true);

            CopyDirectory(source, destination);
        }
        finally
        {
            await Awaitable.MainThreadAsync();
        }

        return relativeDatasetFolder;
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

            if (MapScanSession.IsMemoPlacement)
            {
                ShowMemoPlacementSurface(mesh);
                memoPlacementStatusLabel.text = "스캔 공간을 찾는 중입니다. 주변을 천천히 비춰주세요.";
            }
            else
            {
                ShowServerReconstructionPreview(mesh, serverScanId, localPath);
                SetExportStatus($"Server reconstruction loaded in app.\n{mesh.vertexCount:N0} vertices / {mesh.triangles.Length / 3:N0} triangles");
                if (useMemoAnchorServer)
                    MapScanSession.SetReconstructionResult(serverScanId, fileName);
                MapScanSession.SetCompletedReconstruction(mesh, previewMaterial);
                reconstructionCompletedSuccessfully = true;
            }
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
            AppendObjMesh(builder, meshFilter, ref vertexOffset, ref normalOffset);

        Directory.CreateDirectory(Path.GetDirectoryName(filePath));
        File.WriteAllText(filePath, builder.ToString());
    }

    private async Awaitable WriteObjAsync(string filePath, System.Collections.Generic.IList<MeshFilter> meshFilters)
    {
        var builder = new StringBuilder(1024 * 128);
        builder.AppendLine("# MemoAnchor ARKit mesh scan export");

        var vertexOffset = 0;
        var normalOffset = 0;

        foreach (var meshFilter in meshFilters)
        {
            AppendObjMesh(builder, meshFilter, ref vertexOffset, ref normalOffset);
            await Awaitable.NextFrameAsync();
        }

        string obj = builder.ToString();
        await Awaitable.BackgroundThreadAsync();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            File.WriteAllText(filePath, obj);
        }
        finally
        {
            await Awaitable.MainThreadAsync();
        }
    }

    private static void AppendObjMesh(StringBuilder builder, MeshFilter meshFilter, ref int vertexOffset, ref int normalOffset)
    {
        if (!meshFilter || !meshFilter.sharedMesh)
            return;

        var mesh = meshFilter.sharedMesh;
        var normals = mesh.normals;
        builder.AppendLine($"o {meshFilter.name}");

        foreach (var vertex in mesh.vertices)
        {
            var world = meshFilter.transform.TransformPoint(vertex);
            builder.Append("v ");
            builder.Append(world.x.ToString("F6", CultureInfo.InvariantCulture)).Append(' ');
            builder.Append(world.y.ToString("F6", CultureInfo.InvariantCulture)).Append(' ');
            builder.Append(world.z.ToString("F6", CultureInfo.InvariantCulture)).AppendLine();
        }

        foreach (var normal in normals)
        {
            var worldNormal = meshFilter.transform.TransformDirection(normal).normalized;
            builder.Append("vn ");
            builder.Append(worldNormal.x.ToString("F6", CultureInfo.InvariantCulture)).Append(' ');
            builder.Append(worldNormal.y.ToString("F6", CultureInfo.InvariantCulture)).Append(' ');
            builder.Append(worldNormal.z.ToString("F6", CultureInfo.InvariantCulture)).AppendLine();
        }

        var triangles = mesh.triangles;
        bool hasNormals = normals.Length == mesh.vertexCount;
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
            normalOffset += normals.Length;
    }

    private void GoBack()
    {
        if (MapScanSession.IsMemoPlacement)
        {
            if (memoPlacementPositionSelected)
            {
                ResumeMemoPlacementPositionSelection();
                return;
            }

            MapScanSession.ClearMemoPlacement();
            CloseScanScene();
            return;
        }

        if (scanMode == ScanMode.Scanning)
        {
            MemoAnchor.UI.PopupManager.ShowConfirm(
                "스캔 취소",
                "현재까지 스캔한 내용은 저장되지 않습니다.\n스캔을 취소할까요?",
                "계속 스캔",
                "스캔 취소",
                CancelScanAndClose);
            return;
        }

        if (MapScanSession.HasActiveMap && MapScanSession.Mode == MapScanSession.SessionMode.Scan)
        {
            CancelScanAndClose();
            return;
        }

        CloseScanScene();
    }

    private void CancelScanAndClose()
    {
        _ = CancelScanAndCloseAsync();
    }

    private async Awaitable CancelScanAndCloseAsync()
    {
        SetScanCaptureEnabled(false);
        StopRgbdRecorder();

        try
        {
            if (MapScanSession.HasActiveMap && MapScanSession.Mode == MapScanSession.SessionMode.Scan)
                await scanMapService.DeleteMapAsync(MapScanSession.MapId);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
        finally
        {
            CloseScanScene();
        }
    }

    private void CloseScanScene()
    {
        Scene scanScene = gameObject.scene;
        Scene mainScene = SceneManager.GetSceneByName(fallbackSceneName);
        if (mainScene.IsValid() && mainScene.isLoaded && scanScene != mainScene)
        {
            SceneManager.SetActiveScene(mainScene);
            SceneManager.UnloadSceneAsync(scanScene);
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(fallbackSceneName))
        {
            Debug.LogWarning($"[ARKitMeshScanController] Scene '{fallbackSceneName}' is not in Build Settings.");
            return;
        }

        MapScanSession.Clear();
        SceneManager.LoadScene(fallbackSceneName);
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
            runtime_platform = Application.platform.ToString(),
            depth_provider = IsAndroidDepthScan ? "ARCore" : "ARKit",
            target_frame_rate_hz = frameRate,
            max_rgb_depth_timestamp_difference_ms = GetRgbDepthTimestampLimitSeconds() * 1000d,
            rgb_format = "jpg",
            depth_format = "raw XRCpuImage plane 0",
            depth_unit = "meters; DepthUint16 provider frames are normalized to contiguous DepthFloat32 before recording",
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

    private async Awaitable StopRgbdRecorderAsync()
    {
        if (rgbdRecorder == null)
            return;

        RgbdDatasetRecorder recorder = rgbdRecorder;
        lastRgbdRecorderDatasetPath = recorder.DatasetPath;
        rgbdRecorder = null;

        await Awaitable.BackgroundThreadAsync();
        try
        {
            recorder.Stop();
            recorder.Dispose();
        }
        finally
        {
            await Awaitable.MainThreadAsync();
        }
    }

    private void UpdateSessionState(ARSessionState state)
    {
        UpdateScanHud();
    }

    private void UpdateStats(bool force)
    {
        int meshCount = 0;
        int vertexCount = 0;
        int triangleCount = 0;

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

        UpdateScanningStatus();
    }

    private void SetExportStatus(string message)
    {
        scanHudStatusMessage = message;
        UpdateScanHud();
    }

    private void ConfigureScanHud()
    {
        VisualElement documentRoot = scanHudDocument.rootVisualElement;
        scanHudScanLayer = documentRoot.Q<VisualElement>("scan-viewfinder-layer");
        scanHudProcessingBlur = documentRoot.Q<VisualElement>("scan-processing-blur");
        scanHudFrame = documentRoot.Q<VisualElement>("scan-frame");
        scanHudFrameGlow = documentRoot.Q<VisualElement>("scan-frame-glow");
        scanHudProgressPill = documentRoot.Q<VisualElement>("scan-progress-pill");
        scanHudProgressLabel = documentRoot.Q<Label>("scan-progress-label");
        scanHudProcessingTitleLabel = documentRoot.Q<Label>("scan-processing-title-label");
        scanHudStatusLabel = documentRoot.Q<Label>("scan-status-label");
        scanHudStartButton = documentRoot.Q<Button>("scan-start-button");
        scanHudStopButton = documentRoot.Q<Button>("scan-stop-button");
        scanHudBackButton = documentRoot.Q<Button>("scan-back-button");
        scanHudCompletionLayer = documentRoot.Q<VisualElement>("scan-completion-layer");
        scanHudCompletionMapImage = documentRoot.Q<Image>("scan-completion-map-image");
        scanHudRegenerateButton = documentRoot.Q<Button>("nav-scan");
        scanHudSaveButton = documentRoot.Q<Button>("scan-save-button");
        memoPlacementLayer = documentRoot.Q<VisualElement>("memo-placement-layer");
        memoPlacementMapLoadingOverlay = documentRoot.Q<VisualElement>("memo-placement-map-loading-overlay");
        memoPlacementGuidance = documentRoot.Q<VisualElement>("memo-placement-guidance");
        memoPlacementFrozenCameraImage = documentRoot.Q<Image>("memo-placement-frozen-camera");
        memoPlacementExistingMarkers = documentRoot.Q<VisualElement>("memo-placement-existing-markers");
        memoPlacementExistingToggleButton = documentRoot.Q<Button>("memo-placement-existing-toggle");
        memoPlacementExistingToggleLabel = documentRoot.Q<Label>("memo-placement-existing-toggle-label");
        memoPlacementPin = documentRoot.Q<VisualElement>("memo-placement-pin");
        memoPlacementKindBar = documentRoot.Q<VisualElement>("memo-placement-kind-bar");
        memoPlacementStatusLabel = documentRoot.Q<Label>("memo-placement-status-label");
        memoPlacementConfirmButton = documentRoot.Q<Button>("memo-placement-confirm-button");
        memoPlacementTextButton = documentRoot.Q<Button>("memo-placement-text-button");
        memoPlacementImageButton = documentRoot.Q<Button>("memo-placement-image-button");
        memoPlacementVoiceButton = documentRoot.Q<Button>("memo-placement-voice-button");
        memoPlacementChecklistButton = documentRoot.Q<Button>("memo-placement-checklist-button");

        UpdateScanHud();
    }

    private void SetCompletionReviewVisible(bool visible)
    {
        if (completionReviewVisible == visible)
            return;

        completionReviewVisible = visible;
        scanHudCompletionLayer.EnableInClassList("is-hidden", !visible);
        if (visible)
        {
            scanHudCompletionMapImage.schedule.Execute(EnsureCompletionPreviewTexture);
            return;
        }

        ReleaseCompletionPreviewTexture();
    }

    private void OnCompletionPreviewGeometryChanged(GeometryChangedEvent evt)
    {
        if (completionReviewVisible)
            EnsureCompletionPreviewTexture(evt.newRect);
    }

    private void EnsureCompletionPreviewTexture()
    {
        EnsureCompletionPreviewTexture(scanHudCompletionMapImage.contentRect);
    }

    private void EnsureCompletionPreviewTexture(Rect rect)
    {
        if (rect.width <= 1f || rect.height <= 1f)
            return;

        EnsureCompletionPreviewCamera();
        float renderScale = Mathf.Min(1f, 2048f / Mathf.Max(rect.width, rect.height));
        int width = Mathf.Max(1, Mathf.RoundToInt(rect.width * renderScale));
        int height = Mathf.Max(1, Mathf.RoundToInt(rect.height * renderScale));
        if (completionPreviewTexture
            && completionPreviewTexture.width == width
            && completionPreviewTexture.height == height)
        {
            completionPreviewCamera.enabled = true;
            return;
        }

        ReleaseCompletionPreviewTexture();
        completionPreviewTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
        {
            name = "Scan Completion Preview",
            antiAliasing = 4
        };
        completionPreviewTexture.Create();
        completionPreviewCamera.targetTexture = completionPreviewTexture;
        completionPreviewCamera.enabled = true;
        scanHudCompletionMapImage.image = completionPreviewTexture;
        SyncCompletionPreviewCamera();
    }

    private void EnsureCompletionPreviewCamera()
    {
        if (completionPreviewCamera)
            return;

        var cameraObject = new GameObject("Scan Completion Preview Camera");
        cameraObject.transform.SetParent(transform, false);
        completionPreviewCamera = cameraObject.AddComponent<Camera>();
        completionPreviewCamera.CopyFrom(previewCamera);
        completionPreviewCamera.clearFlags = CameraClearFlags.SolidColor;
        completionPreviewCamera.backgroundColor = Color.clear;
        completionPreviewCamera.allowHDR = false;
        completionPreviewCamera.enabled = false;
    }

    private void SyncCompletionPreviewCamera()
    {
        if (!completionPreviewCamera || !previewCamera)
            return;

        completionPreviewCamera.fieldOfView = previewCamera.fieldOfView;
        completionPreviewCamera.nearClipPlane = previewCamera.nearClipPlane;
        completionPreviewCamera.farClipPlane = previewCamera.farClipPlane;
        completionPreviewCamera.transform.SetPositionAndRotation(
            previewCamera.transform.position,
            previewCamera.transform.rotation);
    }

    private void ReleaseCompletionPreviewTexture()
    {
        if (!completionPreviewTexture)
            return;

        if (completionPreviewCamera)
        {
            completionPreviewCamera.targetTexture = null;
            completionPreviewCamera.enabled = false;
        }

        scanHudCompletionMapImage.image = null;
        completionPreviewTexture.Release();
        Destroy(completionPreviewTexture);
        completionPreviewTexture = null;
    }

    private void UpdateScanHud()
    {
        bool isMemoPlacement = scanMode == ScanMode.MemoPlacement;
        bool isReady = scanMode == ScanMode.Ready && !MapScanSession.IsViewingStoredResult;
        bool isScanning = scanMode == ScanMode.Scanning;
        bool showViewfinder = isReady || isScanning;
        bool showCompletionReview = scanMode == ScanMode.Preview
            && reconstructionCompletedSuccessfully
            && !reconstructionPackageRunning
            && !MapScanSession.IsViewingStoredResult;
        bool showProcessingBlur = scanMode == ScanMode.Preview
            && reconstructionPackageRunning
            && !MapScanSession.IsViewingStoredResult;
        bool qualityReady = latestScanQuality != null &&
            (!requireMinimumQualityToStop || latestScanQuality.score >= minimumStopQualityScore);
        bool showMovementGuidance = isScanning &&
            Time.time - scanStartTime >= 3f &&
            !qualityReady &&
            Time.unscaledTime >= stopGuidanceHoldUntil;

        scanHudScanLayer.EnableInClassList("is-hidden", !showViewfinder);
        scanHudProcessingBlur.EnableInClassList("is-hidden", !showProcessingBlur);
        scanHudProcessingTitleLabel.EnableInClassList("is-hidden", !showProcessingBlur);
        scanHudProgressPill.EnableInClassList("is-hidden", !showViewfinder || isMemoPlacement);
        scanHudStartButton.EnableInClassList("is-hidden", !isReady);
        scanHudStopButton.EnableInClassList("is-hidden", !isScanning);
        scanHudBackButton.EnableInClassList("is-hidden", showProcessingBlur || showCompletionReview);
        scanHudStatusLabel.EnableInClassList("is-hidden", showCompletionReview || isMemoPlacement);
        memoPlacementLayer.EnableInClassList("is-hidden", !isMemoPlacement);
        memoPlacementMapLoadingOverlay.EnableInClassList("is-hidden", !isMemoPlacement || memoPlacementSurfaceReady);
        memoPlacementGuidance.EnableInClassList("is-hidden", isMemoPlacement && !memoPlacementSurfaceReady);
        scanHudFrame.EnableInClassList("is-alert", showMovementGuidance);
        scanHudFrameGlow.EnableInClassList("is-hidden", !showMovementGuidance);
        SetCompletionReviewVisible(showCompletionReview);

        int progress = isScanning && latestScanQuality != null
            ? Mathf.RoundToInt(Mathf.Clamp(latestScanQuality.score, 0f, 100f))
            : 0;
        int completionQuality = Mathf.RoundToInt(Mathf.Clamp(minimumStopQualityScore, 0f, 100f));
        scanHudProgressLabel.text = $"{progress}% / 완료 가능 {completionQuality}%";

        scanHudStatusLabel.EnableInClassList("is-ready", isReady);
        scanHudStatusLabel.EnableInClassList("is-scanning", isScanning);
        scanHudStatusLabel.EnableInClassList("is-result", !showViewfinder);
        scanHudStatusLabel.EnableInClassList("is-processing", showProcessingBlur);
        if (isReady)
        {
            scanHudStatusLabel.text = "스캔을 시작하려면 버튼을 누르십시오.";
            return;
        }

        if (isScanning)
        {
            if (Time.unscaledTime < stopGuidanceHoldUntil && !string.IsNullOrWhiteSpace(scanHudStatusMessage))
                scanHudStatusLabel.text = scanHudStatusMessage;
            else if (qualityReady)
                scanHudStatusLabel.text = "스캔 완료가 가능합니다.";
            else
                scanHudStatusLabel.text = showMovementGuidance ? "움직여 주십시오." : "스캔 중입니다.";

            return;
        }

        scanHudStatusLabel.text = scanHudStatusMessage;
    }

    private void UpdateScanningStatus()
    {
        if (scanMode != ScanMode.Scanning ||
            Time.unscaledTime < stopGuidanceHoldUntil ||
            latestScanQuality == null)
        {
            return;
        }

        UpdateScanHud();
    }

    private async Awaitable<Bounds?> BuildPreviewFromLiveMeshesAsync()
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
            await Awaitable.NextFrameAsync();
        }

        if (vertices.Count == 0 || triangles.Count == 0)
        {
            DestroyPreview();
            return null;
        }

        await SmoothVerticesAsync(vertices, triangles);

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

    private bool HasLiveMeshData()
    {
        if (!meshManager)
            return false;

        foreach (var meshFilter in meshManager.meshes)
        {
            if (meshFilter && meshFilter.sharedMesh &&
                meshFilter.sharedMesh.vertexCount > 0 &&
                meshFilter.sharedMesh.triangles.Length >= 3)
            {
                return true;
            }
        }

        return false;
    }

    private bool HasDepthKeyframeData()
    {
        foreach (VisualKeyframe keyframe in keyframes)
        {
            if (keyframe.Depth != null && keyframe.HasIntrinsics)
                return true;
        }

        return false;
    }

    private async Awaitable<Bounds?> BuildPreviewFromDepthKeyframesAsync()
    {
        DestroyPreview();
        previewHasProjectedColors = false;
        previewProjectedColorCoverage = 0f;

        var availableDepthFrames = 0;
        foreach (var keyframe in keyframes)
        {
            if (keyframe.Depth != null && keyframe.HasIntrinsics)
                availableDepthFrames++;
        }

        if (availableDepthFrames == 0)
            return null;

        var maxFrames = Mathf.Max(1, androidDepthPreviewMaxFrames);
        var frameStride = Mathf.Max(1, Mathf.CeilToInt(availableDepthFrames / (float)maxFrames));
        var vertices = new List<Vector3>(Mathf.Min(availableDepthFrames, maxFrames) * 1024);
        var triangles = new List<int>(Mathf.Min(availableDepthFrames, maxFrames) * 2048);
        var colors = new List<Color>(vertices.Capacity);
        var eligibleFrameIndex = 0;
        var appendedFrameCount = 0;

        foreach (var keyframe in keyframes)
        {
            if (keyframe.Depth == null || !keyframe.HasIntrinsics)
                continue;

            if ((eligibleFrameIndex++ % frameStride) != 0)
                continue;

            var vertexCountBefore = vertices.Count;
            AppendDepthFramePreview(keyframe, vertices, triangles, colors);
            if (vertices.Count > vertexCountBefore)
                appendedFrameCount++;

            await Awaitable.NextFrameAsync();
        }

        if (vertices.Count == 0)
            return null;

        previewRoot = new GameObject("Scanned Map Preview");
        var previewMesh = new Mesh
        {
            name = "ARCore_Depth_Map",
            indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16
        };

        previewMesh.SetVertices(vertices);
        if (triangles.Count > 0)
        {
            previewMesh.SetTriangles(triangles, 0);
        }
        else
        {
            var pointIndices = new int[vertices.Count];
            for (var i = 0; i < pointIndices.Length; i++)
                pointIndices[i] = i;
            previewMesh.SetIndices(pointIndices, MeshTopology.Points, 0);
        }
        if (colors.Count == vertices.Count)
        {
            previewMesh.SetColors(colors);
            previewHasProjectedColors = true;
            previewProjectedColorCoverage = 1f;
        }

        if (triangles.Count > 0)
            previewMesh.RecalculateNormals();
        previewMesh.RecalculateBounds();

        var previewGo = new GameObject("ARCore Depth Surface");
        previewGo.transform.SetParent(previewRoot.transform, false);
        previewGo.AddComponent<MeshFilter>().sharedMesh = previewMesh;
        var renderer = previewGo.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = GetPreviewMaterial();

        previewMeshCount = appendedFrameCount;
        previewVertexCount = vertices.Count;
        previewTriangleCount = triangles.Count / 3;
        return previewMesh.bounds;
    }

    private void AppendDepthFramePreview(
        VisualKeyframe keyframe,
        List<Vector3> vertices,
        List<int> triangles,
        List<Color> colors)
    {
        var depth = keyframe.Depth;
        var configuredStride = Mathf.Max(1, androidDepthPreviewPixelStride);
        var pointLimit = Mathf.Max(64, androidDepthPreviewMaxPointsPerFrame);
        var adaptiveStride = Mathf.CeilToInt(Mathf.Sqrt(depth.Width * depth.Height / (float)pointLimit));
        var pixelStride = Mathf.Max(configuredStride, adaptiveStride);
        var columns = Mathf.CeilToInt(depth.Width / (float)pixelStride);
        var rows = Mathf.CeilToInt(depth.Height / (float)pixelStride);
        var gridIndices = new int[columns * rows];
        var gridDepths = new float[gridIndices.Length];
        for (var i = 0; i < gridIndices.Length; i++)
            gridIndices[i] = -1;

        for (var row = 0; row < rows; row++)
        {
            var y = Mathf.Min(row * pixelStride, depth.Height - 1);
            for (var column = 0; column < columns; column++)
            {
                var x = Mathf.Min(column * pixelStride, depth.Width - 1);
                if (!TryReadDepthMeters(depth, x, y, out var depthMeters) ||
                    depthMeters < androidDepthMinMeters ||
                    depthMeters > androidDepthMaxMeters)
                {
                    continue;
                }

                if (TryReadConfidence(keyframe.Confidence, depth, x, y, out var confidence) && confidence == 0)
                    continue;

                if (!TryUnprojectDepthPixel(keyframe, x, y, depthMeters, out var worldPoint))
                    continue;

                var gridIndex = row * columns + column;
                gridIndices[gridIndex] = vertices.Count;
                gridDepths[gridIndex] = depthMeters;
                vertices.Add(worldPoint);
                colors.Add(SampleDepthPreviewColor(keyframe, x, y));
            }
        }

        for (var row = 0; row < rows - 1; row++)
        {
            for (var column = 0; column < columns - 1; column++)
            {
                var topLeft = row * columns + column;
                var topRight = topLeft + 1;
                var bottomLeft = topLeft + columns;
                var bottomRight = bottomLeft + 1;
                TryAppendDepthTriangle(topLeft, bottomLeft, topRight, gridIndices, gridDepths, vertices, triangles);
                TryAppendDepthTriangle(topRight, bottomLeft, bottomRight, gridIndices, gridDepths, vertices, triangles);
            }
        }
    }

    private void TryAppendDepthTriangle(
        int firstGridIndex,
        int secondGridIndex,
        int thirdGridIndex,
        int[] gridIndices,
        float[] gridDepths,
        List<Vector3> vertices,
        List<int> triangles)
    {
        var first = gridIndices[firstGridIndex];
        var second = gridIndices[secondGridIndex];
        var third = gridIndices[thirdGridIndex];
        if (first < 0 || second < 0 || third < 0)
            return;

        var minDepth = Mathf.Min(gridDepths[firstGridIndex], Mathf.Min(gridDepths[secondGridIndex], gridDepths[thirdGridIndex]));
        var maxDepth = Mathf.Max(gridDepths[firstGridIndex], Mathf.Max(gridDepths[secondGridIndex], gridDepths[thirdGridIndex]));
        var allowedDifference = Mathf.Max(0.02f, androidDepthTriangleMaxDifferenceMeters) + minDepth * 0.03f;
        if (maxDepth - minDepth > allowedDifference)
            return;

        if (Vector3.Cross(vertices[second] - vertices[first], vertices[third] - vertices[first]).sqrMagnitude < 0.00000001f)
            return;

        triangles.Add(first);
        triangles.Add(second);
        triangles.Add(third);
    }

    private static bool TryReadDepthMeters(CpuImageFrame depth, int x, int y, out float depthMeters)
    {
        depthMeters = 0f;
        if (depth == null || depth.Planes == null || depth.Planes.Length == 0)
            return false;

        var plane = depth.Planes[0];
        if (plane.Data == null || x < 0 || x >= depth.Width || y < 0 || y >= depth.Height)
            return false;

        var offset = y * plane.RowStride + x * plane.PixelStride;
        if (string.Equals(depth.Format, XRCpuImage.Format.DepthFloat32.ToString(), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(depth.Format, XRCpuImage.Format.OneComponent32.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            if (offset < 0 || offset + sizeof(float) > plane.Data.Length)
                return false;

            depthMeters = BitConverter.ToSingle(plane.Data, offset);
        }
        else if (string.Equals(depth.Format, XRCpuImage.Format.DepthUint16.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            if (offset < 0 || offset + sizeof(ushort) > plane.Data.Length)
                return false;

            depthMeters = BitConverter.ToUInt16(plane.Data, offset) * 0.001f;
        }
        else
        {
            return false;
        }

        return depthMeters > 0f && !float.IsNaN(depthMeters) && !float.IsInfinity(depthMeters);
    }

    private static bool TryReadConfidence(
        CpuImageFrame confidence,
        CpuImageFrame depth,
        int depthX,
        int depthY,
        out byte value)
    {
        value = 0;
        if (confidence == null || confidence.Planes == null || confidence.Planes.Length == 0 ||
            confidence.Width <= 0 || confidence.Height <= 0)
        {
            return false;
        }

        var plane = confidence.Planes[0];
        if (plane.Data == null)
            return false;

        var x = depth.Width > 1
            ? Mathf.RoundToInt(depthX / (float)(depth.Width - 1) * (confidence.Width - 1))
            : 0;
        var y = depth.Height > 1
            ? Mathf.RoundToInt(depthY / (float)(depth.Height - 1) * (confidence.Height - 1))
            : 0;
        var offset = y * plane.RowStride + x * plane.PixelStride;
        if (offset < 0 || offset >= plane.Data.Length)
            return false;

        value = plane.Data[offset];
        return true;
    }

    private static bool TryUnprojectDepthPixel(
        VisualKeyframe keyframe,
        int depthX,
        int depthY,
        float depthMeters,
        out Vector3 worldPoint)
    {
        worldPoint = default;
        var depth = keyframe.Depth;
        if (!keyframe.HasIntrinsics || depth == null ||
            keyframe.ImageResolution.x <= 1f || keyframe.ImageResolution.y <= 1f)
        {
            return false;
        }

        var scaleX = depth.Width / keyframe.ImageResolution.x;
        var scaleY = depth.Height / keyframe.ImageResolution.y;
        var fx = keyframe.FocalLength.x * scaleX;
        var fy = keyframe.FocalLength.y * scaleY;
        if (fx <= 0.001f || fy <= 0.001f)
            return false;

        var cx = keyframe.PrincipalPoint.x * scaleX;
        var cy = keyframe.PrincipalPoint.y * scaleY;
        var cameraPoint = new Vector3(
            (depthX - cx) * depthMeters / fx,
            -(depthY - cy) * depthMeters / fy,
            depthMeters);
        worldPoint = keyframe.Position + keyframe.Rotation * cameraPoint;
        return IsFinite(worldPoint);
    }

    private static Color SampleDepthPreviewColor(VisualKeyframe keyframe, int depthX, int depthY)
    {
        if (!keyframe.Texture || keyframe.Depth == null)
            return keyframe.SampleColor;

        var u = keyframe.Depth.Width > 1 ? depthX / (float)(keyframe.Depth.Width - 1) : 0.5f;
        var v = keyframe.Depth.Height > 1 ? 1f - depthY / (float)(keyframe.Depth.Height - 1) : 0.5f;
        return keyframe.Texture.GetPixelBilinear(u, v);
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
        ShowPreviewCamera(mesh.bounds, MapScanSession.IsViewingStoredResult);
        BuildVisualKeyframePreview(mesh.bounds);
        UpdateButtonStates();
        UpdateStats(force: true);

        Debug.Log($"[ARKitMeshScanController] Showing server reconstruction: {localPath}");
    }

    private void ShowMemoPlacementSurface(Mesh mesh)
    {
        DestroyMemoPlacementSurface();

        memoPlacementRoot = new GameObject("Memo Placement Map Space");
        var surface = new GameObject("Memo Placement Reconstruction Surface");
        surface.transform.SetParent(memoPlacementRoot.transform, false);
        surface.transform.localScale = new Vector3(1f, 1f, -1f);
        surface.AddComponent<MeshFilter>().sharedMesh = mesh;
        memoPlacementCollider = surface.AddComponent<MeshCollider>();
        memoPlacementCollider.sharedMesh = mesh;
        BuildExistingMemoMarkers();
        SetExistingMemoMarkersVisible(memoPlacementExistingMarkersVisible);

        memoPlacementSurfaceReady = true;
        memoPlacementLocalized = false;
        memoPlacementLocalizing = false;
        memoPlacementHasCandidate = false;
        memoPlacementPositionSelected = false;
        nextMemoPlacementLocalizationTime = 0f;
        memoPlacementPin.AddToClassList("is-hidden");
        memoPlacementConfirmButton.AddToClassList("is-hidden");
        memoPlacementKindBar.AddToClassList("is-hidden");
        UpdateScanHud();
    }

    private void DestroyMemoPlacementSurface()
    {
        ReleaseMemoPlacementFrozenCamera();
        ClearExistingMemoMarkers();

        if (memoPlacementRoot)
            Destroy(memoPlacementRoot);

        memoPlacementRoot = null;
        memoPlacementCollider = null;
        memoPlacementSurfaceReady = false;
        memoPlacementLocalized = false;
        memoPlacementHasCandidate = false;
        memoPlacementPositionSelected = false;
    }

    private void UpdateMemoPlacement()
    {
        if (!memoPlacementSurfaceReady)
        {
            memoPlacementStatusLabel.text = "3D MAP을 불러오는 중입니다.";
            return;
        }

        if (!memoPlacementLocalized)
        {
            if (!memoPlacementLocalizing && Time.unscaledTime >= nextMemoPlacementLocalizationTime)
                _ = LocalizeMemoPlacementAsync();
            return;
        }

        UpdateExistingMemoMarkers();
        if (memoPlacementPositionSelected)
            return;

        Ray ray = arCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f));
        if (!memoPlacementCollider.Raycast(ray, out RaycastHit hit, 10f))
        {
            memoPlacementHasCandidate = false;
            memoPlacementPin.AddToClassList("is-hidden");
            memoPlacementConfirmButton.AddToClassList("is-hidden");
            memoPlacementStatusLabel.text = "메모를 부착할 표면을 화면 가운데에 맞춰주세요.";
            return;
        }

        memoPlacementHasCandidate = true;
        memoPlacementCandidatePosition = memoPlacementRoot.transform.InverseTransformPoint(hit.point);
        Vector3 localNormal = memoPlacementRoot.transform.InverseTransformDirection(hit.normal).normalized;
        Vector3 up = Mathf.Abs(Vector3.Dot(localNormal, Vector3.up)) > 0.95f ? Vector3.forward : Vector3.up;
        memoPlacementCandidateRotation = Quaternion.LookRotation(localNormal, up);
        memoPlacementPin.RemoveFromClassList("is-hidden");
        memoPlacementConfirmButton.RemoveFromClassList("is-hidden");
        memoPlacementStatusLabel.text = "부착할 위치에 맞춘 뒤 버튼을 눌러주세요.";
    }

    private async Awaitable LocalizeMemoPlacementAsync()
    {
        memoPlacementLocalizing = true;
        nextMemoPlacementLocalizationTime = Time.unscaledTime + 1.5f;
        memoPlacementStatusLabel.text = "스캔 공간을 찾는 중입니다. 주변을 천천히 비춰주세요.";

        if (!TryCreateCameraTexture(out CameraTextureFrame frame, false))
        {
            memoPlacementLocalizing = false;
            return;
        }

        if (!frame.HasIntrinsics)
        {
            Destroy(frame.Texture);
            memoPlacementLocalizing = false;
            memoPlacementStatusLabel.text = "카메라 내부 파라미터를 기다리는 중입니다.";
            return;
        }

        byte[] jpeg = frame.Texture.EncodeToJPG(78);
        Destroy(frame.Texture);
        string path = MapScanSession.BuildLocalizationPath(MapScanSession.ReconstructionScanId);
        using var request = new UnityWebRequest(ServicesManager.BuildServerUrl(path), UnityWebRequest.kHttpVerbPOST)
        {
            uploadHandler = new UploadHandlerRaw(jpeg),
            downloadHandler = new DownloadHandlerBuffer(),
            timeout = 30
        };
        request.SetRequestHeader("Content-Type", "image/jpeg");
        request.SetRequestHeader("X-MemoAnchor-Fx", frame.FocalLength.x.ToString("R", CultureInfo.InvariantCulture));
        request.SetRequestHeader("X-MemoAnchor-Fy", frame.FocalLength.y.ToString("R", CultureInfo.InvariantCulture));
        request.SetRequestHeader("X-MemoAnchor-Cx", frame.PrincipalPoint.x.ToString("R", CultureInfo.InvariantCulture));
        request.SetRequestHeader("X-MemoAnchor-Cy", frame.PrincipalPoint.y.ToString("R", CultureInfo.InvariantCulture));
        ServicesManager.Authorize(request);

        await ServicesManager.SendRequestAsync(request);
        memoPlacementLocalizing = false;
        if (scanMode != ScanMode.MemoPlacement || !memoPlacementRoot)
            return;

        if (request.result != UnityWebRequest.Result.Success)
        {
            memoPlacementStatusLabel.text = request.responseCode == 422
                ? "스캔 당시 위치와 비슷한 곳에서 주변을 천천히 비춰주세요."
                : "공간을 찾지 못했습니다. 잠시 후 다시 시도합니다.";
            return;
        }

        MemoPlacementLocalizationDto localization = JsonUtility.FromJson<MemoPlacementLocalizationDto>(request.downloadHandler.text);
        if (localization == null || !localization.localized)
        {
            memoPlacementStatusLabel.text = string.IsNullOrWhiteSpace(localization?.message)
                ? "스캔 당시 위치와 비슷한 곳에서 주변을 천천히 비춰주세요."
                : localization.message;
            return;
        }

        if (localization.cameraPosition == null || localization.cameraPosition.Length < 3
            || localization.cameraRotation == null || localization.cameraRotation.Length < 4)
        {
            memoPlacementStatusLabel.text = "공간 위치 응답이 올바르지 않습니다.";
            return;
        }

        Vector3 scanCameraPosition = new(
            localization.cameraPosition[0],
            localization.cameraPosition[1],
            localization.cameraPosition[2]);
        Quaternion scanCameraRotation = new(
            localization.cameraRotation[0],
            localization.cameraRotation[1],
            localization.cameraRotation[2],
            localization.cameraRotation[3]);
        Matrix4x4 sessionFromCamera = Matrix4x4.TRS(arCamera.transform.position, arCamera.transform.rotation, Vector3.one);
        Matrix4x4 scanFromCamera = Matrix4x4.TRS(scanCameraPosition, scanCameraRotation, Vector3.one);
        Matrix4x4 sessionFromScan = sessionFromCamera * scanFromCamera.inverse;
        Vector3 rootPosition = sessionFromScan.GetColumn(3);
        Quaternion rootRotation = Quaternion.LookRotation(sessionFromScan.GetColumn(2), sessionFromScan.GetColumn(1));
        memoPlacementRoot.transform.SetPositionAndRotation(rootPosition, rootRotation);
        memoPlacementLocalized = true;
        memoPlacementStatusLabel.text = "부착할 위치에 맞춘 뒤 버튼을 눌러주세요.";
    }

    private void ConfirmMemoPlacementPosition()
    {
        if (!memoPlacementHasCandidate || memoPlacementPositionSelected)
            return;

        memoPlacementPositionSelected = true;
        _ = FreezeMemoPlacementCameraAsync();
    }

    private async Awaitable FreezeMemoPlacementCameraAsync()
    {
        memoPlacementLayer.style.visibility = Visibility.Hidden;
        scanHudBackButton.style.visibility = Visibility.Hidden;
        await Awaitable.EndOfFrameAsync();

        UpdateExistingMemoMarkers();
        MemoAnchor.UI.ScreenSpaceUIToolkitBlurRendererFeature.SetOutputFrozen(true);
        memoPlacementFrozenCameraTexture = ScreenCapture.CaptureScreenshotAsTexture();
        memoPlacementFrozenCameraImage.image = memoPlacementFrozenCameraTexture;
        memoPlacementFrozenCameraImage.RemoveFromClassList("is-hidden");
        memoPlacementLayer.style.visibility = Visibility.Visible;
        scanHudBackButton.style.visibility = Visibility.Visible;
        memoPlacementConfirmButton.AddToClassList("is-hidden");
        memoPlacementKindBar.RemoveFromClassList("is-hidden");
        memoPlacementStatusLabel.text = "작성할 메모의 유형을 고르십시오";
    }

    private void ReleaseMemoPlacementFrozenCamera()
    {
        MemoAnchor.UI.ScreenSpaceUIToolkitBlurRendererFeature.SetOutputFrozen(false);
        memoPlacementFrozenCameraImage.image = null;
        memoPlacementFrozenCameraImage.AddToClassList("is-hidden");
        if (memoPlacementFrozenCameraTexture)
            Destroy(memoPlacementFrozenCameraTexture);

        memoPlacementFrozenCameraTexture = null;
    }

    private void ResumeMemoPlacementPositionSelection()
    {
        ReleaseMemoPlacementFrozenCamera();
        memoPlacementPositionSelected = false;
        memoPlacementHasCandidate = false;
        memoPlacementKindBar.AddToClassList("is-hidden");
        memoPlacementConfirmButton.AddToClassList("is-hidden");
        memoPlacementPin.AddToClassList("is-hidden");
        memoPlacementStatusLabel.text = "메모를 부착할 표면을 화면 가운데에 맞춰주세요.";
    }

    private void BuildExistingMemoMarkers()
    {
        ClearExistingMemoMarkers();
        IReadOnlyList<MapScanSession.ExistingMemoMarker> markers = MapScanSession.ExistingMemoMarkers;
        for (int i = 0; i < markers.Count; i++)
        {
            var marker = new VisualElement
            {
                pickingMode = PickingMode.Ignore
            };
            marker.AddToClassList("memo-placement-existing-marker");
            marker.EnableInClassList("is-completion-requested", markers[i].IsCompletionRequested);
            marker.AddToClassList("is-hidden");
            memoPlacementExistingMarkers.Add(marker);
            memoPlacementExistingMarkerElements.Add(marker);
        }
    }

    private void ToggleExistingMemoMarkers()
    {
        SetExistingMemoMarkersVisible(!memoPlacementExistingMarkersVisible);
    }

    private void SetExistingMemoMarkersVisible(bool visible)
    {
        memoPlacementExistingMarkersVisible = visible;
        memoPlacementExistingMarkers.EnableInClassList("is-hidden", !visible);
        memoPlacementExistingToggleButton.EnableInClassList("is-on", visible);
        memoPlacementExistingToggleLabel.text = visible ? "ON" : "OFF";
        if (visible && memoPlacementLocalized && !memoPlacementPositionSelected)
            UpdateExistingMemoMarkers();
    }

    private void UpdateExistingMemoMarkers()
    {
        IReadOnlyList<MapScanSession.ExistingMemoMarker> markers = MapScanSession.ExistingMemoMarkers;
        float panelWidth = memoPlacementLayer.resolvedStyle.width;
        float panelHeight = memoPlacementLayer.resolvedStyle.height;
        for (int i = 0; i < memoPlacementExistingMarkerElements.Count; i++)
        {
            Vector3 worldPosition = memoPlacementRoot.transform.TransformPoint(markers[i].Position);
            Vector3 screenPosition = arCamera.WorldToScreenPoint(worldPosition);
            bool isVisible = screenPosition.z > 0f
                && screenPosition.x >= 0f
                && screenPosition.x <= Screen.width
                && screenPosition.y >= 0f
                && screenPosition.y <= Screen.height;
            VisualElement marker = memoPlacementExistingMarkerElements[i];
            marker.EnableInClassList("is-hidden", !isVisible);
            if (!isVisible)
                continue;

            marker.style.left = screenPosition.x / Screen.width * panelWidth;
            marker.style.top = (1f - screenPosition.y / Screen.height) * panelHeight;
        }
    }

    private void ClearExistingMemoMarkers()
    {
        memoPlacementExistingMarkers.Clear();
        memoPlacementExistingMarkerElements.Clear();
    }

    private void CompleteTextMemoPlacement()
    {
        CompleteMemoPlacement("text");
    }

    private void CompleteImageMemoPlacement()
    {
        CompleteMemoPlacement("image");
    }

    private void CompleteVoiceMemoPlacement()
    {
        CompleteMemoPlacement("voice");
    }

    private void CompleteChecklistMemoPlacement()
    {
        CompleteMemoPlacement("checklist");
    }

    private void CompleteMemoPlacement(string kind)
    {
        if (!memoPlacementPositionSelected)
            return;

        MapScanSession.CompleteMemoPlacement(
            memoPlacementCandidatePosition,
            memoPlacementCandidateRotation,
            kind);
        MapScanSession.RequestMemoPlacementWriting();
    }

    public void SetMemoPlacementWritingActive(bool active)
    {
        memoPlacementWritingActive = active;
        scanHudDocument.rootVisualElement.style.display = active ? DisplayStyle.None : DisplayStyle.Flex;
        arCamera.gameObject.SetActive(!active);
        memoPlacementRoot.SetActive(!active);
        MemoAnchor.UI.ScreenSpaceUIToolkitBlurRendererFeature.SetOutputFrozen(!active);
    }

    public static bool TryCreateMeshFromPly(byte[] data, out Mesh mesh, out string error)
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

            var maxDeltaMs = GetRgbDepthTimestampLimitSeconds() * 1000d;
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
                    depth_unit = "meters; DepthUint16 provider frames are normalized to contiguous DepthFloat32",
                    depth_little_endian = BitConverter.IsLittleEndian,
                    invalid_depth_policy = "0, NaN, and Inf are invalid; Android DepthUint16 is normalized to meters before writing",
                    confidence_value_meaning = IsAndroidDepthScan
                        ? "ARCore confidence normalized on capture: 0 low, 1 medium, 2 high"
                        : "ARKit environment depth confidence raw values: 0 low, 1 medium, 2 high",
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
        else if (IsAndroidDepthScan && TryGetCenterDepthSurfacePoint(keyframe, out surfacePoint))
        {
            keyframe.HasSurfacePoint = true;
            keyframe.SurfacePoint = surfacePoint;
        }

        keyframes.Add(keyframe);
        lastKeyframePosition = position;
        lastKeyframeRotation = rotation;
        hasLastKeyframePose = true;
        nextKeyframeCaptureTime = Time.unscaledTime + keyframeIntervalSeconds;

    }

    private bool TryCreateCameraTexture(out CameraTextureFrame frame, bool includeDepth = true)
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

            if (includeDepth && captureDepthForReconstruction && arOcclusionManager)
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
                        return false;
                    }

                    var rgbDepthTimestampLimit = GetRgbDepthTimestampLimitSeconds();
                    var rgbConfidenceTimestampLimit = GetRgbConfidenceTimestampLimitSeconds();
                    if (rgbDepthDelta > rgbDepthTimestampLimit ||
                        rgbConfidenceDelta > rgbConfidenceTimestampLimit)
                    {
                        skippedUnsyncedDepthFrames++;
                        nextKeyframeCaptureTime = Time.unscaledTime + 0.12f;
                        return false;
                    }
                }
            }

            var requestedWidth = Mathf.Clamp(keyframeTextureWidth, 1, 1920);
            var targetWidth = Mathf.Min(requestedWidth, image.width);
            var aspect = image.height / (float)image.width;
            var targetHeight = Mathf.Clamp(Mathf.RoundToInt(targetWidth * aspect), 1, image.height);
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

        XRCpuImage image;
        var acquired = IsAndroidDepthScan
            ? arOcclusionManager.TryAcquireRawEnvironmentDepthCpuImage(out image)
            : arOcclusionManager.TryAcquireEnvironmentDepthCpuImage(out image);
        if (!acquired)
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

        if (image.format == XRCpuImage.Format.DepthUint16)
            return CopyDepthUint16AsFloat(image, kind);

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

        var frame = new CpuImageFrame
        {
            Kind = kind,
            Width = image.width,
            Height = image.height,
            Format = image.format.ToString(),
            Timestamp = image.timestamp,
            Planes = planes
        };

        if (IsAndroidDepthScan &&
            string.Equals(kind, "environment_depth_confidence", StringComparison.Ordinal) &&
            image.format == XRCpuImage.Format.OneComponent8)
        {
            NormalizeAndroidConfidence(frame);
        }

        return frame;
    }

    private static void NormalizeAndroidConfidence(CpuImageFrame frame)
    {
        var plane = frame.Planes[0];
        for (var y = 0; y < frame.Height; y++)
        {
            var row = y * plane.RowStride;
            for (var x = 0; x < frame.Width; x++)
            {
                var offset = row + x * plane.PixelStride;
                if (offset < 0 || offset >= plane.Data.Length)
                    continue;

                var confidence = plane.Data[offset];
                plane.Data[offset] = confidence >= 192 ? (byte)2 : confidence >= 64 ? (byte)1 : (byte)0;
            }
        }
    }

    private static CpuImageFrame CopyDepthUint16AsFloat(XRCpuImage image, string kind)
    {
        var source = image.GetPlane(0);
        var depthMeters = new float[image.width * image.height];

        for (var y = 0; y < image.height; y++)
        {
            var sourceRow = y * source.rowStride;
            var destinationRow = y * image.width;
            for (var x = 0; x < image.width; x++)
            {
                var sourceOffset = sourceRow + x * source.pixelStride;
                if (sourceOffset < 0 || sourceOffset + 1 >= source.data.Length)
                    continue;

                var millimeters = source.data[sourceOffset] | source.data[sourceOffset + 1] << 8;
                depthMeters[destinationRow + x] = millimeters * 0.001f;
            }
        }

        var data = new byte[depthMeters.Length * sizeof(float)];
        Buffer.BlockCopy(depthMeters, 0, data, 0, data.Length);
        return new CpuImageFrame
        {
            Kind = kind,
            Width = image.width,
            Height = image.height,
            Format = XRCpuImage.Format.DepthFloat32.ToString(),
            Timestamp = image.timestamp,
            Planes = new[]
            {
                new CpuImagePlaneFrame
                {
                    RowStride = image.width * sizeof(float),
                    PixelStride = sizeof(float),
                    Data = data
                }
            }
        };
    }

    private float GetRgbDepthTimestampLimitSeconds()
    {
        var configuredLimit = IsAndroidDepthScan
            ? Mathf.Max(maxRgbDepthTimestampDeltaSeconds, androidMaxRgbDepthTimestampDeltaSeconds)
            : maxRgbDepthTimestampDeltaSeconds;
        return Mathf.Max(0.001f, configuredLimit);
    }

    private float GetRgbConfidenceTimestampLimitSeconds()
    {
        var configuredLimit = IsAndroidDepthScan
            ? Mathf.Max(maxRgbConfidenceTimestampDeltaSeconds, androidMaxRgbDepthTimestampDeltaSeconds)
            : maxRgbConfidenceTimestampDeltaSeconds;
        return Mathf.Max(0.001f, configuredLimit);
    }

    private bool IsEnvironmentDepthUnsupported()
    {
        return arOcclusionManager &&
               arOcclusionManager.descriptor != null &&
               arOcclusionManager.descriptor.environmentDepthImageSupported == Supported.Unsupported;
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

    private bool TryGetCenterDepthSurfacePoint(VisualKeyframe keyframe, out Vector3 surfacePoint)
    {
        surfacePoint = default;
        var depth = keyframe.Depth;
        if (depth == null)
            return false;

        var centerX = depth.Width / 2;
        var centerY = depth.Height / 2;
        var searchStep = Mathf.Max(1, Mathf.Min(depth.Width, depth.Height) / 40);
        for (var radius = 0; radius <= 3; radius++)
        {
            for (var yOffset = -radius; yOffset <= radius; yOffset++)
            {
                for (var xOffset = -radius; xOffset <= radius; xOffset++)
                {
                    if (radius > 0 && Mathf.Abs(xOffset) != radius && Mathf.Abs(yOffset) != radius)
                        continue;

                    var x = Mathf.Clamp(centerX + xOffset * searchStep, 0, depth.Width - 1);
                    var y = Mathf.Clamp(centerY + yOffset * searchStep, 0, depth.Height - 1);
                    if (!TryReadDepthMeters(depth, x, y, out var depthMeters) ||
                        depthMeters < androidDepthMinMeters ||
                        depthMeters > androidDepthMaxMeters)
                    {
                        continue;
                    }

                    if (TryReadConfidence(keyframe.Confidence, depth, x, y, out var confidence) && confidence == 0)
                        continue;

                    if (TryUnprojectDepthPixel(keyframe, x, y, depthMeters, out surfacePoint))
                        return true;
                }
            }
        }

        return false;
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

    private void ShowPreviewCamera(Bounds bounds, bool showOnScreen = true)
    {
        EnsurePreviewCamera();
        previewBounds = bounds;
        previewCenter = bounds.center;

        if (showOnScreen)
        {
            if (arCamera)
                arCamera.enabled = false;
            if (arCameraBackground)
                arCameraBackground.enabled = false;
        }

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
            previewCamera.enabled = showOnScreen;
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
        var desiredShader = FindPreviewShader(previewHasProjectedColors);

        if (previewMaterial && previewMaterial.shader == desiredShader)
            return previewMaterial;

        previewMaterial = CreatePreviewMaterial(desiredShader, previewHasProjectedColors);
        return previewMaterial;
    }

    public static Material CreateReconstructionPreviewMaterial(Mesh mesh)
    {
        var colors = mesh.colors;
        var hasProjectedColors = colors != null && colors.Length == mesh.vertexCount;
        return CreatePreviewMaterial(FindPreviewShader(hasProjectedColors), hasProjectedColors);
    }

    private static Shader FindPreviewShader(bool hasProjectedColors)
    {
        return hasProjectedColors
            ? Shader.Find("MemoAnchor/Preview Vertex Colors") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit")
            : Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Unlit");
    }

    private static Material CreatePreviewMaterial(Shader shader, bool hasProjectedColors)
    {
        var material = new Material(shader)
        {
            name = "Runtime Clean Map Preview Material"
        };

        var color = hasProjectedColors ? Color.white : new Color(0.58f, 0.7f, 0.74f, 1f);
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", 0.2f);
        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", 0f);
        if (material.HasProperty("_Cull"))
            material.SetFloat("_Cull", (float)CullMode.Off);

        return material;
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

    private async Awaitable SmoothVerticesAsync(List<Vector3> vertices, List<int> triangles)
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

            await Awaitable.NextFrameAsync();
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
        SyncCompletionPreviewCamera();
    }

    private static bool IsPointerOverUi()
    {
        return EventSystem.current && EventSystem.current.IsPointerOverGameObject();
    }

    private static bool IsTouchOverUi(Touch touch)
    {
        return EventSystem.current && EventSystem.current.IsPointerOverGameObject(touch.fingerId);
    }

    private void SetScanCaptureEnabled(bool enabled)
    {
        if (meshManager)
            meshManager.enabled = enabled && !IsAndroidDepthScan;

        if (planeManager)
            planeManager.enabled = enabled;
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
        var geometryScore = IsAndroidDepthScan
            ? ScoreRatio(depthFrameCount, recommendedMinKeyframes) * 15f
            : ScoreRatio(triangleCount, recommendedMinMeshTriangles) * 15f;
        var planeScore = ScoreRatio(planeCount, recommendedMinDetectedPlanes) * 10f;
        var score = Mathf.Clamp(durationScore + keyframeScore + pathScore + confidenceScore + surfaceScore + geometryScore + planeScore, 0f, 100f);

        var guidance = BuildScanGuidance(
            duration,
            keyframeCount,
            depthFrameCount,
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
        int depthFrameCount,
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

        if (IsAndroidDepthScan && depthFrameCount < recommendedMinKeyframes)
            guidance.Add("Keep textured surfaces in view until more ARCore Depth frames are captured.");

        if (cameraPathMeters < recommendedMinCameraPathMeters)
            guidance.Add("Walk along the wall/corners; avoid only rotating in place.");

        if (averageDepthConfidence < recommendedMinDepthConfidenceRatio)
            guidance.Add("Point at well-lit matte surfaces and slow down until depth is stable.");

        if (requireSynchronizedRgbdKeyframes && averageRgbdTimestampDeltaMs > GetRgbDepthTimestampLimitSeconds() * 750f)
            guidance.Add("Keep moving slowly; RGB-D synchronization is drifting.");

        if (surfaceHitRatio < recommendedMinSurfaceHitRatio)
            guidance.Add("Keep the center of the camera on real surfaces, not empty space.");

        if (coverageCellCount > 0 && coverageWeakCellRatio > 0.35f)
            guidance.Add("Rescan the red coverage cells from a second angle before stopping.");

        if (!IsAndroidDepthScan && triangleCount < recommendedMinMeshTriangles)
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
        public string runtimePlatform;
        public bool depthOnlyReconstruction;
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

    [Serializable]
    private sealed class MemoPlacementLocalizationDto
    {
        public bool localized;
        public float confidence;
        public string message;
        public int matchedFrameId;
        public float[] cameraPosition;
        public float[] cameraRotation;
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
        scanHudStartButton.SetEnabled(!MapScanSession.IsViewingStoredResult
            && !reconstructionPackageRunning
            && scanMode != ScanMode.Scanning);
        scanHudStopButton.SetEnabled(!MapScanSession.IsViewingStoredResult
            && !reconstructionPackageRunning
            && scanMode == ScanMode.Scanning);
        scanHudBackButton.SetEnabled(!mapConfirmationRunning && !reconstructionPackageRunning);
        bool canReviewCompletion = scanMode == ScanMode.Preview
            && reconstructionCompletedSuccessfully
            && !reconstructionPackageRunning
            && !mapSaveRunning
            && !MapScanSession.IsViewingStoredResult;
        scanHudRegenerateButton.SetEnabled(canReviewCompletion);
        scanHudSaveButton.SetEnabled(canReviewCompletion);

        UpdateScanHud();
    }
}
