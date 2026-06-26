using System;
using Unity.Services.Authentication;
using UnityEngine;
using UnityEngine.UIElements;

namespace MemoAnchor.UI
{
    [RequireComponent(typeof(UIDocument))]
    public partial class MainTabView : MonoBehaviour
    {
        private const string DIALOG_OPEN_CLASS = "is-open";
        private const string DIALOG_ANIM_READY_CLASS = "is-anim-ready";
        private const string MEMO_DELETE_OPEN_CLASS = "is-delete-open";
        private const string SELECTED_CLASS = "is-selected";
        private const string HIDDEN_CLASS = "is-hidden";
        private const string ERROR_CLASS = "is-error";
        private const string HOME_ADMIN_MODE_CLASS = "is-admin-mode";
        private const string HOME_WORK_MODE_CLASS = "is-work-mode";
        private const float MEMO_SWIPE_INTENT_THRESHOLD = 20f;
        private const float MEMO_SWIPE_OPEN_THRESHOLD = 80f;

        [SerializeField] private VisualTreeAsset _scanActionDialogAsset;
        [SerializeField] private VisualTreeAsset _alertRequestItemAsset;
        [SerializeField] private VisualTreeAsset _alertMapItemAsset;
        [SerializeField] private string _splashScene = "Splash";
        [SerializeField] private string _homeAdminTitle = "관리자";
        [SerializeField] private string _homeWorkTitle = "내 업무";

        private Button _homeButton, _menuButton, _scanButton, _mapButton, _profileButton, _scanStartButton, _alertButton, _homeModeToggle, _memoModeToggle;
        private Button _scanActionCreateButton, _scanActionJoinButton;
        private Button _alertBackButton;
        private Button _memoFilterButton;
        private Button _memoSearchBackButton;
        private Button _profileSettingsButton, _profileAccountSettingsBackButton;
        private Button _profileLogoutButton;
        private Button _profileFriendListButton, _profileFriendAddButton;
        private Button _profilePushToggle, _profileSoundToggle;

        private VisualElement _root, _tabViewport, _tabStrip, _bottomNavWrapper, _bottomNav, _memoFilterBottomBar;
        private VisualElement _homeTab, _menuTab, _scanTab, _mapTab, _profileTab;
        private VisualElement _homeModeBack;
        private VisualElement _scanActionDialogOverlay;
        private VisualElement _alertDialogPage, _alertRequestList, _alertMapList;
        private VisualElement _memoSearchPage, _memoSearchHistoryList;
        private VisualElement _profileMainContent, _profileAccountSettingsPage;
        private VisualElement _profileFriendListCard, _profileFriendList, _profileFriendItemsList, _profileFriendListChevron;
        private TextField _memoSearchSourceInput, _memoSearchPageInput;
        private Label _homeGreetingLabel, _homeModeTitle;
        private Label _profileNameLabel, _profileCompanyLabel;
        private TemplateContainer _scanActionDialogTree;
        private FadeTransition _fadeTransition;
        private Action _onScanActionCreate, _onScanActionJoin;
        private int _scanActionDialogTransitionToken;
        private bool _isHomeWorkMode = true;
        private bool _profilePushEnabled;
        private bool _profileSoundEnabled = true;
        private bool _profileFriendListExpanded;
        private bool _isLoggingOut;

        public Button HomeButton => _homeButton;
        public Button MenuButton => _menuButton;
        public Button ScanButton => _scanButton;
        public Button MapButton => _mapButton;
        public Button ProfileButton => _profileButton;
        public VisualElement TabViewport => _tabViewport;

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
            _alertButton = _root.Q<Button>("alert");
            _homeModeToggle = _root.Q<Button>("home-mode-toggle");
            _memoModeToggle = _root.Q<Button>("memo-mode-toggle");
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
            _homeTab = _root.Q<VisualElement>("tab-home");
            _menuTab = _root.Q<VisualElement>("tab-menu");
            _scanTab = _root.Q<VisualElement>("tab-scan");
            _mapTab = _root.Q<VisualElement>("tab-map");
            _profileTab = _root.Q<VisualElement>("tab-profile");
            _homeModeBack = _root.Q<VisualElement>("mode-back");
            _alertDialogPage = _root.Q<VisualElement>("alert-dialog-page");
            _alertRequestList = _root.Q<VisualElement>("alert-request-list");
            _alertMapList = _root.Q<VisualElement>("alert-map-list");
            _profileMainContent = _root.Q<VisualElement>("profile-main-content");
            _profileAccountSettingsPage = _root.Q<VisualElement>("profile-account-settings-page");
            _profileFriendListCard = _root.Q<VisualElement>("profile-friend-list-card");
            _profileFriendList = _root.Q<VisualElement>("profile-friend-list");
            _profileFriendItemsList = _root.Q<VisualElement>("profile-friend-items-list");
            _profileFriendListChevron = _root.Q<VisualElement>("profile-friend-list-chevron");
            _homeGreetingLabel = _root.Q<Label>("home-greeting-label");
            _homeModeTitle = _root.Q<Label>("home-mode-title");
            _profileNameLabel = _root.Q<Label>("profile-name-label");
            _profileCompanyLabel = _root.Q<Label>("profile-company-label");
            _alertBackButton = _root.Q<Button>("alert-back-button");

