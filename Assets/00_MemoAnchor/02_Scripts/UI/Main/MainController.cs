using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace MemoAnchor.UI
{
    [RequireComponent(typeof(MainView), typeof(Tab_ScanView), typeof(Tab_ScanController))]
    [RequireComponent(typeof(FadeTransition))]
    public class MainController : MonoBehaviour
    {
        private const string NAV_TAP_DOWN_CLASS = "is-tapping-down";
        private const int NAV_TAP_DOWN_DURATION_MS = 105;

        private MainView _view;
        private Tab_ScanView _scanView;
        private Tab_ScanController _scanController;
        private FadeTransition _fadeTransition;
        private Camera _mainCamera;
        private int _currentTabIndex;
        private bool _isScanNavModeActive;
        private bool _isRegistered;
        private bool _isOpeningScanScene;

        private void Awake()
        {
            TryGetComponent<MainView>(out _view);
            TryGetComponent<Tab_ScanView>(out _scanView);
            TryGetComponent<Tab_ScanController>(out _scanController);
            TryGetComponent<FadeTransition>(out _fadeTransition);
            _mainCamera = Camera.main;
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
            _scanView.ScanStartReadinessChanged += UpdateScanStartAvailability;
            _view.TabViewport.RegisterCallback<GeometryChangedEvent>(OnViewportGeometryChanged);
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            _isRegistered = true;
            ShowTab(0);
            UpdateScanStartAvailability();
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

            Scene mainScene = gameObject.scene;
            if (mainScene.IsValid() && mainScene.isLoaded)
            {
                SceneManager.SetActiveScene(mainScene);
            }

            _mainCamera.gameObject.SetActive(true);
            _view.SetScanSceneActive(false);

            bool returnToMap = MapScanSession.ReturnToMapOnClose;
            bool completedNewScan = returnToMap && !MapScanSession.IsViewingStoredResult;
            string returnMapId = MapScanSession.HasActiveMap ? MapScanSession.MapId : string.Empty;
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
            }

            MapScanSession.Clear();
            _ = _fadeTransition.FadeInAsync();
        }

        private void ShowTab(int tabIndex)
        {
            _view.HideProfileAccountSettings();
            int nextTabIndex = Mathf.Clamp(tabIndex, 0, 4);
            if (_currentTabIndex != nextTabIndex)
            {
                _view.HideMemoOverlayPages();
            }

            _currentTabIndex = nextTabIndex;
            if (_currentTabIndex != 2)
            {
                _isScanNavModeActive = false;
                _view.HideScanActionDialog();
            }

            SetState(_view.HomeButton, _currentTabIndex == 0);
            SetState(_view.MenuButton, _currentTabIndex == 1);
            SetState(_view.ScanButton, _currentTabIndex == 2);
            SetState(_view.MapButton, _currentTabIndex == 3);
            SetState(_view.ProfileButton, _currentTabIndex == 4);
            _view.SetScanNavMode(_isScanNavModeActive);
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
