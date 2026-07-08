using System;
using Unity.Services.Friends;
using Unity.Services.Friends.Models;
using UnityEngine;
using UnityEngine.UIElements;

namespace MemoAnchor.UI
{
    public partial class MainTabView
    {
        private VisualElement _memoCreatePage, _memoCreateCalendar, _memoCreateCalendarGrid, _memoCreateTimePicker;
        private ScrollView _memoCreateScroll;
        private ScrollView _memoCreateHourColumn, _memoCreateMinuteColumn, _memoCreatePeriodColumn;
        private Button _memoCreateCalendarCloseButton, _memoCreateCalendarPrevButton, _memoCreateCalendarNextButton;
        private Button _memoCreateDateButton, _memoCreateTimeButton, _memoCreateTimeCloseButton;
        private Button _memoCreateUrgencyHighButton, _memoCreateUrgencyMiddleButton, _memoCreateUrgencyLowButton;
        private Label _memoCreateDateLabel, _memoCreateTimeLabel, _memoCreateCalendarTitle;
        private ScanMapItem _memoCreateTargetMap;
        private string _memoCreateUrgency = "middle";
        private string _memoCreateRepairerPlayerId = string.Empty;
        private bool _memoCreateRepairerExpanded;
        private DateTime _memoCreateSelectedDate = new(2026, 1, 1);
        private DateTime _memoCreateCalendarMonth = new(2026, 1, 1);
        private int _memoCreateSelectedHour = 9;
        private int _memoCreateSelectedMinute;
        private bool _memoCreateIsPm;
        private bool _isMemoCreateTimeScrollSyncing;