            ApplyPlayerProfile();
            _alertButton.clicked += ShowAlertDialog;
            _alertBackButton.clicked += HideAlertDialog;
            _homeModeToggle.clicked += ToggleHomeMode;
            _memoModeToggle.clicked += ToggleHomeMode;
            _memoFilterButton.clicked += ShowMemoFilterPage;
            _profileSettingsButton.clicked += ShowProfileAccountSettings;
            _profileAccountSettingsBackButton.clicked += HideProfileAccountSettings;
            _profileLogoutButton.clicked += ShowProfileLogoutConfirmPopup;
            _profileFriendListButton.clicked += ToggleProfileFriendList;
            _profileFriendAddButton.RegisterCallback<ClickEvent>(OnProfileFriendAddClicked);
            _profilePushToggle.clicked += ToggleProfilePush;
            _profileSoundToggle.clicked += ToggleProfileSound;
            InitializeMemoFilterDates();
            RegisterMemoFilterPage();
            RegisterMemoSearchPage();
            HideProfileAccountSettings();
            HideAlertDialog();
            RebuildProfileFriendList();
            ApplyProfileFriendList();
            _ = InitializeFriendsAsync();
            ApplyProfileSwitches();
            ApplyHomeMode();
            RegisterMemoSwipeRows();
        }

        private void OnDisable()
        {
            _alertButton.clicked -= ShowAlertDialog;
            _alertBackButton.clicked -= HideAlertDialog;
            _homeModeToggle.clicked -= ToggleHomeMode;
            _memoModeToggle.clicked -= ToggleHomeMode;
            _memoFilterButton.clicked -= ShowMemoFilterPage;
            _profileSettingsButton.clicked -= ShowProfileAccountSettings;
            _profileAccountSettingsBackButton.clicked -= HideProfileAccountSettings;
            _profileLogoutButton.clicked -= ShowProfileLogoutConfirmPopup;
            _profileFriendListButton.clicked -= ToggleProfileFriendList;
            _profileFriendAddButton.UnregisterCallback<ClickEvent>(OnProfileFriendAddClicked);
            _profilePushToggle.clicked -= ToggleProfilePush;
            _profileSoundToggle.clicked -= ToggleProfileSound;
            UnregisterFriendsCallbacks();
            UnregisterMemoFilterPage();
            UnregisterMemoSearchPage();
        }

