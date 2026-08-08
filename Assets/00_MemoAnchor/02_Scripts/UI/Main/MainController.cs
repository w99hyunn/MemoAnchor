using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.InputSystem;
#endif
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace MemoAnchor.UI
{
    [RequireComponent(typeof(MainView), typeof(Tab_ScanView), typeof(Tab_ScanController))]
    [RequireComponent(typeof(Tab_HomeView))]
    [RequireComponent(typeof(FadeTransition))]
    public class MainController : MonoBehaviour
    {
        private const string NAV_TAP_DOWN_CLASS = "is-tapping-down";
        private const int NAV_TAP_DOWN_DURATION_MS = 105;
        private const float REMOTE_SYNC_INTERVAL_SECONDS = 5f;

        private MainView _view;
        private Tab_HomeView _homeView;
        private Tab_ScanView _scanView;
        private Tab_ScanController _scanController;
        private FadeTransition _fadeTransition;
        private Camera _mainCamera;
        private int _currentTabIndex;
        private bool _isScanNavModeActive;
        private bool _isMapNavModeActive;
        private bool _isRegistered;
        private bool _isOpeningScanScene;
        private bool _isMemoPlacementWriting;
        private global::ARKitMeshScanController _memoPlacementScanController;
        private float _lastBackPressedTime = float.NegativeInfinity;
        private float _nextRemoteSyncTime;
        private bool _hasApplicationFocus = true;

        private void Awake()
        {
            TryGetComponent<MainView>(out _view);
            TryGetComponent<Tab_HomeView>(out _homeView);
            TryGetComponent<Tab_ScanView>(out _scanView);
            TryGetComponent<Tab_ScanController>(out _scanController);
            TryGetComponent<FadeTransition>(out _fadeTransition);
            _mainCamera = Camera.main;
        }

        private void Update()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                HandleAndroidBack();
            }
#endif

            if (_isRegistered && _hasApplicationFocus && Time.unscaledTime >= _nextRemoteSyncTime)
            {
                RequestRemoteSync();
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            _hasApplicationFocus = hasFocus;
            if (hasFocus && _isRegistered)
            {
                RequestRemoteSync();
            }
        }

        private void HandleAndroidBack()
        {
            if (PopupManager.TryHandleSystemBack())
            {
                return;
            }

            if (_currentTabIndex == 0 && _homeView.TryHandleSystemBack())
            {
                return;
            }

            if (_currentTabIndex == 2 && _scanView.TryHandleSystemBack())
            {
                return;
            }

            if (_view.TryHandleSystemBack(_currentTabIndex))
            {
                return;
            }

            if (Time.unscaledTime - _lastBackPressedTime <= 2f)
            {
                Application.Quit();
                return;
            }

            _lastBackPressedTime = Time.unscaledTime;
            ShowAndroidToast("뒤로가기 버튼을 한 번 더 누르면 종료됩니다.");
        }

        private static void ShowAndroidToast(string message)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using AndroidJavaClass unityPlayer = new("com.unity3d.player.UnityPlayer");
            using AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
            {
                using AndroidJavaClass currentUnityPlayer = new("com.unity3d.player.UnityPlayer");
                using AndroidJavaObject currentActivity = currentUnityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                using AndroidJavaClass toastClass = new("android.widget.Toast");
                using AndroidJavaObject toast = toastClass.CallStatic<AndroidJavaObject>(
                    "makeText",
                    currentActivity,
                    message,
                    0);
                toast.Call("show");
            }));