        private void RegisterMemoCreatePage()
        {
            _memoCreatePage = _root.Q<VisualElement>("memo-create-page");
            _memoCreateScroll = _root.Q<ScrollView>("memo-create-scroll");
            _memoCreateCalendar = _root.Q<VisualElement>(className: "memo-create-calendar");
            _memoCreateCalendarGrid = _root.Q<VisualElement>("memo-create-calendar-grid");
            _memoCreateTimePicker = _root.Q<VisualElement>("memo-create-time-picker");
            _memoCreateHourColumn = _root.Q<ScrollView>("memo-create-hour-column");
            _memoCreateMinuteColumn = _root.Q<ScrollView>("memo-create-minute-column");
            _memoCreatePeriodColumn = _root.Q<ScrollView>("memo-create-period-column");
            _memoCreateCalendarCloseButton = _root.Q<Button>("memo-create-calendar-close");
            _memoCreateCalendarPrevButton = _root.Q<Button>("memo-create-calendar-prev");
            _memoCreateCalendarNextButton = _root.Q<Button>("memo-create-calendar-next");
            _memoCreateDateButton = _root.Q<Button>("memo-create-date-button");
            _memoCreateTimeButton = _root.Q<Button>("memo-create-time-button");
            _memoCreateTimeCloseButton = _root.Q<Button>("memo-create-time-close");
            _memoCreateDateLabel = _root.Q<Label>("memo-create-date-label");
            _memoCreateTimeLabel = _root.Q<Label>("memo-create-time-label");
            _memoCreateCalendarTitle = _root.Q<Label>("memo-create-calendar-title");
            _memoCreateBackButton = _root.Q<Button>("memo-create-back-button");
            _memoCreateRepairerButton = _root.Q<Button>("memo-create-repairer-button");
            _memoCreateRepairerCard = _root.Q<VisualElement>("memo-create-repairer-card");
            _memoCreateRepairerList = _root.Q<VisualElement>("memo-create-repairer-list");
            _memoCreateRepairerItemsList = _root.Q<VisualElement>("memo-create-repairer-items-list");
            _memoCreateRepairerChevron = _root.Q<VisualElement>("memo-create-repairer-chevron");
            _memoCreateRepairerLabel = _root.Q<Label>("memo-create-repairer-label");
            _memoCreateResetButton = _root.Q<Button>("memo-create-reset-button");
            _memoCreateSubmitButton = _root.Q<Button>("memo-create-submit-button");
            _memoCreateTitleInput = _root.Q<TextField>("memo-create-title-input");
            _memoCreateBodyInput = _root.Q<TextField>("memo-create-body-input");
            _memoCreateUrgencyHighButton = _root.Q<Button>("memo-create-urgency-high");
            _memoCreateUrgencyMiddleButton = _root.Q<Button>("memo-create-urgency-middle");
            _memoCreateUrgencyLowButton = _root.Q<Button>("memo-create-urgency-low");

            BuildMemoCreateCalendar();
            BuildMemoCreateTimePicker();
            HideMemoCreatePage();

            _memoCreateBackButton.clicked += OnClickMemoCreateBack;
            _memoCreateRepairerButton.clicked += ToggleMemoCreateRepairerList;
            _memoCreateDateButton.clicked += ShowMemoCreateCalendar;
            _memoCreateTimeButton.clicked += ShowMemoCreateTimePicker;
            _memoCreateCalendarCloseButton.clicked += HideMemoCreateCalendar;
            _memoCreateCalendarPrevButton.clicked += ShowPreviousMemoCreateCalendarMonth;
            _memoCreateCalendarNextButton.clicked += ShowNextMemoCreateCalendarMonth;
            _memoCreateTimeCloseButton.clicked += HideMemoCreateTimePicker;
            _memoCreateHourColumn.RegisterCallback<WheelEvent>(OnMemoCreateHourColumnWheel);
            _memoCreateMinuteColumn.RegisterCallback<WheelEvent>(OnMemoCreateMinuteColumnWheel);
            _memoCreatePeriodColumn.RegisterCallback<WheelEvent>(OnMemoCreatePeriodColumnWheel);
            _memoCreateHourColumn.RegisterCallback<PointerUpEvent>(OnMemoCreateHourColumnPointerUp);
            _memoCreateMinuteColumn.RegisterCallback<PointerUpEvent>(OnMemoCreateMinuteColumnPointerUp);
            _memoCreatePeriodColumn.RegisterCallback<PointerUpEvent>(OnMemoCreatePeriodColumnPointerUp);
            _memoCreateHourColumn.RegisterCallback<PointerCancelEvent>(OnMemoCreateHourColumnPointerCancel);
            _memoCreateMinuteColumn.RegisterCallback<PointerCancelEvent>(OnMemoCreateMinuteColumnPointerCancel);
            _memoCreatePeriodColumn.RegisterCallback<PointerCancelEvent>(OnMemoCreatePeriodColumnPointerCancel);
            _memoCreateResetButton.clicked += ResetMemoCreateForm;
            _memoCreateSubmitButton.clicked += OnClickMemoCreateSubmit;
            _memoCreateUrgencyHighButton.clicked += () => SetMemoCreateUrgency("high");
            _memoCreateUrgencyMiddleButton.clicked += () => SetMemoCreateUrgency("middle");
            _memoCreateUrgencyLowButton.clicked += () => SetMemoCreateUrgency("low");
        }

        private void UnregisterMemoCreatePage()
        {
            _memoCreateBackButton.clicked -= OnClickMemoCreateBack;
            _memoCreateRepairerButton.clicked -= ToggleMemoCreateRepairerList;
            _memoCreateDateButton.clicked -= ShowMemoCreateCalendar;
            _memoCreateTimeButton.clicked -= ShowMemoCreateTimePicker;
            _memoCreateCalendarCloseButton.clicked -= HideMemoCreateCalendar;
            _memoCreateCalendarPrevButton.clicked -= ShowPreviousMemoCreateCalendarMonth;
            _memoCreateCalendarNextButton.clicked -= ShowNextMemoCreateCalendarMonth;
            _memoCreateTimeCloseButton.clicked -= HideMemoCreateTimePicker;
            _memoCreateHourColumn.UnregisterCallback<WheelEvent>(OnMemoCreateHourColumnWheel);
            _memoCreateMinuteColumn.UnregisterCallback<WheelEvent>(OnMemoCreateMinuteColumnWheel);
            _memoCreatePeriodColumn.UnregisterCallback<WheelEvent>(OnMemoCreatePeriodColumnWheel);
            _memoCreateHourColumn.UnregisterCallback<PointerUpEvent>(OnMemoCreateHourColumnPointerUp);
            _memoCreateMinuteColumn.UnregisterCallback<PointerUpEvent>(OnMemoCreateMinuteColumnPointerUp);
            _memoCreatePeriodColumn.UnregisterCallback<PointerUpEvent>(OnMemoCreatePeriodColumnPointerUp);
            _memoCreateHourColumn.UnregisterCallback<PointerCancelEvent>(OnMemoCreateHourColumnPointerCancel);
            _memoCreateMinuteColumn.UnregisterCallback<PointerCancelEvent>(OnMemoCreateMinuteColumnPointerCancel);
            _memoCreatePeriodColumn.UnregisterCallback<PointerCancelEvent>(OnMemoCreatePeriodColumnPointerCancel);
            _memoCreateResetButton.clicked -= ResetMemoCreateForm;
            _memoCreateSubmitButton.clicked -= OnClickMemoCreateSubmit;
        }

