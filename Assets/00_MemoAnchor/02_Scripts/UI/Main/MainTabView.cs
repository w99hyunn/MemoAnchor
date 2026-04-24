using UnityEngine;
using UnityEngine.UIElements;

namespace MemoAnchor.UI
{
    [RequireComponent(typeof(UIDocument))]
    public class MainTabView : MonoBehaviour
    {
        private UIDocument _uiDocument;
        private Button _homeButton;
        private Button _menuButton;
        private Button _scanButton;
        private Button _mapButton;
        private Button _profileButton;

        private VisualElement _tabViewport;
        private VisualElement _tabStrip;
        private VisualElement _homeTab;
        private VisualElement _menuTab;
        private VisualElement _scanTab;
        private VisualElement _mapTab;
        private VisualElement _profileTab;
        private ScrollView _memoScroll;
        private ScrollView _scanMapScroll;

        public Button HomeButton => _homeButton;
        public Button MenuButton => _menuButton;
        public Button ScanButton => _scanButton;
        public Button MapButton => _mapButton;
        public Button ProfileButton => _profileButton;
        public VisualElement TabViewport => _tabViewport;
        public VisualElement HomeTab => _homeTab;
        public VisualElement MenuTab => _menuTab;
        public VisualElement ScanTab => _scanTab;
        public VisualElement MapTab => _mapTab;
        public VisualElement ProfileTab => _profileTab;
        public ScrollView MemoScroll => _memoScroll;
        public ScrollView ScanMapScroll => _scanMapScroll;

        private void Awake()
        {
            TryGetComponent<UIDocument>(out _uiDocument);
            VisualElement root = _uiDocument.rootVisualElement;
            _homeButton = root.Q<Button>("nav-home");
            _menuButton = root.Q<Button>("nav-menu");
            _scanButton = root.Q<Button>("nav-scan");
            _mapButton = root.Q<Button>("nav-map");
            _profileButton = root.Q<Button>("nav-profile");

            _tabViewport = root.Q<VisualElement>("tab-viewport");
            _tabStrip = root.Q<VisualElement>("tab-strip");
            _homeTab = root.Q<VisualElement>("tab-home");
            _menuTab = root.Q<VisualElement>("tab-menu");
            _scanTab = root.Q<VisualElement>("tab-scan");
            _mapTab = root.Q<VisualElement>("tab-map");
            _profileTab = root.Q<VisualElement>("tab-profile");
            _memoScroll = root.Q<ScrollView>("memo-scroll");
            _scanMapScroll = root.Q<ScrollView>("scan-map-scroll");
        }

        public void SetTabStripOffset(float x)
        {
            _tabStrip.style.left = x;
        }

        public void SetTabPageWidth(float width)
        {
            _homeTab.style.width = width;
            _menuTab.style.width = width;
            _scanTab.style.width = width;
            _mapTab.style.width = width;
            _profileTab.style.width = width;
            _tabStrip.style.width = width * 5f;
        }
    }
}
