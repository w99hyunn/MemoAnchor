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
            ShowTab("home");
        }

        private void OnDisable()
        {
            _view.HomeButton.clicked -= OnClickHome;
            _view.MenuButton.clicked -= OnClickMenu;
            _view.ScanButton.clicked -= OnClickScan;
            _view.MapButton.clicked -= OnClickMap;
            _view.ProfileButton.clicked -= OnClickProfile;
        }

        private void OnClickHome()
        {
            PlayNavTapAnimation(_view.HomeButton);
            ShowTab("home");
        }

        private void OnClickMenu()
        {
            PlayNavTapAnimation(_view.MenuButton);
            ShowTab("menu");
        }

        private void OnClickScan()
        {
            PlayNavTapAnimation(_view.ScanButton);
            ShowTab("scan");
        }

        private void OnClickMap()
        {
            PlayNavTapAnimation(_view.MapButton);
            ShowTab("map");
        }

        private void OnClickProfile()
        {
            PlayNavTapAnimation(_view.ProfileButton);
            ShowTab("profile");
        }

        private void ShowTab(string tab)
        {
            SetState(_view.HomeButton, _view.HomeTab, tab == "home");
            SetState(_view.MenuButton, _view.MenuTab, tab == "menu");
            SetState(_view.ScanButton, _view.ScanTab, tab == "scan");
            SetState(_view.MapButton, _view.MapTab, tab == "map");
            SetState(_view.ProfileButton, _view.ProfileTab, tab == "profile");
        }

        private static void SetState(Button button, VisualElement page, bool active)
        {
            button.EnableInClassList("is-active", active);
            page.EnableInClassList("is-visible", active);
        }

        private static void PlayNavTapAnimation(Button button)
        {
            button.RemoveFromClassList(NavTapDownClass);
            button.AddToClassList(NavTapDownClass);
            button.schedule.Execute(() => button.RemoveFromClassList(NavTapDownClass)).ExecuteLater(NavTapDownDurationMs);
        }
    }
}