        public void SetScanNavMode(bool enabled)
        {
            _bottomNavWrapper.EnableInClassList("is-scan-mode", enabled);
            _bottomNav.EnableInClassList("is-scan-mode", enabled);
            _scanStartButton.pickingMode = enabled ? PickingMode.Position : PickingMode.Ignore;
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

        private void ToggleHomeMode()
        {
            _isHomeWorkMode = !_isHomeWorkMode;
            ApplyHomeMode();
        }

        private void ApplyHomeMode()
        {
            _homeModeTitle.text = _isHomeWorkMode ? _homeWorkTitle : _homeAdminTitle;
            _homeModeToggle.EnableInClassList(HOME_WORK_MODE_CLASS, _isHomeWorkMode);
            _memoModeToggle.EnableInClassList(HOME_WORK_MODE_CLASS, _isHomeWorkMode);
            _homeModeBack.EnableInClassList(HOME_ADMIN_MODE_CLASS, !_isHomeWorkMode);
            _menuTab.EnableInClassList(HOME_ADMIN_MODE_CLASS, !_isHomeWorkMode);
            _memoFilterPage.EnableInClassList(HOME_ADMIN_MODE_CLASS, !_isHomeWorkMode);
        }

        private void ToggleProfilePush()
        {
            _profilePushEnabled = !_profilePushEnabled;
            ApplyProfileSwitches();
        }

        private void ShowProfileAccountSettings()
        {
            PopupManager.HideConfirm();
            SetVisible(_profileMainContent, false);
            SetVisible(_profileAccountSettingsPage, true);
        }

        public void HideProfileAccountSettings()
        {
            PopupManager.HideConfirm();
            SetVisible(_profileMainContent, true);
            SetVisible(_profileAccountSettingsPage, false);
        }

        private void ShowProfileLogoutConfirmPopup()
        {
            PopupManager.ShowConfirm("로그아웃", "정말 로그아웃할까요?", "취소", "로그아웃", ConfirmProfileLogout);
        }

        private void ConfirmProfileLogout()
        {
            _ = LogoutAsync();
        }

        private async Awaitable LogoutAsync()
        {
            if (_isLoggingOut)
            {
                return;
            }

            _isLoggingOut = true;
            PopupManager.SetConfirmButtonsEnabled(false);
            UnregisterFriendsCallbacks();
            AuthenticationService.Instance.SignOut(true);
            MemoAnchor.PlayerSession.Clear();
            await _fadeTransition.FadeOutAndLoadSceneAsync(_splashScene);
        }

        private void ToggleProfileFriendList()
        {
            _profileFriendListExpanded = !_profileFriendListExpanded;
            ApplyProfileFriendList();
        }

        private void ApplyProfileFriendList()
        {
            SetVisible(_profileFriendList, _profileFriendListExpanded);
            _profileFriendListCard.EnableInClassList(SELECTED_CLASS, _profileFriendListExpanded);
            _profileFriendListChevron.EnableInClassList(SELECTED_CLASS, _profileFriendListExpanded);
        }

        private void ToggleProfileSound()
        {
            _profileSoundEnabled = !_profileSoundEnabled;
            ApplyProfileSwitches();
        }

        private void ApplyProfileSwitches()
        {
            _profilePushToggle.EnableInClassList(SELECTED_CLASS, _profilePushEnabled);
            _profileSoundToggle.EnableInClassList(SELECTED_CLASS, _profileSoundEnabled);
        }

        private void ApplyPlayerProfile()
        {
            MemoAnchor.PlayerProfile profile = MemoAnchor.PlayerSession.Profile;
            if (!string.IsNullOrWhiteSpace(profile.Name))
            {
                _homeGreetingLabel.text = $"{profile.Name}님, 안녕하세요!";
                _profileNameLabel.text = profile.Name;
            }

            if (!string.IsNullOrWhiteSpace(profile.CompanyName))
            {
                _profileCompanyLabel.text = profile.CompanyName;
            }
        }

        public void ShowScanActionDialog(Action onCreate, Action onJoin)
        {
            EnsureScanActionDialog();
            _onScanActionCreate = onCreate;
            _onScanActionJoin = onJoin;

            _scanActionDialogTransitionToken++;

            if (_scanActionDialogTree.parent == null)
            {
                _scanActionDialogOverlay.RemoveFromClassList(DIALOG_OPEN_CLASS);
                _root.Add(_scanActionDialogTree);
            }

            int token = _scanActionDialogTransitionToken;
            _scanActionDialogOverlay.schedule.Execute(() =>
            {
                if (token != _scanActionDialogTransitionToken)
                {
                    return;
                }

                _scanActionDialogOverlay.AddToClassList(DIALOG_OPEN_CLASS);
            }).ExecuteLater(16);
        }

        public void HideScanActionDialog()
        {
            if (_scanActionDialogOverlay == null)
            {
                return;
            }

            _scanActionDialogTransitionToken++;
            int token = _scanActionDialogTransitionToken;

            _scanActionDialogOverlay.RemoveFromClassList(DIALOG_OPEN_CLASS);
            _scanActionDialogOverlay.schedule.Execute(() =>
            {
                if (token != _scanActionDialogTransitionToken)
                {
                    return;
                }

                _scanActionDialogTree.RemoveFromHierarchy();
            }).ExecuteLater(240);
        }

        private void ShowAlertDialog()
        {
            RebuildAlertItems();
            SetVisible(_alertDialogPage, true);
            _alertDialogPage.BringToFront();
        }

        private void HideAlertDialog()
        {
            SetVisible(_alertDialogPage, false);
        }

        private void EnsureScanActionDialog()
        {
            if (_scanActionDialogOverlay != null)
            {
                return;
            }

            _scanActionDialogTree = _scanActionDialogAsset.Instantiate();
            _scanActionDialogTree.style.position = Position.Absolute;
            _scanActionDialogTree.style.left = 0;
            _scanActionDialogTree.style.right = 0;
            _scanActionDialogTree.style.top = 0;
            _scanActionDialogTree.style.bottom = 0;

            _scanActionDialogOverlay = _scanActionDialogTree.Q<VisualElement>("scan-action-dialog-overlay");
            VisualElement dialogSheet = _scanActionDialogTree.Q<VisualElement>("scan-action-dialog-sheet");
            _scanActionCreateButton = _scanActionDialogTree.Q<Button>("scan-action-create-button");
            _scanActionJoinButton = _scanActionDialogTree.Q<Button>("scan-action-join-button");

            _scanActionDialogOverlay.AddToClassList(DIALOG_ANIM_READY_CLASS);

            _scanActionDialogOverlay.RegisterCallback<ClickEvent>(_ => HideScanActionDialog());
            dialogSheet.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
            _scanActionCreateButton.clicked += () =>
            {
                HideScanActionDialog();
                _onScanActionCreate?.Invoke();
            };
            _scanActionJoinButton.clicked += () =>
            {
                HideScanActionDialog();
                _onScanActionJoin?.Invoke();
            };

        }

        private void RebuildAlertItems()
        {
            _alertRequestList.Clear();
            _alertMapList.Clear();

            AddFriendRequestAlerts();

            AddMapAlert("전기실", "3일 뒤 마감알림", "10분 전", false);
            AddMapAlert("전기실", "3일 뒤 마감알림", "10분 전", true);
        }

        private void AddMapAlert(string title, string description, string time, bool showsActions)
        {
            TemplateContainer item = _alertMapItemAsset.Instantiate();
            item.Q<Label>("alert-primary-text").text = title;
            item.Q<Label>("alert-secondary-text").text = description;
            item.Q<Label>("alert-time-text").text = time;
            item.Q<Button>("alert-close-button").style.display = showsActions ? DisplayStyle.None : DisplayStyle.Flex;
            item.Q<VisualElement>("alert-action-row").style.display = showsActions ? DisplayStyle.Flex : DisplayStyle.None;
            _alertMapList.Add(item);
        }

        private void RegisterMemoSwipeRows()
        {
            _root.Query<VisualElement>(className: "memo-list-swipe-row").ForEach(row =>
            {
                Vector3 pointerDownPosition = Vector3.zero;
                bool isSwipeIntent = false;
                bool isGestureResolved = false;

                row.RegisterCallback<PointerDownEvent>(evt =>
                {
                    pointerDownPosition = evt.position;
                    isSwipeIntent = false;
                    isGestureResolved = false;
                });

                row.RegisterCallback<PointerMoveEvent>(evt =>
                {
                    Vector3 delta = evt.position - pointerDownPosition;
                    if (!isGestureResolved)
                    {
                        float absX = Mathf.Abs(delta.x);
                        float absY = Mathf.Abs(delta.y);
                        if (absX < MEMO_SWIPE_INTENT_THRESHOLD && absY < MEMO_SWIPE_INTENT_THRESHOLD)
                        {
                            return;
                        }

                        isSwipeIntent = absX > absY;
                        isGestureResolved = true;
                        if (isSwipeIntent)
                        {
                            row.CapturePointer(evt.pointerId);
                        }
                    }

                    if (!isSwipeIntent)
                    {
                        return;
                    }

                    evt.StopPropagation();
                });

                row.RegisterCallback<PointerUpEvent>(evt =>
                {
                    Vector3 delta = evt.position - pointerDownPosition;
                    if (!isSwipeIntent && Mathf.Abs(delta.x) <= Mathf.Abs(delta.y))
                    {
                        return;
                    }

                    evt.StopPropagation();
                    if (delta.x < -MEMO_SWIPE_OPEN_THRESHOLD)
                    {
                        row.AddToClassList(MEMO_DELETE_OPEN_CLASS);
                    }
                    else if (delta.x > MEMO_SWIPE_OPEN_THRESHOLD)
                    {
                        row.RemoveFromClassList(MEMO_DELETE_OPEN_CLASS);
                    }

                    if (row.HasPointerCapture(evt.pointerId))
                    {
                        row.ReleasePointer(evt.pointerId);
                    }
                });

                row.RegisterCallback<PointerCancelEvent>(evt =>
                {
                    if (row.HasPointerCapture(evt.pointerId))
                    {
                        row.ReleasePointer(evt.pointerId);
                    }
                });

                row.Q<Button>("memo-list-delete-button").clicked += () =>
                {
                    RemoveMemoFilterRow(row);
                    row.parent.RemoveFromHierarchy();
                };
            });
        }

    }
}