        private void ShowMemoCreatePage(ScanMapItem map)
        {
            _memoCreateTargetMap = map;
            RequestTabSwitch(1);
            ResetMemoCreateForm();
            _ = RebuildMemoCreateRepairerListAsync();
            HideMemoDetailPage();
            _memoSearchSourceInput.Blur();
            _memoSearchPageInput.Blur();
            SetVisible(_memoCreatePage, true);
            SetMemoCreateNavMode(true);
            _memoCreateTitleInput.schedule.Execute(() => _memoCreateTitleInput.Focus()).ExecuteLater(16);
        }

        private void HideMemoCreatePage()
        {
            SetVisible(_memoCreatePage, false);
            SetMemoCreateNavMode(false);
        }

        private void OnClickMemoCreateBack()
        {
            HideMemoCreatePage();
            RequestTabSwitch(3);
        }

        private void ResetMemoCreateForm()
        {
            _memoCreateTitleInput.SetValueWithoutNotify(string.Empty);
            _memoCreateBodyInput.SetValueWithoutNotify(string.Empty);
            _memoCreateRepairerPlayerId = string.Empty;
            _memoCreateRepairerLabel.text = "수리자 선택하기";
            SetMemoCreateRepairerExpanded(false);
            _memoCreateSelectedDate = new DateTime(2026, 1, 1);
            _memoCreateCalendarMonth = new DateTime(_memoCreateSelectedDate.Year, _memoCreateSelectedDate.Month, 1);
            _memoCreateSelectedHour = 9;
            _memoCreateSelectedMinute = 0;
            _memoCreateIsPm = false;
            RefreshMemoCreateDateTimeLabels();
            SetVisible(_memoCreateCalendar, false);
            SetVisible(_memoCreateTimePicker, false);
            SetMemoCreateUrgency("middle");
        }

        private void ShowMemoCreateCalendar()
        {
            BuildMemoCreateCalendar();
            SetVisible(_memoCreateTimePicker, false);
            SetVisible(_memoCreateCalendar, true);
        }

        private void HideMemoCreateCalendar()
        {
            SetVisible(_memoCreateCalendar, false);
            ResetMemoCreateScrollOffset();
        }

        private void ShowPreviousMemoCreateCalendarMonth()
        {
            _memoCreateCalendarMonth = _memoCreateCalendarMonth.AddMonths(-1);
            BuildMemoCreateCalendar();
        }

        private void ShowNextMemoCreateCalendarMonth()
        {
            _memoCreateCalendarMonth = _memoCreateCalendarMonth.AddMonths(1);
            BuildMemoCreateCalendar();
        }

        private void ShowMemoCreateTimePicker()
        {
            BuildMemoCreateTimePicker();
            SetVisible(_memoCreateCalendar, false);
            SetVisible(_memoCreateTimePicker, true);
        }

        private void HideMemoCreateTimePicker()
        {
            SetVisible(_memoCreateTimePicker, false);
            ResetMemoCreateScrollOffset();
        }

        private void ResetMemoCreateScrollOffset()
        {
            _memoCreateScroll.schedule.Execute(() => _memoCreateScroll.scrollOffset = Vector2.zero).ExecuteLater(16);
        }

        private void OnClickMemoCreateSubmit()
        {
            _ = CreateMemoFromCreatePageAsync();
        }

