using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace MemoAnchor.UI
{
    [RequireComponent(typeof(UIDocument))]
    public class MainTabView : MonoBehaviour
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
        private const int MEMO_FILTER_SORT_LATEST = 0;
        private const int MEMO_FILTER_SORT_UNHANDLED = 1;
        private const int MEMO_FILTER_SORT_ADDRESS = 2;
        private const int MEMO_FILTER_SORT_DEADLINE = 3;

        [SerializeField] private VisualTreeAsset _scanActionDialogAsset;
        [SerializeField] private VisualTreeAsset _alertDialogAsset;
        [SerializeField] private VisualTreeAsset _alertRequestItemAsset;
        [SerializeField] private VisualTreeAsset _alertMapItemAsset;

        private Button _homeButton, _menuButton, _scanButton, _mapButton, _profileButton, _scanStartButton, _alertButton, _homeModeToggle, _memoModeToggle;
        private Button _scanActionCreateButton, _scanActionJoinButton;
        private Button _alertBackButton;
        private Button _memoFilterButton, _memoFilterBackButton, _memoFilterStartDateButton, _memoFilterEndDateButton, _memoFilterDateToggle;
        private Button _memoFilterSortLatestButton, _memoFilterSortUnhandledButton, _memoFilterSortAddressButton, _memoFilterSortDeadlineButton;
        private Button _memoFilterMapButton, _memoFilterMap1Button, _memoFilterMap2Button, _memoFilterMap3Button;
        private Button _memoFilterUrgencyHighButton, _memoFilterUrgencyMediumButton, _memoFilterUrgencyLowButton, _memoFilterUrgencyToggle;
        private Button _memoFilterCalendarPrevButton, _memoFilterCalendarNextButton, _memoFilterCalendarCloseButton, _memoFilterResetButton, _memoFilterApplyButton;

        private VisualElement _root, _tabViewport, _tabStrip, _bottomNavWrapper, _bottomNav;
        private VisualElement _homeTab, _menuTab, _scanTab, _mapTab, _profileTab;
        private VisualElement _homeModeBack;
        private VisualElement _memoFilterPage, _memoFilterCalendar, _memoFilterCalendarGrid, _memoFilterMapList;
        private VisualElement _scanActionDialogOverlay;
        private VisualElement _alertDialogPage, _alertRequestList, _alertMapList;
        private Label _homeModeTitle, _memoFilterStartDateLabel, _memoFilterEndDateLabel, _memoFilterMapLabel, _memoFilterCalendarTitle;
        private TemplateContainer _scanActionDialogTree;
        private TemplateContainer _alertDialogTree;
        private Action _onScanActionCreate, _onScanActionJoin;
        private readonly List<MemoFilterRow> _memoFilterRows = new();
        private readonly List<string> _memoFilterMapOptions = new();
        private DateTime _memoFilterStartDate;
        private DateTime _memoFilterEndDate;
        private bool _memoFilterDateEnabled;
        private bool _memoFilterUrgencyEnabled;
        private bool _memoFilterEditingStartDate = true;
        private int _memoFilterSortMode = MEMO_FILTER_SORT_LATEST;
        private int _memoFilterUrgency = 1;
        private string _memoFilterMap;
        private DateTime _memoFilterCalendarMonth;
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
            _memoFilterBackButton.clicked -= HideMemoFilterPage;
            _memoFilterStartDateButton.clicked -= ShowMemoFilterStartCalendar;
            _memoFilterEndDateButton.clicked -= ShowMemoFilterEndCalendar;
            _memoFilterDateToggle.clicked -= ToggleMemoFilterDate;
            _memoFilterSortLatestButton.clicked -= SelectMemoFilterSortLatest;
            _memoFilterSortUnhandledButton.clicked -= SelectMemoFilterSortUnhandled;
            _memoFilterSortAddressButton.clicked -= SelectMemoFilterSortAddress;
            _memoFilterSortDeadlineButton.clicked -= SelectMemoFilterSortDeadline;
            _memoFilterMapButton.clicked -= ToggleMemoFilterMapList;
            _memoFilterMap1Button.clicked -= SelectMemoFilterMap1;
            _memoFilterMap2Button.clicked -= SelectMemoFilterMap2;
            _memoFilterMap3Button.clicked -= SelectMemoFilterMap3;
            _memoFilterUrgencyHighButton.clicked -= SelectMemoFilterUrgencyHigh;
            _memoFilterUrgencyMediumButton.clicked -= SelectMemoFilterUrgencyMedium;
            _memoFilterUrgencyLowButton.clicked -= SelectMemoFilterUrgencyLow;
            _memoFilterUrgencyToggle.clicked -= ToggleMemoFilterUrgency;
            _memoFilterCalendarPrevButton.clicked -= ShowPreviousMemoFilterCalendarMonth;
            _memoFilterCalendarNextButton.clicked -= ShowNextMemoFilterCalendarMonth;
            _memoFilterCalendarCloseButton.clicked -= HideMemoFilterCalendar;
            _memoFilterResetButton.clicked -= ResetMemoFilter;
            _memoFilterApplyButton.clicked -= ApplyMemoFilterAndClose;
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

        private void InitializeMemoFilterDates()
        {
            DateTime today = DateTime.Today;
            _memoFilterStartDate = today;
            _memoFilterEndDate = today;
            _memoFilterCalendarMonth = new DateTime(today.Year, today.Month, 1);
        }

        private void RegisterMemoFilterPage()
        {
            _memoFilterPage = _root.Q<VisualElement>("memo-filter-page");
            _memoFilterCalendar = _root.Q<VisualElement>("memo-filter-calendar");
            _memoFilterCalendarGrid = _root.Q<VisualElement>("memo-filter-calendar-grid");
            _memoFilterMapList = _root.Q<VisualElement>("memo-filter-map-list");
            _memoFilterBackButton = _root.Q<Button>("memo-filter-back-button");
            _memoFilterStartDateButton = _root.Q<Button>("memo-filter-start-date-button");
            _memoFilterEndDateButton = _root.Q<Button>("memo-filter-end-date-button");
            _memoFilterDateToggle = _root.Q<Button>("memo-filter-date-toggle");
            _memoFilterSortLatestButton = _root.Q<Button>("memo-filter-sort-latest");
            _memoFilterSortUnhandledButton = _root.Q<Button>("memo-filter-sort-deadline");
            _memoFilterSortAddressButton = _root.Q<Button>("memo-filter-sort-address");
            _memoFilterSortDeadlineButton = _root.Q<Button>("memo-filter-sort-deadline-date");
            _memoFilterMapButton = _root.Q<Button>("memo-filter-map-button");
            _memoFilterMap1Button = _root.Q<Button>("memo-filter-map-1");
            _memoFilterMap2Button = _root.Q<Button>("memo-filter-map-2");
            _memoFilterMap3Button = _root.Q<Button>("memo-filter-map-3");
            _memoFilterUrgencyHighButton = _root.Q<Button>("memo-filter-urgency-high");
            _memoFilterUrgencyMediumButton = _root.Q<Button>("memo-filter-urgency-medium");
            _memoFilterUrgencyLowButton = _root.Q<Button>("memo-filter-urgency-low");
            _memoFilterUrgencyToggle = _root.Q<Button>("memo-filter-urgency-toggle");
            _memoFilterCalendarPrevButton = _root.Q<Button>("memo-filter-calendar-prev");
            _memoFilterCalendarNextButton = _root.Q<Button>("memo-filter-calendar-next");
            _memoFilterCalendarCloseButton = _root.Q<Button>("memo-filter-calendar-close");
            _memoFilterResetButton = _root.Q<Button>("memo-filter-reset-button");
            _memoFilterApplyButton = _root.Q<Button>("memo-filter-apply-button");
            _memoFilterStartDateLabel = _root.Q<Label>("memo-filter-start-date-label");
            _memoFilterEndDateLabel = _root.Q<Label>("memo-filter-end-date-label");
            _memoFilterMapLabel = _root.Q<Label>("memo-filter-map-label");
            _memoFilterCalendarTitle = _root.Q<Label>("memo-filter-calendar-title");

            _memoFilterBackButton.clicked += HideMemoFilterPage;
            _memoFilterStartDateButton.clicked += ShowMemoFilterStartCalendar;
            _memoFilterEndDateButton.clicked += ShowMemoFilterEndCalendar;
            _memoFilterDateToggle.clicked += ToggleMemoFilterDate;
            _memoFilterSortLatestButton.clicked += SelectMemoFilterSortLatest;
            _memoFilterSortUnhandledButton.clicked += SelectMemoFilterSortUnhandled;
            _memoFilterSortAddressButton.clicked += SelectMemoFilterSortAddress;
            _memoFilterSortDeadlineButton.clicked += SelectMemoFilterSortDeadline;
            _memoFilterMapButton.clicked += ToggleMemoFilterMapList;
            _memoFilterMap1Button.clicked += SelectMemoFilterMap1;
            _memoFilterMap2Button.clicked += SelectMemoFilterMap2;
            _memoFilterMap3Button.clicked += SelectMemoFilterMap3;
            _memoFilterUrgencyHighButton.clicked += SelectMemoFilterUrgencyHigh;
            _memoFilterUrgencyMediumButton.clicked += SelectMemoFilterUrgencyMedium;
            _memoFilterUrgencyLowButton.clicked += SelectMemoFilterUrgencyLow;
            _memoFilterUrgencyToggle.clicked += ToggleMemoFilterUrgency;
            _memoFilterCalendarPrevButton.clicked += ShowPreviousMemoFilterCalendarMonth;
            _memoFilterCalendarNextButton.clicked += ShowNextMemoFilterCalendarMonth;
            _memoFilterCalendarCloseButton.clicked += HideMemoFilterCalendar;
            _memoFilterResetButton.clicked += ResetMemoFilter;
            _memoFilterApplyButton.clicked += ApplyMemoFilterAndClose;

            CacheMemoFilterRows();
            RebuildMemoFilterCalendar();
            RefreshMemoFilterVisualState();
        }

        private void CacheMemoFilterRows()
        {
            _memoFilterRows.Clear();
            _memoFilterMapOptions.Clear();
            int index = 0;
            _root.Query<VisualElement>(className: "memo-list-swipe-row").ForEach(row =>
            {
                string map = GetMemoFilterRowMap(row, index);
                if (!_memoFilterMapOptions.Contains(map))
                {
                    _memoFilterMapOptions.Add(map);
                }

                int urgency = index % 3;
                bool handled = index % 2 == 0;
                DateTime deadline = DateTime.Today.AddDays(index);
                _memoFilterRows.Add(new MemoFilterRow(row, index, map, urgency, handled, deadline));
                index++;
            });

            RefreshMemoFilterMapButtons();
        }

        private static string GetMemoFilterRowMap(VisualElement row, int index)
        {
            Label mapLabel = row.Q<Label>(className: "memo-list-item-meta");
            return mapLabel != null && mapLabel.text.Length > 0 ? mapLabel.text : $"맵 {index + 1}";
        }

        private void RefreshMemoFilterMapButtons()
        {
            RefreshMemoFilterMapButton(_memoFilterMap1Button, 0);
            RefreshMemoFilterMapButton(_memoFilterMap2Button, 1);
            RefreshMemoFilterMapButton(_memoFilterMap3Button, 2);
        }

        private void RefreshMemoFilterMapButton(Button button, int optionIndex)
        {
            bool hasOption = optionIndex < _memoFilterMapOptions.Count;
            button.style.display = hasOption ? DisplayStyle.Flex : DisplayStyle.None;
            if (hasOption)
            {
                button.text = _memoFilterMapOptions[optionIndex];
            }
        }

        private void ShowMemoFilterPage()
        {
            _memoFilterPage.AddToClassList(OPEN_CLASS);
        }

        private void HideMemoFilterPage()
        {
            _memoFilterPage.RemoveFromClassList(OPEN_CLASS);
            HideMemoFilterCalendar();
            _memoFilterMapList.RemoveFromClassList(OPEN_CLASS);
        }

        private void ShowMemoFilterStartCalendar()
        {
            _memoFilterEditingStartDate = true;
            _memoFilterDateEnabled = true;
            _memoFilterCalendarMonth = new DateTime(_memoFilterStartDate.Year, _memoFilterStartDate.Month, 1);
            _memoFilterCalendar.AddToClassList(OPEN_CLASS);
            RebuildMemoFilterCalendar();
            RefreshMemoFilterVisualState();
        }

        private void ShowMemoFilterEndCalendar()
        {
            _memoFilterEditingStartDate = false;
            _memoFilterDateEnabled = true;
            _memoFilterCalendarMonth = new DateTime(_memoFilterEndDate.Year, _memoFilterEndDate.Month, 1);
            _memoFilterCalendar.AddToClassList(OPEN_CLASS);
            RebuildMemoFilterCalendar();
            RefreshMemoFilterVisualState();
        }

        private void HideMemoFilterCalendar()
        {
            _memoFilterCalendar.RemoveFromClassList(OPEN_CLASS);
        }

        private void ToggleMemoFilterDate()
        {
            _memoFilterDateEnabled = !_memoFilterDateEnabled;
            RefreshMemoFilterVisualState();
        }

        private void ShowPreviousMemoFilterCalendarMonth()
        {
            _memoFilterCalendarMonth = _memoFilterCalendarMonth.AddMonths(-1);
            RebuildMemoFilterCalendar();
            RefreshMemoFilterVisualState();
        }

        private void ShowNextMemoFilterCalendarMonth()
        {
            _memoFilterCalendarMonth = _memoFilterCalendarMonth.AddMonths(1);
            RebuildMemoFilterCalendar();
            RefreshMemoFilterVisualState();
        }

        private void SelectMemoFilterSortLatest()
        {
            SelectMemoFilterSort(MEMO_FILTER_SORT_LATEST);
        }

        private void SelectMemoFilterSortUnhandled()
        {
            SelectMemoFilterSort(MEMO_FILTER_SORT_UNHANDLED);
        }

        private void SelectMemoFilterSortAddress()
        {
            SelectMemoFilterSort(MEMO_FILTER_SORT_ADDRESS);
        }

        private void SelectMemoFilterSortDeadline()
        {
            SelectMemoFilterSort(MEMO_FILTER_SORT_DEADLINE);
        }

        private void SelectMemoFilterSort(int sortMode)
        {
            _memoFilterSortMode = sortMode;
            RefreshMemoFilterVisualState();
        }

        private void ToggleMemoFilterMapList()
        {
            _memoFilterMapList.EnableInClassList(OPEN_CLASS, !_memoFilterMapList.ClassListContains(OPEN_CLASS));
        }

        private void SelectMemoFilterMap1()
        {
            SelectMemoFilterMapAt(0);
        }

        private void SelectMemoFilterMap2()
        {
            SelectMemoFilterMapAt(1);
        }

        private void SelectMemoFilterMap3()
        {
            SelectMemoFilterMapAt(2);
        }

        private void SelectMemoFilterMapAt(int optionIndex)
        {
            if (optionIndex < _memoFilterMapOptions.Count)
            {
                SelectMemoFilterMap(_memoFilterMapOptions[optionIndex]);
            }
        }

        private void SelectMemoFilterMap(string map)
        {
            _memoFilterMap = map;
            _memoFilterMapList.RemoveFromClassList(OPEN_CLASS);
            RefreshMemoFilterVisualState();
        }

        private void SelectMemoFilterUrgencyHigh()
        {
            SelectMemoFilterUrgency(0);
        }

        private void SelectMemoFilterUrgencyMedium()
        {
            SelectMemoFilterUrgency(1);
        }

        private void SelectMemoFilterUrgencyLow()
        {
            SelectMemoFilterUrgency(2);
        }

        private void SelectMemoFilterUrgency(int urgency)
        {
            _memoFilterUrgency = urgency;
            _memoFilterUrgencyEnabled = true;
            RefreshMemoFilterVisualState();
        }

        private void ToggleMemoFilterUrgency()
        {
            _memoFilterUrgencyEnabled = !_memoFilterUrgencyEnabled;
            RefreshMemoFilterVisualState();
        }

        private void ResetMemoFilter()
        {
            InitializeMemoFilterDates();
            _memoFilterDateEnabled = false;
            _memoFilterUrgencyEnabled = false;
            _memoFilterEditingStartDate = true;
            _memoFilterSortMode = MEMO_FILTER_SORT_LATEST;
            _memoFilterUrgency = 1;
            _memoFilterMap = null;
            HideMemoFilterCalendar();
            _memoFilterMapList.RemoveFromClassList(OPEN_CLASS);
            RebuildMemoFilterCalendar();
            RefreshMemoFilterVisualState();
            ApplyMemoFilter();
        }

        private void ApplyMemoFilterAndClose()
        {
            ApplyMemoFilter();
            HideMemoFilterPage();
        }

        private void ApplyMemoFilter()
        {
            _memoFilterRows.Sort(CompareMemoFilterRows);

            foreach (MemoFilterRow row in _memoFilterRows)
            {
                row.Element.parent.Add(row.Element);
                bool matchesDate = !_memoFilterDateEnabled || (row.Deadline.Date >= _memoFilterStartDate.Date && row.Deadline.Date <= _memoFilterEndDate.Date);
                bool matchesMap = string.IsNullOrEmpty(_memoFilterMap) || row.Map == _memoFilterMap;
                bool matchesUrgency = !_memoFilterUrgencyEnabled || row.Urgency == _memoFilterUrgency;
                row.Element.style.display = matchesDate && matchesMap && matchesUrgency ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private void RemoveMemoFilterRow(VisualElement element)
        {
            _memoFilterRows.RemoveAll(row => row.Element == element);
        }

        private int CompareMemoFilterRows(MemoFilterRow first, MemoFilterRow second)
        {
            return _memoFilterSortMode switch
            {
                MEMO_FILTER_SORT_UNHANDLED => first.Handled.CompareTo(second.Handled),
                MEMO_FILTER_SORT_ADDRESS => string.Compare(first.Map, second.Map, StringComparison.Ordinal),
                MEMO_FILTER_SORT_DEADLINE => first.Deadline.CompareTo(second.Deadline),
                _ => first.OriginalIndex.CompareTo(second.OriginalIndex)
            };
        }

        private void RebuildMemoFilterCalendar()
        {
            _memoFilterCalendarGrid.Clear();
            _memoFilterCalendarTitle.text = _memoFilterCalendarMonth.ToString("yyyy년 M월");

            string[] weekdays = { "일", "월", "화", "수", "목", "금", "토" };
            for (int i = 0; i < weekdays.Length; i++)
            {
                Button weekday = new() { text = weekdays[i] };
                weekday.AddToClassList("memo-filter-calendar-cell");
                weekday.AddToClassList("is-weekday-header");
                if (i == 0)
                {
                    weekday.AddToClassList("is-sunday");
                }
                else if (i == 6)
                {
                    weekday.AddToClassList("is-saturday");
                }

                _memoFilterCalendarGrid.Add(weekday);
            }

            int leadingBlankCount = (int)_memoFilterCalendarMonth.DayOfWeek;
            DateTime firstVisibleDate = _memoFilterCalendarMonth.AddDays(-leadingBlankCount);
            const int CALENDAR_VISIBLE_DAY_COUNT = 42;
            for (int i = 0; i < CALENDAR_VISIBLE_DAY_COUNT; i++)
            {
                DateTime date = firstVisibleDate.AddDays(i);
                int dayOfWeek = (int)date.DayOfWeek;
                bool isMuted = date.Month != _memoFilterCalendarMonth.Month;
                Button dayButton = new() { text = date.Day.ToString() };
                dayButton.AddToClassList("memo-filter-calendar-cell");
                if (isMuted)
                {
                    dayButton.AddToClassList("is-muted");
                }

                if (dayOfWeek == 0)
                {
                    dayButton.AddToClassList("is-sunday");
                }
                else if (dayOfWeek == 6)
                {
                    dayButton.AddToClassList("is-saturday");
                }

                DateTime capturedDate = date;
                dayButton.clicked += () => SelectMemoFilterDate(capturedDate);

                _memoFilterCalendarGrid.Add(dayButton);
            }
        }

        private void SelectMemoFilterDate(DateTime date)
        {
            if (_memoFilterEditingStartDate)
            {
                _memoFilterStartDate = date;
            }
            else
            {
                _memoFilterEndDate = date;
            }

            _memoFilterCalendarMonth = new DateTime(date.Year, date.Month, 1);
            RebuildMemoFilterCalendar();
            RefreshMemoFilterVisualState();
        }

        private void RefreshMemoFilterVisualState()
        {
            _memoFilterStartDateLabel.text = _memoFilterStartDate.ToString("yyyy-MM-dd");
            _memoFilterEndDateLabel.text = _memoFilterEndDate.ToString("yyyy-MM-dd");
            _memoFilterMapLabel.text = string.IsNullOrEmpty(_memoFilterMap) ? "건물 선택하기" : _memoFilterMap;
            _memoFilterCalendarTitle.text = _memoFilterCalendarMonth.ToString("yyyy년 M월");

            _memoFilterDateToggle.EnableInClassList(SELECTED_CLASS, _memoFilterDateEnabled);
            _memoFilterStartDateButton.EnableInClassList(SELECTED_CLASS, _memoFilterDateEnabled && _memoFilterEditingStartDate);
            _memoFilterEndDateButton.EnableInClassList(SELECTED_CLASS, _memoFilterDateEnabled && !_memoFilterEditingStartDate);
            _memoFilterStartDateButton.EnableInClassList(ERROR_CLASS, _memoFilterDateEnabled && _memoFilterStartDate > _memoFilterEndDate);
            _memoFilterCalendar.EnableInClassList(ERROR_CLASS, _memoFilterDateEnabled && _memoFilterStartDate > _memoFilterEndDate);

            _memoFilterSortLatestButton.EnableInClassList(SELECTED_CLASS, _memoFilterSortMode == MEMO_FILTER_SORT_LATEST);
            _memoFilterSortUnhandledButton.EnableInClassList(SELECTED_CLASS, _memoFilterSortMode == MEMO_FILTER_SORT_UNHANDLED);
            _memoFilterSortAddressButton.EnableInClassList(SELECTED_CLASS, _memoFilterSortMode == MEMO_FILTER_SORT_ADDRESS);
            _memoFilterSortDeadlineButton.EnableInClassList(SELECTED_CLASS, _memoFilterSortMode == MEMO_FILTER_SORT_DEADLINE);

            _memoFilterMap1Button.EnableInClassList(SELECTED_CLASS, _memoFilterMapOptions.Count > 0 && _memoFilterMap == _memoFilterMapOptions[0]);
            _memoFilterMap2Button.EnableInClassList(SELECTED_CLASS, _memoFilterMapOptions.Count > 1 && _memoFilterMap == _memoFilterMapOptions[1]);
            _memoFilterMap3Button.EnableInClassList(SELECTED_CLASS, _memoFilterMapOptions.Count > 2 && _memoFilterMap == _memoFilterMapOptions[2]);

            _memoFilterUrgencyToggle.EnableInClassList(SELECTED_CLASS, _memoFilterUrgencyEnabled);
            _memoFilterUrgencyHighButton.EnableInClassList(SELECTED_CLASS, _memoFilterUrgency == 0);
            _memoFilterUrgencyMediumButton.EnableInClassList(SELECTED_CLASS, _memoFilterUrgency == 1);
            _memoFilterUrgencyLowButton.EnableInClassList(SELECTED_CLASS, _memoFilterUrgency == 2);

            RefreshMemoFilterCalendarSelection();
        }

        private void RefreshMemoFilterCalendarSelection()
        {
            DateTime selectedDate = _memoFilterEditingStartDate ? _memoFilterStartDate : _memoFilterEndDate;
            _memoFilterCalendarGrid.Query<Button>(className: "memo-filter-calendar-cell").ForEach(button =>
            {
                bool isSelected = false;
                if (int.TryParse(button.text, out int day))
                {
                    isSelected = selectedDate.Year == _memoFilterCalendarMonth.Year
                                 && selectedDate.Month == _memoFilterCalendarMonth.Month
                                 && selectedDate.Day == day
                                 && !button.ClassListContains("is-muted");
                }

                button.EnableInClassList(SELECTED_CLASS, isSelected);
            });
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

        private readonly struct MemoFilterRow
        {
            public MemoFilterRow(VisualElement element, int originalIndex, string map, int urgency, bool handled, DateTime deadline)
            {
                Element = element;
                OriginalIndex = originalIndex;
                Map = map;
                Urgency = urgency;
                Handled = handled;
                Deadline = deadline;
            }

            public VisualElement Element { get; }
            public int OriginalIndex { get; }
            public string Map { get; }
            public int Urgency { get; }
            public bool Handled { get; }
            public DateTime Deadline { get; }
        }
    }
}