#endif
        }

        private void Start()
        {
            _view.HomeButton.clicked += OnClickHome;
            _view.MenuButton.clicked += OnClickMenu;
            _view.ScanButton.clicked += OnClickScan;
            _view.MapButton.clicked += OnClickMap;
            _view.ProfileButton.clicked += OnClickProfile;
            _view.ScanStartButton.clicked += OnClickScanStart;
            _view.TabSwitchRequested += ShowTab;
            _view.MapMemoPlacementRequested += OnMapMemoPlacementRequested;
            _view.MemoPlacementResumeRequested += ResumeMemoPlacement;
            _view.MemoPlacementSceneCloseRequested += CloseSuspendedMemoPlacementScene;
            _view.MapNavAvailabilityChanged += OnMapNavAvailabilityChanged;
            MapScanSession.MemoPlacementWritingRequested += ShowMemoPlacementWritingPage;
            _scanView.ScanStartReadinessChanged += UpdateScanStartAvailability;
            _view.TabViewport.RegisterCallback<GeometryChangedEvent>(OnViewportGeometryChanged);
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            _isRegistered = true;
            _nextRemoteSyncTime = Time.unscaledTime + REMOTE_SYNC_INTERVAL_SECONDS;
            ShowTab(0);
            UpdateScanStartAvailability();
        }

        private void RequestRemoteSync()
        {
            _nextRemoteSyncTime = Time.unscaledTime + REMOTE_SYNC_INTERVAL_SECONDS;
            _ = _view.RefreshRemoteChangesAsync();
        }

        private void OnDisable()
        {
            if (!_isRegistered)
            {
                return;
            }

            _view.HomeButton.clicked -= OnClickHome;
            _view.MenuButton.clicked -= OnClickMenu;
            _view.ScanButton.clicked -= OnClickScan;
            _view.MapButton.clicked -= OnClickMap;
            _view.ProfileButton.clicked -= OnClickProfile;
            _view.ScanStartButton.clicked -= OnClickScanStart;
            _view.TabSwitchRequested -= ShowTab;
            _view.MapMemoPlacementRequested -= OnMapMemoPlacementRequested;
            _view.MemoPlacementResumeRequested -= ResumeMemoPlacement;
            _view.MemoPlacementSceneCloseRequested -= CloseSuspendedMemoPlacementScene;
            _view.MapNavAvailabilityChanged -= OnMapNavAvailabilityChanged;
            MapScanSession.MemoPlacementWritingRequested -= ShowMemoPlacementWritingPage;
            _scanView.ScanStartReadinessChanged -= UpdateScanStartAvailability;
            _view.TabViewport.UnregisterCallback<GeometryChangedEvent>(OnViewportGeometryChanged);
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            _isRegistered = false;
        }

        private void OnClickHome()
        {
            PlayNavTapAnimation(_view.HomeButton);
            ShowTab(0);
        }

        private void OnClickMenu()
        {
            PlayNavTapAnimation(_view.MenuButton);
            ShowTab(1);
        }

        private void OnClickScan()
        {
            PlayNavTapAnimation(_view.ScanButton);

            if (_isScanNavModeActive)
            {
                _isScanNavModeActive = false;
                ShowTab(2);
                return;
            }

            if (_currentTabIndex == 2)
            {
                _isScanNavModeActive = true;
                ShowTab(2);
                return;
            }

            _view.ShowScanActionDialog(OnClickScanActionCreate, OnClickScanActionJoin);
        }

        private void OnClickMap()
        {
            PlayNavTapAnimation(_view.MapButton);

            if (_isMapNavModeActive)
            {
                _isMapNavModeActive = false;
                ShowTab(3);
                return;
            }

            _isMapNavModeActive = _view.IsMapNavAvailable;
            ShowTab(3);
        }

        private void OnClickProfile()
        {
            PlayNavTapAnimation(_view.ProfileButton);
            ShowTab(4);
        }

        private void OnClickScanStart()
        {
            if (!_scanView.HasSpaceName())
            {
                _scanView.HighlightSpaceNameError();
            }

            if (!_scanView.HasSelectedAddress())
            {
                _scanView.HighlightAddressError();
            }

            if (!_scanView.IsScanStartReady())
            {
                return;
            }

            _ = StartScanAsync();
        }

        private async Awaitable StartScanAsync()
        {
            MapScanSession.BeginScan(_scanController.CreateScanDraft());
            await OpenScanSceneAsync();
        }

        private void OnMapMemoPlacementRequested(ScanMapItem map)
        {
            MapScanSession.BeginMemoPlacement(
                map.id,
                map.reconstructionScanId,
                map.reconstructionResultFile,
                _view.GetSpatialMemoMarkers(map));
            _ = OpenScanSceneAsync();
        }

        private void ShowMemoPlacementWritingPage()
        {
            if (_isMemoPlacementWriting)
            {
                return;
            }

            _memoPlacementScanController = FindFirstObjectByType<global::ARKitMeshScanController>();
            _memoPlacementScanController.SetMemoPlacementWritingActive(true);

            Scene mainScene = gameObject.scene;
            SceneManager.SetActiveScene(mainScene);
            _mainCamera.gameObject.SetActive(true);
            _view.SetScanSceneActive(false);
            _view.PreferMapSelection(MapScanSession.MapId, null, null);
            ShowTab(3);
            _view.ShowMapMemoCreatePage(MapScanSession.MapId, MapScanSession.MemoPlacementKind);
            _isMemoPlacementWriting = true;
            _ = _fadeTransition.FadeInAsync();
        }

        private void ResumeMemoPlacement()
        {
            if (!_isMemoPlacementWriting)
            {
                return;
            }

            _view.SetScanSceneActive(true);
            _mainCamera.gameObject.SetActive(false);
            Scene scanScene = SceneManager.GetSceneByName(MapScanSession.SCAN_SCENE_NAME);
            SceneManager.SetActiveScene(scanScene);
            _memoPlacementScanController.SetMemoPlacementWritingActive(false);
            _isMemoPlacementWriting = false;
        }

        private void CloseSuspendedMemoPlacementScene()
        {
            if (!_isMemoPlacementWriting)
            {
                return;
            }

            _isMemoPlacementWriting = false;
            Scene scanScene = SceneManager.GetSceneByName(MapScanSession.SCAN_SCENE_NAME);
            SceneManager.UnloadSceneAsync(scanScene);
        }

        private async Awaitable OpenScanSceneAsync()
        {
            if (_isOpeningScanScene || SceneManager.GetSceneByName(MapScanSession.SCAN_SCENE_NAME).isLoaded)
            {
                return;
            }

            _isOpeningScanScene = true;
            await _fadeTransition.FadeOutAsync();
            _view.SetScanSceneActive(true);
            _mainCamera.gameObject.SetActive(false);

            SceneManager.LoadScene(MapScanSession.SCAN_SCENE_NAME, LoadSceneMode.Additive);
            Scene scanScene = SceneManager.GetSceneByName(MapScanSession.SCAN_SCENE_NAME);
            if (scanScene.IsValid() && scanScene.isLoaded)
            {
                SceneManager.SetActiveScene(scanScene);
            }
            _isOpeningScanScene = false;
        }

        private void OnSceneUnloaded(Scene scene)
        {
            if (scene.name != MapScanSession.SCAN_SCENE_NAME)
            {
                return;
            }

            _isMemoPlacementWriting = false;
            _memoPlacementScanController = null;

            Scene mainScene = gameObject.scene;
            if (mainScene.IsValid() && mainScene.isLoaded)
            {
                SceneManager.SetActiveScene(mainScene);
            }

            _mainCamera.gameObject.SetActive(true);
            _view.SetScanSceneActive(false);

            bool returnToMap = MapScanSession.ReturnToMapOnClose;
            bool completedNewScan = returnToMap && MapScanSession.Mode == MapScanSession.SessionMode.Scan;
            bool memoPlacementCompleted = MapScanSession.IsMemoPlacement && MapScanSession.HasPendingMemoPlacement;
            string returnMapId = MapScanSession.HasActiveMap ? MapScanSession.MapId : string.Empty;
            string memoKind = MapScanSession.MemoPlacementKind;
            if (!returnToMap)
            {
                _isScanNavModeActive = true;
                ShowTab(2);
            }
            else
            {
                if (completedNewScan)
                {
                    _scanView.ResetScanForm();
                }
                _view.PreferMapSelection(
                    returnMapId,
                    MapScanSession.CompletedReconstructionMesh,
                    MapScanSession.CompletedReconstructionMaterial);
                ShowTab(3);
                if (memoPlacementCompleted)
                {
                    _view.ShowMapMemoCreatePage(returnMapId, memoKind);
                }
            }

            MapScanSession.ClearSceneState();
            _ = _fadeTransition.FadeInAsync();
        }

        private void ShowTab(int tabIndex)
        {
            _view.BlurFocusedElement();
            _view.HideProfileAccountSettings();
            int nextTabIndex = Mathf.Clamp(tabIndex, 0, 4);
            bool enteringMapTab = _currentTabIndex != nextTabIndex && nextTabIndex == 3;
            if (_currentTabIndex != nextTabIndex)
            {
                _lastBackPressedTime = float.NegativeInfinity;
                _view.HideMemoOverlayPages();
            }

            _currentTabIndex = nextTabIndex;
            if (_currentTabIndex != 2)
            {
                _isScanNavModeActive = false;
                _view.HideScanActionDialog();
            }
            if (_currentTabIndex != 3)
            {
                _isMapNavModeActive = false;
            }
            else if (enteringMapTab)
            {
                _isMapNavModeActive = _view.IsMapNavAvailable;
            }

            SetState(_view.HomeButton, _currentTabIndex == 0);
            SetState(_view.MenuButton, _currentTabIndex == 1);
            SetState(_view.ScanButton, _currentTabIndex == 2);
            SetState(_view.MapButton, _currentTabIndex == 3);
            SetState(_view.ProfileButton, _currentTabIndex == 4);
            _view.SetScanNavMode(_isScanNavModeActive);
            _view.SetMapNavMode(_isMapNavModeActive);
            _view.SetMapPreviewActive(_currentTabIndex == 3);
            UpdateScanStartAvailability();
            UpdateTabStripOffset();

            if (_currentTabIndex == 3)
            {
                _ = _view.RefreshMapListAsync();
            }
            else if (_currentTabIndex == 1)
            {
                _ = _view.RefreshMemoListAsync();
            }
        }

        private void OnMapNavAvailabilityChanged(bool available)
        {
            if (_currentTabIndex != 3)
            {
                return;
            }

            _isMapNavModeActive = available;
            _view.SetMapNavMode(_isMapNavModeActive);
        }

        private void UpdateScanStartAvailability()
        {
            _view.SetScanStartAvailable(_scanView.IsScanStartReady());
        }

        private void OnClickScanActionCreate()
        {
            _isScanNavModeActive = true;
            ShowTab(2);
        }

        private void OnClickScanActionJoin()
        {
            _view.ShowScanJoinDialog();
        }

        private static void SetState(Button button, bool active)
        {
            button.EnableInClassList("is-active", active);
        }

        private void OnViewportGeometryChanged(GeometryChangedEvent _)
        {
            UpdateTabStripOffset();
        }

        private void UpdateTabStripOffset()
        {
            float width = _view.TabViewport.resolvedStyle.width;
            if (width <= 0f)
            {
                return;
            }

            _view.SetTabPageWidth(width);
            _view.SetTabStripOffset(-_currentTabIndex * width);
        }

        private static void PlayNavTapAnimation(Button button)
        {
            button.RemoveFromClassList(NAV_TAP_DOWN_CLASS);
            button.AddToClassList(NAV_TAP_DOWN_CLASS);
            button.schedule.Execute(() => button.RemoveFromClassList(NAV_TAP_DOWN_CLASS)).ExecuteLater(NAV_TAP_DOWN_DURATION_MS);
        }
    }
}