        private async Awaitable CreateMemoFromCreatePageAsync()
        {
            if (_memoCreateTargetMap == null)
            {
                return;
            }

            string title = _memoCreateTitleInput.value.Trim();
            string body = _memoCreateBodyInput.value.Trim();
            if (string.IsNullOrWhiteSpace(title))
            {
                title = "새 메모";
            }

            string assigneeName = string.IsNullOrWhiteSpace(_memoCreateRepairerPlayerId) ? string.Empty : _memoCreateRepairerLabel.text;
            bool isCreated = await CreateMemoForMapAsync(_memoCreateTargetMap, title, body, _memoCreateUrgency, _memoCreateRepairerPlayerId, assigneeName);
            if (!isCreated)
            {
                return;
            }

            HideMemoCreatePage();
            RequestTabSwitch(3);
        }

        private void ToggleMemoCreateRepairerList()
        {
            SetMemoCreateRepairerExpanded(!_memoCreateRepairerExpanded);
            if (_memoCreateRepairerExpanded)
            {
                _ = RebuildMemoCreateRepairerListAsync();
            }
        }

        private void SetMemoCreateRepairerExpanded(bool expanded)
        {
            _memoCreateRepairerExpanded = expanded;
            SetVisible(_memoCreateRepairerList, expanded);
            _memoCreateRepairerCard.EnableInClassList(SELECTED_CLASS, expanded);
            _memoCreateRepairerChevron.EnableInClassList(SELECTED_CLASS, expanded);
        }

        private async Awaitable RebuildMemoCreateRepairerListAsync()
        {
            _memoCreateRepairerItemsList.Clear();
            if (!_friendsInitialized)
            {
                await InitializeFriendsAsync();
            }

            if (!_friendsInitialized)
            {
                AddMemoCreateRepairerStatus("친구 정보를 불러오는 중입니다.");
                return;
            }

            if (FriendsService.Instance.Friends.Count == 0)
            {
                AddMemoCreateRepairerStatus("등록된 친구가 없습니다.");
                return;
            }

            foreach (Relationship relationship in FriendsService.Instance.Friends)
            {
                AddMemoCreateRepairerRow(relationship);
            }
        }

        private void AddMemoCreateRepairerRow(Relationship relationship)
        {
            Button row = new();
            row.AddToClassList("profile-menu-row");
            row.AddToClassList("profile-friend-row");
            row.EnableInClassList("is-first", _memoCreateRepairerItemsList.childCount == 0);
            string memberId = relationship.Member.Id;
            string memberName = GetMemberDisplayName(relationship.Member);
            row.clicked += () =>
            {
                _memoCreateRepairerPlayerId = memberId;
                _memoCreateRepairerLabel.text = memberName;
                SetMemoCreateRepairerExpanded(false);
            };

            Label nameLabel = new(memberName);
            nameLabel.AddToClassList("profile-friend-name");
            row.Add(nameLabel);
            _memoCreateRepairerItemsList.Add(row);
        }

        private void AddMemoCreateRepairerStatus(string text)
        {
            VisualElement row = new();
            row.AddToClassList("profile-friend-row");
            row.EnableInClassList("is-first", _memoCreateRepairerItemsList.childCount == 0);
            Label label = new(text);
            label.AddToClassList("profile-friend-company");
            row.Add(label);
            _memoCreateRepairerItemsList.Add(row);
        }

        private void SetMemoCreateUrgency(string urgency)
        {
            _memoCreateUrgency = urgency;
            _memoCreateUrgencyHighButton.EnableInClassList(SELECTED_CLASS, urgency == "high");
            _memoCreateUrgencyMiddleButton.EnableInClassList(SELECTED_CLASS, urgency == "middle");
            _memoCreateUrgencyLowButton.EnableInClassList(SELECTED_CLASS, urgency == "low");
        }

        private void BuildMemoCreateCalendar()
        {
            RebuildMemoCreateCalendarForMonth();
        }

        private void RebuildMemoCreateCalendarForMonth()
        {
            MemoCalendarUtility.Rebuild(_memoCreateCalendarGrid, _memoCreateCalendarTitle, _memoCreateCalendarMonth, _memoCreateSelectedDate, SelectMemoCreateDate);
        }

