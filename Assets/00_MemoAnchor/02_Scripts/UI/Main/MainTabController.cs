using UnityEngine;
using UnityEngine.UIElements;

namespace MemoAnchor.UI
{
    [RequireComponent(typeof(MainTabView))]
    public class MainTabController : MonoBehaviour
    {
        private const string NavTapDownClass = "is-tapping-down";
        private const int NavTapDownDurationMs = 105;

        private MainTabView _view;
        private int _currentTabIndex;

        private void Awake()
        {
            TryGetComponent<MainTabView>(out _view);
        }

        private void Start()
        {
            _view.HomeButton.clicked += OnClickHome;
            _view.MenuButton.clicked += OnClickMenu;
            _view.ScanButton.clicked += OnClickScan;
            _view.MapButton.clicked += OnClickMap;
            _view.ProfileButton.clicked += OnClickProfile;
            _view.TabViewport.RegisterCallback<GeometryChangedEvent>(OnViewportGeometryChanged);
            ShowTab(0);
        }

        private void OnDisable()
        {
            _view.HomeButton.clicked -= OnClickHome;
            _view.MenuButton.clicked -= OnClickMenu;
            _view.ScanButton.clicked -= OnClickScan;
            _view.MapButton.clicked -= OnClickMap;
            _view.ProfileButton.clicked -= OnClickProfile;
            _view.TabViewport.UnregisterCallback<GeometryChangedEvent>(OnViewportGeometryChanged);
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
            ShowTab(2);
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

        private void ShowTab(int tabIndex)
        {
            _currentTabIndex = Mathf.Clamp(tabIndex, 0, 4);

            SetState(_view.HomeButton, _currentTabIndex == 0);
            SetState(_view.MenuButton, _currentTabIndex == 1);
            SetState(_view.ScanButton, _currentTabIndex == 2);
            SetState(_view.MapButton, _currentTabIndex == 3);
            SetState(_view.ProfileButton, _currentTabIndex == 4);
            UpdateTabStripOffset();
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
            button.RemoveFromClassList(NavTapDownClass);
            button.AddToClassList(NavTapDownClass);
            button.schedule.Execute(() => button.RemoveFromClassList(NavTapDownClass)).ExecuteLater(NavTapDownDurationMs);
        }
    }
}
