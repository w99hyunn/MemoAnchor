using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace MemoAnchor.UI
{
    [RequireComponent(typeof(UIDocument))]
    public partial class MainView : MonoBehaviour
    {
        private const string MEMO_DELETE_OPEN_CLASS = "is-delete-open";
        private const string MEMO_DELETE_PRESS_CLASS = "is-delete-press";
        private const string SELECTED_CLASS = "is-selected";
        private const string HIDDEN_CLASS = "is-hidden";
        private const string ERROR_CLASS = "is-error";
        private const string HOME_ADMIN_MODE_CLASS = "is-admin-mode";
        private const string HOME_WORK_MODE_CLASS = "is-work-mode";
        private const long MEMO_DELETE_LONG_PRESS_MS = 520;
        private const float MEMO_DELETE_LONG_PRESS_MOVE_TOLERANCE = 18f;

        private Button _homeButton, _menuButton, _scanButton, _mapButton, _profileButton, _scanStartButton, _mapMemoAddButton;
        private VisualElement _root, _tabViewport, _tabStrip, _bottomNavWrapper, _bottomNav, _memoFilterBottomBar, _memoDetailBottomBar, _memoCreateBottomBar, _memoTrashBottomBar;
        private VisualElement _homeTab, _menuTab, _scanTab, _mapTab, _profileTab;

        public event Action<int> TabSwitchRequested;
        public event Action<ScanMapItem> MapMemoPlacementRequested;
        public event Action MemoPlacementResumeRequested;
        public event Action MemoPlacementSceneCloseRequested;
        public event Action<bool> MapNavAvailabilityChanged;

        public Button HomeButton => _homeButton;
        public Button MenuButton => _menuButton;
        public Button ScanButton => _scanButton;
        public Button MapButton => _mapButton;
        public Button ProfileButton => _profileButton;
        public Button ScanStartButton => _scanStartButton;
        public VisualElement TabViewport => _tabViewport;
        public bool IsMapNavAvailable => _isMapNavAvailable;

        public bool TryHandleSystemBack(int tabIndex)
        {
            if (tabIndex == 1)
            {
                if (IsVisible(_memoVoiceRecorderPage))
                {
                    RequestCloseMemoVoiceRecorder();
                    return true;
                }

                if (IsVisible(_memoCreatePage))
                {
                    OnClickMemoCreateBack();
                    return true;
                }

                if (IsVisible(_memoDetailPage))
                {
                    OnClickMemoDetailBack();
                    return true;
                }

                if (IsVisible(_memoTrashPage))
                {
                    HideMemoTrashPage();
                    return true;
                }

                if (IsVisible(_memoSearchPage))
                {
                    HideMemoSearchPage();
                    return true;
                }

                if (IsVisible(_memoFilterPage))
                {
                    HideMemoFilterPage();
                    return true;
                }
            }
            else if (tabIndex == 3)
            {
                if (IsVisible(_memoVoiceRecorderPage))
                {
                    RequestCloseMemoVoiceRecorder();
                    return true;
                }

                if (IsVisible(_memoCreatePage))
                {
                    OnClickMemoCreateBack();
                    return true;
                }

                if (_mapFriendInviteOverlay.pickingMode == PickingMode.Position)
                {
                    HideMapFriendInvite();
                    return true;
                }

                if (IsVisible(_mapMemoPage))
                {
                    HideMapMemoPage();
                    return true;
                }
            }
            else if (tabIndex == 4 && IsVisible(_profileAccountSettingsPage))
            {
                HideProfileAccountSettings();
                return true;
            }

            return false;
        }

        private void Awake()
        {
            TryGetComponent<UIDocument>(out var uiDocument);
            TryGetComponent<FadeTransition>(out _fadeTransition);
            _root = uiDocument.rootVisualElement;
            _homeButton = _root.Q<Button>("nav-home");
            _menuButton = _root.Q<Button>("nav-menu");
            _scanButton = _root.Q<Button>("nav-scan");
            _mapButton = _root.Q<Button>("nav-map");
            _profileButton = _root.Q<Button>("nav-profile");
            _scanStartButton = _root.Q<Button>("nav-scan-start");
            _mapMemoAddButton = _root.Q<Button>("nav-map-memo-add");
            _memoFilterButton = _root.Q<Button>("memo-filter-button");
            _profileSettingsButton = _root.Q<Button>("profile-settings-button");
            _profileAccountSettingsBackButton = _root.Q<Button>("profile-account-settings-back-button");
            _profileLogoutButton = _root.Q<Button>("profile-logout-button");
            _profileFriendListButton = _root.Q<Button>("profile-friend-list-button");
            _profileFriendAddButton = _root.Q<Button>("profile-friend-add-button");
            _profilePushToggle = _root.Q<Button>("profile-push-toggle");
            _profileSoundToggle = _root.Q<Button>("profile-sound-toggle");

            _tabViewport = _root.Q<VisualElement>("tab-viewport");
            _tabStrip = _root.Q<VisualElement>("tab-strip");
            _bottomNavWrapper = _root.Q<VisualElement>("bottom-nav-wrapper");
            _bottomNav = _root.Q<VisualElement>("bottom-nav");
            _memoFilterBottomBar = _root.Q<VisualElement>("memo-filter-bottom-bar");
            _memoDetailBottomBar = _root.Q<VisualElement>("memo-detail-bottom-bar");
            _memoCreateBottomBar = _root.Q<VisualElement>("memo-create-bottom-bar");
            _memoTrashBottomBar = _root.Q<VisualElement>("memo-trash-bottom-bar");
            _homeTab = _root.Q<VisualElement>("tab-home");
            _menuTab = _root.Q<VisualElement>("tab-menu");
            _scanTab = _root.Q<VisualElement>("tab-scan");
            _mapTab = _root.Q<VisualElement>("tab-map");
            _profileTab = _root.Q<VisualElement>("tab-profile");
            _profileMainContent = _root.Q<VisualElement>("profile-main-content");
            _profileAccountSettingsPage = _root.Q<VisualElement>("profile-account-settings-page");
            _profileFriendListCard = _root.Q<VisualElement>("profile-friend-list-card");
            _profileFriendList = _root.Q<VisualElement>("profile-friend-list");
            _profileFriendItemsList = _root.Q<VisualElement>("profile-friend-items-list");
            _profileFriendListChevron = _root.Q<VisualElement>("profile-friend-list-chevron");
            _profileNameLabel = _root.Q<Label>("profile-name-label");
            _profileCompanyLabel = _root.Q<Label>("profile-company-label");

            ApplyProfileSummary();
            _memoFilterButton.clicked += ShowMemoFilterPage;
            _profileSettingsButton.clicked += ShowProfileAccountSettings;
            _profileAccountSettingsBackButton.clicked += HideProfileAccountSettings;
            _profileLogoutButton.clicked += ShowProfileLogoutConfirmPopup;
            _profileFriendListButton.clicked += ToggleProfileFriendList;
            _profileFriendAddButton.RegisterCallback<ClickEvent>(OnProfileFriendAddClicked);
            _profilePushToggle.clicked += ToggleProfilePush;
            _profileSoundToggle.clicked += ToggleProfileSound;
            InitializeMemoFilterDates();
            RegisterMemoDetailPage();
            RegisterMapMemoCreatePage();
            RegisterMemoFilterPage();
            RegisterMemoSearchPage();
            RegisterMapPage();
            LoadInitialData();
            HideProfileAccountSettings();
            RebuildProfileFriendList();
            ApplyProfileFriendList();
            _ = InitializeFriendsAsync();
            ApplyProfileSwitches();
        }

        private void OnDisable()
        {
            _memoFilterButton.clicked -= ShowMemoFilterPage;
            _profileSettingsButton.clicked -= ShowProfileAccountSettings;
            _profileAccountSettingsBackButton.clicked -= HideProfileAccountSettings;
            _profileLogoutButton.clicked -= ShowProfileLogoutConfirmPopup;
            _profileFriendListButton.clicked -= ToggleProfileFriendList;
            _profileFriendAddButton.UnregisterCallback<ClickEvent>(OnProfileFriendAddClicked);
            _profilePushToggle.clicked -= ToggleProfilePush;
            _profileSoundToggle.clicked -= ToggleProfileSound;
            UnregisterFriendsCallbacks();
            UnregisterMemoDetailPage();
            UnregisterMapMemoCreatePage();
            UnregisterMemoFilterPage();
            UnregisterMemoSearchPage();
            UnregisterMapPage();
            UnregisterScanActionDialog();
            ReleaseHomeMapThumbnails();
        }

        public void SetScanNavMode(bool enabled)
        {
            _bottomNavWrapper.EnableInClassList("is-scan-mode", enabled);
            _bottomNav.EnableInClassList("is-scan-mode", enabled);
            _scanStartButton.pickingMode = enabled ? PickingMode.Position : PickingMode.Ignore;
        }

        public void SetMapNavMode(bool enabled)
        {
            _bottomNavWrapper.EnableInClassList("is-map-mode", enabled);
            _bottomNav.EnableInClassList("is-map-mode", enabled);
            _mapMemoAddButton.pickingMode = enabled ? PickingMode.Position : PickingMode.Ignore;
            if (!enabled)
            {
                SetVisible(_mapCreateMemoActions, false);
            }
        }

        private void SetMapMemoAddAvailable(bool available)
        {
            _mapMemoAddButton.SetEnabled(available);
            _mapMemoAddButton.EnableInClassList("is-disabled", !available);
        }

        private static void SetVisible(VisualElement element, bool visible)
        {
            element.EnableInClassList(HIDDEN_CLASS, !visible);
        }

        private static bool IsVisible(VisualElement element)
        {
            return !element.ClassListContains(HIDDEN_CLASS);
        }

        public void BlurFocusedElement()
        {
            PopupPresentation.BlurFocusedElement(_root);
        }

        private void SetMemoDetailNavMode(bool enabled, bool showActions = true)
        {
            _bottomNavWrapper.EnableInClassList("is-memo-detail-mode", enabled);
            SetVisible(_memoDetailBottomBar, enabled && showActions);
        }

        private void SetMemoFilterNavMode(bool enabled)
        {
            _bottomNavWrapper.EnableInClassList("is-memo-filter-mode", enabled);
            SetVisible(_memoFilterBottomBar, enabled);
        }

        private void SetMemoCreateNavMode(bool enabled)
        {
            _bottomNavWrapper.EnableInClassList("is-memo-create-mode", enabled);
            SetVisible(_memoCreateBottomBar, enabled);
        }

        private void SetMemoTrashNavMode(bool enabled, bool showActions)
        {
            _bottomNavWrapper.EnableInClassList("is-memo-trash-mode", enabled);
            SetVisible(_memoTrashBottomBar, enabled && showActions);
        }

        private void RequestTabSwitch(int tabIndex)
        {
            TabSwitchRequested?.Invoke(tabIndex);
        }

        public void ShowMemoCollectionTab()
        {
            RequestTabSwitch(1);
        }

        public void SetScanStartAvailable(bool available)
        {
            _scanStartButton.EnableInClassList("is-disabled", !available);
        }

        public void SetScanSceneActive(bool active)
        {
            _root.EnableInClassList(HIDDEN_CLASS, active);
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
