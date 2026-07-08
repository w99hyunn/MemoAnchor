using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace MemoAnchor.UI
{
    public partial class MainTabView
    {
        private const int MEMO_FILTER_SORT_LATEST = 0;
        private const int MEMO_FILTER_SORT_UNHANDLED = 1;
        private const int MEMO_FILTER_SORT_ADDRESS = 2;
        private const int MEMO_FILTER_SORT_DEADLINE = 3;

        private Button _memoFilterBackButton, _memoFilterStartDateButton, _memoFilterEndDateButton, _memoFilterDateToggle;
        private Button _memoFilterSortLatestButton, _memoFilterSortUnhandledButton, _memoFilterSortAddressButton, _memoFilterSortDeadlineButton;
        private Button _memoFilterMapButton, _memoFilterMap1Button, _memoFilterMap2Button, _memoFilterMap3Button;
        private Button _memoFilterUrgencyHighButton, _memoFilterUrgencyMediumButton, _memoFilterUrgencyLowButton, _memoFilterUrgencyToggle;
        private Button _memoFilterCalendarPrevButton, _memoFilterCalendarNextButton, _memoFilterCalendarCloseButton, _memoFilterResetButton, _memoFilterApplyButton;

        private VisualElement _memoFilterPage, _memoFilterCalendar, _memoFilterCalendarGrid, _memoFilterMapList;
        private Label _memoFilterStartDateLabel, _memoFilterEndDateLabel, _memoFilterMapLabel, _memoFilterCalendarTitle;
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

            PrepareMemoFilterRuntimeVisibility();
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

        private void PrepareMemoFilterRuntimeVisibility()
        {
            SetVisible(_memoFilterPage, false);
            SetVisible(_memoFilterBottomBar, false);
            SetVisible(_memoFilterCalendar, false);
            SetVisible(_memoFilterMapList, false);
        }

        private static void SetVisible(VisualElement element, bool visible)
        {
            element.EnableInClassList(HIDDEN_CLASS, !visible);
        }

        private void UnregisterMemoFilterPage()
        {
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
            return mapLabel.text.Length > 0 ? mapLabel.text : $"맵 {index + 1}";
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
            HideMemoDetailPage();
            SetVisible(_memoFilterPage, true);
            SetMemoFilterNavMode(true);
        }

        private void HideMemoFilterPage()
        {
            SetVisible(_memoFilterPage, false);
            SetMemoFilterNavMode(false);
            HideMemoFilterCalendar();
            SetVisible(_memoFilterMapList, false);
        }

        public void HideMemoOverlayPages()
        {
            HideMemoFilterPage();
            HideMemoSearchPage();
            HideMemoCreatePage();
            HideMemoDetailPage();
        }

        private void ShowMemoFilterStartCalendar()
        {
            _memoFilterEditingStartDate = true;
            _memoFilterDateEnabled = true;
            _memoFilterCalendarMonth = new DateTime(_memoFilterStartDate.Year, _memoFilterStartDate.Month, 1);
            SetVisible(_memoFilterCalendar, true);
            RebuildMemoFilterCalendar();
            RefreshMemoFilterVisualState();
        }

        private void ShowMemoFilterEndCalendar()
        {
            _memoFilterEditingStartDate = false;
            _memoFilterDateEnabled = true;
            _memoFilterCalendarMonth = new DateTime(_memoFilterEndDate.Year, _memoFilterEndDate.Month, 1);
            SetVisible(_memoFilterCalendar, true);
            RebuildMemoFilterCalendar();
            RefreshMemoFilterVisualState();
        }

        private void HideMemoFilterCalendar()
        {
            SetVisible(_memoFilterCalendar, false);
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
            SetVisible(_memoFilterMapList, _memoFilterMapList.ClassListContains(HIDDEN_CLASS));
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
            SetVisible(_memoFilterMapList, false);
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
            SetVisible(_memoFilterMapList, false);
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
            DateTime selectedDate = _memoFilterEditingStartDate ? _memoFilterStartDate : _memoFilterEndDate;
            MemoCalendarUtility.Rebuild(_memoFilterCalendarGrid, _memoFilterCalendarTitle, _memoFilterCalendarMonth, selectedDate, SelectMemoFilterDate);
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
