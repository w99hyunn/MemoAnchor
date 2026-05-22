using System;
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
        private const string OPEN_CLASS = "is-open";
        private const string ERROR_CLASS = "is-error";
        private const string HOME_ADMIN_MODE_CLASS = "is-admin-mode";
        private const string HOME_WORK_MODE_CLASS = "is-work-mode";
        private const string HOME_ADMIN_TITLE = "관리자";
        private const string HOME_WORK_TITLE = "내 업무";
        private const float MEMO_SWIPE_INTENT_THRESHOLD = 20f;
        private const float MEMO_SWIPE_OPEN_THRESHOLD = 80f;

        [SerializeField] private VisualTreeAsset _scanActionDialogAsset;
        [SerializeField] private VisualTreeAsset _alertDialogAsset;
        [SerializeField] private VisualTreeAsset _alertRequestItemAsset;
        [SerializeField] private VisualTreeAsset _alertMapItemAsset;

        private Button _homeButton, _menuButton, _scanButton, _mapButton, _profileButton, _scanStartButton, _alertButton, _homeModeToggle, _memoModeToggle;
        private Button _scanActionCreateButton, _scanActionJoinButton;
        private Button _alertBackButton;
        private Button _memoFilterButton;

        private VisualElement _root, _tabViewport, _tabStrip, _bottomNavWrapper, _bottomNav;
        private VisualElement _homeTab, _menuTab, _scanTab, _mapTab, _profileTab;
        private VisualElement _homeModeBack;
        private VisualElement _scanActionDialogOverlay;
        private VisualElement _alertDialogPage, _alertRequestList, _alertMapList;
        private Label _homeModeTitle;
        private TemplateContainer _scanActionDialogTree;
        private TemplateContainer _alertDialogTree;
        private Action _onScanActionCreate, _onScanActionJoin;
        private int _scanActionDialogTransitionToken;
        private bool _isHomeWorkMode = true;

        public Button HomeButton => _homeButton;
        public Button MenuButton => _menuButton;
        public Button ScanButton => _scanButton;
        public Button MapButton => _mapButton;
        public Button ProfileButton => _profileButton;
        public VisualElement TabViewport => _tabViewport;

        private void Awake()
        {
            TryGetComponent<UIDocument>(out var uiDocument);
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

            _tabViewport = _root.Q<VisualElement>("tab-viewport");
            _tabStrip = _root.Q<VisualElement>("tab-strip");
            _bottomNavWrapper = _root.Q<VisualElement>("bottom-nav-wrapper");
            _bottomNav = _root.Q<VisualElement>("bottom-nav");
            _homeTab = _root.Q<VisualElement>("tab-home");
            _menuTab = _root.Q<VisualElement>("tab-menu");
            _scanTab = _root.Q<VisualElement>("tab-scan");
            _mapTab = _root.Q<VisualElement>("tab-map");
            _profileTab = _root.Q<VisualElement>("tab-profile");
            _homeModeBack = _root.Q<VisualElement>("mode-back");
            _homeModeTitle = _root.Q<Label>("home-mode-title");

            _alertButton.clicked += ShowAlertDialog;
            _homeModeToggle.clicked += ToggleHomeMode;
            _memoModeToggle.clicked += ToggleHomeMode;
            _memoFilterButton.clicked += ShowMemoFilterPage;
            InitializeMemoFilterDates();
            RegisterMemoFilterPage();
            ApplyHomeMode();
            RegisterMemoSwipeRows();
        }

        private void OnDisable()
        {
            _alertButton.clicked -= ShowAlertDialog;
            _homeModeToggle.clicked -= ToggleHomeMode;
            _memoModeToggle.clicked -= ToggleHomeMode;
            _memoFilterButton.clicked -= ShowMemoFilterPage;
            UnregisterMemoFilterPage();
            if (_alertBackButton != null)
            {
                _alertBackButton.clicked -= HideAlertDialog;
            }

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
            _homeModeTitle.text = _isHomeWorkMode ? HOME_WORK_TITLE : HOME_ADMIN_TITLE;
            _homeModeToggle.EnableInClassList(HOME_WORK_MODE_CLASS, _isHomeWorkMode);
            _memoModeToggle.EnableInClassList(HOME_WORK_MODE_CLASS, _isHomeWorkMode);
            _homeModeBack.EnableInClassList(HOME_ADMIN_MODE_CLASS, !_isHomeWorkMode);
            _menuTab.EnableInClassList(HOME_ADMIN_MODE_CLASS, !_isHomeWorkMode);
            _memoFilterPage.EnableInClassList(HOME_ADMIN_MODE_CLASS, !_isHomeWorkMode);
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
            EnsureAlertDialog();

            if (_alertDialogTree.parent == null)
            {
                _root.Add(_alertDialogTree);
            }
        }

        private void HideAlertDialog()
        {
            _alertDialogTree.RemoveFromHierarchy();
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

        private void EnsureAlertDialog()
        {
            if (_alertDialogPage != null)
            {
                return;
            }

            _alertDialogTree = _alertDialogAsset.Instantiate();
            _alertDialogTree.style.position = Position.Absolute;
            _alertDialogTree.style.left = 0;
            _alertDialogTree.style.right = 0;
            _alertDialogTree.style.top = 0;
            _alertDialogTree.style.bottom = 0;

            _alertDialogPage = _alertDialogTree.Q<VisualElement>("alert-dialog-page");
            _alertRequestList = _alertDialogTree.Q<VisualElement>("alert-request-list");
            _alertMapList = _alertDialogTree.Q<VisualElement>("alert-map-list");
            _alertBackButton = _alertDialogTree.Q<Button>("alert-back-button");
            _alertBackButton.clicked += HideAlertDialog;

            RebuildAlertItems();
        }

        private void RebuildAlertItems()
        {
            _alertRequestList.Clear();
            _alertMapList.Clear();

            AddRequestAlert("김서진 (sj1011)님께서 친구요청을 보내셨습니다.", string.Empty, "5분 전");
            AddRequestAlert("조우현 (wh9482)님께서 참여요청을 보내셨습니다.", "외우산로 159 - 1층 - 화장실", "10분 전");
            AddRequestAlert("조우현 (wh9482)님께서 참여요청을 보내셨습니다.", "외우산로 159 - 1층 - 화장실", "10분 전");

            AddMapAlert("전기실", "3일 뒤 마감알림", "10분 전", false);
            AddMapAlert("전기실", "3일 뒤 마감알림", "10분 전", true);
        }

        private void AddRequestAlert(string title, string description, string time)
        {
            TemplateContainer item = _alertRequestItemAsset.Instantiate();
            item.Q<Label>("alert-primary-text").text = title;
            Label secondaryText = item.Q<Label>("alert-secondary-text");
            secondaryText.text = description;
            secondaryText.style.display = description.Length == 0 ? DisplayStyle.None : DisplayStyle.Flex;
            item.Q<Label>("alert-time-text").text = time;
            _alertRequestList.Add(item);
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