        private void SelectMemoCreateDate(DateTime selectedDate)
        {
            _memoCreateSelectedDate = selectedDate;
            _memoCreateCalendarMonth = new DateTime(selectedDate.Year, selectedDate.Month, 1);
            RefreshMemoCreateDateTimeLabels();
            BuildMemoCreateCalendar();
            HideMemoCreateCalendar();
        }

        private void BuildMemoCreateTimePicker()
        {
            _memoCreateHourColumn.contentContainer.Clear();
            _memoCreateMinuteColumn.contentContainer.Clear();
            _memoCreatePeriodColumn.contentContainer.Clear();

            for (int hour = 1; hour <= 12; hour++)
            {
                int selectedHour = hour;
                AddMemoCreateTimeOption(_memoCreateHourColumn, hour.ToString(), hour == _memoCreateSelectedHour, () =>
                {
                    SelectMemoCreateHour(selectedHour, true);
                });
            }

            for (int minute = 0; minute < 60; minute += 5)
            {
                int selectedMinute = minute;
                AddMemoCreateTimeOption(_memoCreateMinuteColumn, minute.ToString("00"), minute == _memoCreateSelectedMinute, () =>
                {
                    SelectMemoCreateMinute(selectedMinute, true);
                });
            }

            AddMemoCreateTimeOption(_memoCreatePeriodColumn, "AM", !_memoCreateIsPm, () =>
            {
                SelectMemoCreatePeriod(false, true);
            });
            AddMemoCreateTimeOption(_memoCreatePeriodColumn, "PM", _memoCreateIsPm, () =>
            {
                SelectMemoCreatePeriod(true, true);
            });

            SnapMemoCreateTimeColumnsToSelection();
        }

        private static void AddMemoCreateTimeOption(ScrollView column, string text, bool isSelected, Action onClick)
        {
            Button option = new() { text = text };
            option.AddToClassList("memo-create-time-option");
            option.EnableInClassList(SELECTED_CLASS, isSelected);
            option.clicked += onClick;
            column.contentContainer.Add(option);
        }

        private void OnMemoCreateHourColumnWheel(WheelEvent evt)
        {
            ScheduleMemoCreateHourColumnSnap();
        }

        private void OnMemoCreateMinuteColumnWheel(WheelEvent evt)
        {
            ScheduleMemoCreateMinuteColumnSnap();
        }

        private void OnMemoCreatePeriodColumnWheel(WheelEvent evt)
        {
            ScheduleMemoCreatePeriodColumnSnap();
        }

        private void OnMemoCreateHourColumnPointerUp(PointerUpEvent evt)
        {
            SnapMemoCreateHourColumnFromScroll();
        }

        private void OnMemoCreateMinuteColumnPointerUp(PointerUpEvent evt)
        {
            SnapMemoCreateMinuteColumnFromScroll();
        }

        private void OnMemoCreatePeriodColumnPointerUp(PointerUpEvent evt)
        {
            SnapMemoCreatePeriodColumnFromScroll();
        }

        private void OnMemoCreateHourColumnPointerCancel(PointerCancelEvent evt)
        {
            SnapMemoCreateHourColumnFromScroll();
        }

        private void OnMemoCreateMinuteColumnPointerCancel(PointerCancelEvent evt)
        {
            SnapMemoCreateMinuteColumnFromScroll();
        }

        private void OnMemoCreatePeriodColumnPointerCancel(PointerCancelEvent evt)
        {
            SnapMemoCreatePeriodColumnFromScroll();
        }

        private void ScheduleMemoCreateHourColumnSnap()
        {
            _memoCreateHourColumn.schedule.Execute(SnapMemoCreateHourColumnFromScroll).ExecuteLater(140);
        }

        private void ScheduleMemoCreateMinuteColumnSnap()
        {
            _memoCreateMinuteColumn.schedule.Execute(SnapMemoCreateMinuteColumnFromScroll).ExecuteLater(140);
        }

