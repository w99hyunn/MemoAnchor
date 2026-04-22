using UnityEngine;
using UnityEngine.UIElements;

namespace MemoAnchor.UI
{
    [RequireComponent(typeof(UIDocument))]
    public class MainTabController : MonoBehaviour
    {
        private UIDocument _uiDocument;
        private Button _homeButton;
        private Button _menuButton;
        private Button _scanButton;
        private Button _mapButton;
        private Button _profileButton;

        private VisualElement _homeTab;
        private VisualElement _menuTab;
        private VisualElement _scanTab;
        private VisualElement _mapTab;
        private VisualElement _profileTab;
        private ScrollView _memoScroll;

        private bool _isDragging;
        private float _dragStartX;
        private float _dragStartScrollX;

        private void Awake()
        {
            _uiDocument = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            VisualElement root = _uiDocument.rootVisualElement;
            _homeButton = root.Q<Button>("nav-home");
            _menuButton = root.Q<Button>("nav-menu");
            _scanButton = root.Q<Button>("nav-scan");
            _mapButton = root.Q<Button>("nav-map");
            _profileButton = root.Q<Button>("nav-profile");

            _homeTab = root.Q<VisualElement>("tab-home");
            _menuTab = root.Q<VisualElement>("tab-menu");
            _scanTab = root.Q<VisualElement>("tab-scan");
            _mapTab = root.Q<VisualElement>("tab-map");
            _profileTab = root.Q<VisualElement>("tab-profile");
            _memoScroll = root.Q<ScrollView>("memo-scroll");

            _homeButton.clicked += OnClickHome;
            _menuButton.clicked += OnClickMenu;
            _scanButton.clicked += OnClickScan;
            _mapButton.clicked += OnClickMap;
            _profileButton.clicked += OnClickProfile;

            _memoScroll.RegisterCallback<PointerDownEvent>(OnScrollDown, TrickleDown.TrickleDown);
            _memoScroll.RegisterCallback<PointerMoveEvent>(OnScrollMove, TrickleDown.TrickleDown);
            _memoScroll.RegisterCallback<PointerUpEvent>(OnScrollUp, TrickleDown.TrickleDown);

            ShowTab("home");
        }

        private void OnDisable()
        {
            _homeButton.clicked -= OnClickHome;
            _menuButton.clicked -= OnClickMenu;
            _scanButton.clicked -= OnClickScan;
            _mapButton.clicked -= OnClickMap;
            _profileButton.clicked -= OnClickProfile;

            _memoScroll.UnregisterCallback<PointerDownEvent>(OnScrollDown, TrickleDown.TrickleDown);
            _memoScroll.UnregisterCallback<PointerMoveEvent>(OnScrollMove, TrickleDown.TrickleDown);
            _memoScroll.UnregisterCallback<PointerUpEvent>(OnScrollUp, TrickleDown.TrickleDown);
        }

        private void OnClickHome() => ShowTab("home");
        private void OnClickMenu() => ShowTab("menu");
        private void OnClickScan() => ShowTab("scan");
        private void OnClickMap() => ShowTab("map");
        private void OnClickProfile() => ShowTab("profile");

        private void OnScrollDown(PointerDownEvent evt)
        {
            if (evt.button != 0)
            {
                return;
            }

            _isDragging = true;
            _dragStartX = evt.position.x;
            _dragStartScrollX = _memoScroll.scrollOffset.x;
        }

        private void OnScrollMove(PointerMoveEvent evt)
        {
            if (!_isDragging)
            {
                return;
            }

            float dx = evt.position.x - _dragStartX;
            float target = _dragStartScrollX - dx;

            float contentWidth = _memoScroll.contentContainer.worldBound.width;
            float viewportWidth = _memoScroll.contentViewport.worldBound.width;
            float max = Mathf.Max(0f, contentWidth - viewportWidth);
            float x = Mathf.Clamp(target, 0f, max);

            _memoScroll.scrollOffset = new Vector2(x, _memoScroll.scrollOffset.y);
        }

        private void OnScrollUp(PointerUpEvent _)
        {
            _isDragging = false;
        }

        private void ShowTab(string tab)
        {
            SetState(_homeButton, _homeTab, tab == "home");
            SetState(_menuButton, _menuTab, tab == "menu");
            SetState(_scanButton, _scanTab, tab == "scan");
            SetState(_mapButton, _mapTab, tab == "map");
            SetState(_profileButton, _profileTab, tab == "profile");
        }

        private static void SetState(Button button, VisualElement page, bool active)
        {
            button.EnableInClassList("is-active", active);
            page.EnableInClassList("is-visible", active);
        }
    }
}