        private void ScheduleMemoCreatePeriodColumnSnap()
        {
            _memoCreatePeriodColumn.schedule.Execute(SnapMemoCreatePeriodColumnFromScroll).ExecuteLater(140);
        }

        private void SnapMemoCreateHourColumnFromScroll()
        {
            if (_isMemoCreateTimeScrollSyncing)
            {
                return;
            }

            int hour = Mathf.Clamp(Mathf.RoundToInt(_memoCreateHourColumn.scrollOffset.y / 70f) + 1, 1, 12);
            SelectMemoCreateHour(hour, true);
        }

        private void SnapMemoCreateMinuteColumnFromScroll()
        {
            if (_isMemoCreateTimeScrollSyncing)
            {
                return;
            }

            int minuteIndex = Mathf.Clamp(Mathf.RoundToInt(_memoCreateMinuteColumn.scrollOffset.y / 70f), 0, 11);
            SelectMemoCreateMinute(minuteIndex * 5, true);
        }

        private void SnapMemoCreatePeriodColumnFromScroll()
        {
            if (_isMemoCreateTimeScrollSyncing)
            {
                return;
            }

            int periodIndex = Mathf.Clamp(Mathf.RoundToInt(_memoCreatePeriodColumn.scrollOffset.y / 70f), 0, 1);
            SelectMemoCreatePeriod(periodIndex == 1, true);
        }

        private void SelectMemoCreateHour(int hour, bool shouldSnap)
        {
            _memoCreateSelectedHour = hour;
            RefreshMemoCreateDateTimeLabels();
            RefreshMemoCreateTimeColumnSelection(_memoCreateHourColumn, hour - 1);
            if (shouldSnap)
            {
                SetMemoCreateTimeColumnOffset(_memoCreateHourColumn, hour - 1);
            }
        }

        private void SelectMemoCreateMinute(int minute, bool shouldSnap)
        {
            _memoCreateSelectedMinute = minute;
            RefreshMemoCreateDateTimeLabels();
            int minuteIndex = minute / 5;
            RefreshMemoCreateTimeColumnSelection(_memoCreateMinuteColumn, minuteIndex);
            if (shouldSnap)
            {
                SetMemoCreateTimeColumnOffset(_memoCreateMinuteColumn, minuteIndex);
            }
        }

        private void SelectMemoCreatePeriod(bool isPm, bool shouldSnap)
        {
            _memoCreateIsPm = isPm;
            RefreshMemoCreateDateTimeLabels();
            int periodIndex = isPm ? 1 : 0;
            RefreshMemoCreateTimeColumnSelection(_memoCreatePeriodColumn, periodIndex);
            if (shouldSnap)
            {
                SetMemoCreateTimeColumnOffset(_memoCreatePeriodColumn, periodIndex);
            }
        }

        private static void RefreshMemoCreateTimeColumnSelection(ScrollView column, int selectedIndex)
        {
            for (int i = 0; i < column.contentContainer.childCount; i++)
            {
                column.contentContainer.ElementAt(i).EnableInClassList(SELECTED_CLASS, i == selectedIndex);
            }
        }

        private void SnapMemoCreateTimeColumnsToSelection()
        {
            SetMemoCreateTimeColumnOffset(_memoCreateHourColumn, _memoCreateSelectedHour - 1);
            SetMemoCreateTimeColumnOffset(_memoCreateMinuteColumn, _memoCreateSelectedMinute / 5);
            SetMemoCreateTimeColumnOffset(_memoCreatePeriodColumn, _memoCreateIsPm ? 1 : 0);
        }

        private void SetMemoCreateTimeColumnOffset(ScrollView column, int selectedIndex)
        {
            _isMemoCreateTimeScrollSyncing = true;
            column.scrollOffset = new Vector2(0f, selectedIndex * 70f);
            _isMemoCreateTimeScrollSyncing = false;
        }

        private void RefreshMemoCreateDateTimeLabels()
        {
            _memoCreateDateLabel.text = _memoCreateSelectedDate.ToString("yyyy-MM-dd");
            _memoCreateTimeLabel.text = $"{_memoCreateSelectedHour:00}:{_memoCreateSelectedMinute:00} {(_memoCreateIsPm ? "PM" : "AM")}";
        }
    }
}
