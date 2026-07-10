using System;
using System.Collections.Generic;
using System.IO;
using Unity.Services.Friends;
using Unity.Services.Friends.Models;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;

namespace MemoAnchor.UI
{
    public partial class MainView
    {
        [SerializeField] private VisualTreeAsset _memoCreateChecklistInputAsset;
        [SerializeField] private VisualTreeAsset _memoCreateMediaItemAsset;
        [SerializeField] private VisualTreeAsset _memoCreateRepairerRowAsset;
        [SerializeField] private VisualTreeAsset _memoCreateRepairerStatusAsset;
        [SerializeField] private VisualTreeAsset _memoCreateTimeOptionAsset;

        private VisualElement _memoCreatePage, _memoCreateCalendar, _memoCreateCalendarGrid, _memoCreateTimePicker, _memoCreateLoadingOverlay, _memoCreateLoadingSpinner;
        private VisualElement _memoCreateRepairerCard, _memoCreateRepairerList, _memoCreateRepairerItemsList, _memoCreateRepairerChevron;
        private VisualElement _memoCreateMediaDivider, _memoCreateMediaContent, _memoCreateMediaBox, _memoCreateMediaList;
        private VisualElement _memoCreateMediaSourceOverlay, _memoCreateMediaSourceSheet;
        private ScrollView _memoCreateScroll;
        private ScrollView _memoCreateHourColumn, _memoCreateMinuteColumn, _memoCreatePeriodColumn;
        private Button _memoCreateBackButton, _memoCreateResetButton, _memoCreateSubmitButton, _memoCreateRepairerButton;
        private Button _memoCreateCalendarCloseButton, _memoCreateCalendarPrevButton, _memoCreateCalendarNextButton;
        private Button _memoCreateDateButton, _memoCreateTimeButton, _memoCreateTimeCloseButton;
        private Button _memoCreateChecklistAddButton, _memoCreateMediaAddButton, _memoCreateMediaGalleryButton, _memoCreateMediaCameraButton;
        private Button _memoCreateUrgencyHighButton, _memoCreateUrgencyMiddleButton, _memoCreateUrgencyLowButton;
        private Label _memoCreateDateLabel, _memoCreateTimeLabel, _memoCreateCalendarTitle, _memoCreateContentTitle, _memoCreateRepairerLabel, _memoCreatePageTitle;
        private TextField _memoCreateTitleInput, _memoCreateBodyInput;
        private VisualElement _memoCreateTitleContent, _memoCreateTextContent, _memoCreateChecklistContent, _memoCreateChecklistList;
        private readonly List<TextField> _memoCreateChecklistInputs = new();
        private readonly List<MemoCreateMediaSelection> _memoCreateMediaSelections = new();
        private readonly List<VisualElement> _memoCreateMediaSpinners = new();
        private ScanMapItem _memoCreateTargetMap;
        private string _memoCreateKind = "text";
        private string _memoCreateUrgency = "middle";
        private string _memoCreateRepairerPlayerId = string.Empty;
        private string _memoCreateDefaultRepairerLabel;
        private string _memoCreateOriginalDueText = string.Empty;
        private MemoDetailItem _memoCreateEditingItem;
        private bool _memoCreateDueChanged;
        private bool _memoCreateRepairerExpanded;
        private bool _isMemoCreateSubmitting;
        private DateTime _memoCreateSelectedDate;
        private DateTime _memoCreateCalendarMonth;
        private int _memoCreateSelectedHour;
        private int _memoCreateSelectedMinute;
        private bool _memoCreateIsPm;
        private bool _isMemoCreateTimeScrollSyncing;
        private int _memoCreateMediaSourceTransitionToken;

        private void RegisterMapMemoCreatePage()
        {
            VisualElement mainRoot = _root.Q<VisualElement>("main-root");
            _memoCreatePage = _root.Q<VisualElement>("memo-create-page");
            _memoCreateLoadingOverlay = _root.Q<VisualElement>("memo-create-loading-overlay");
            _memoCreateLoadingSpinner = _root.Q<VisualElement>("memo-create-loading-spinner");
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
            _memoCreateChecklistAddButton = _root.Q<Button>("memo-create-checklist-add-button");
            _memoCreateDateLabel = _root.Q<Label>("memo-create-date-label");
            _memoCreateTimeLabel = _root.Q<Label>("memo-create-time-label");
            _memoCreateCalendarTitle = _root.Q<Label>("memo-create-calendar-title");
            _memoCreateContentTitle = _root.Q<Label>("memo-create-content-title");
            _memoCreateTitleContent = _root.Q<VisualElement>("memo-create-title-content");
            _memoCreateTextContent = _root.Q<VisualElement>("memo-create-text-content");
            _memoCreateChecklistContent = _root.Q<VisualElement>("memo-create-checklist-content");
            _memoCreateChecklistList = _root.Q<VisualElement>("memo-create-checklist-list");
            _memoCreateMediaDivider = _root.Q<VisualElement>("memo-create-media-divider");
            _memoCreateMediaContent = _root.Q<VisualElement>("memo-create-media-content");
            _memoCreateMediaBox = _root.Q<VisualElement>("memo-create-media-box");
            _memoCreateMediaList = _root.Q<VisualElement>("memo-create-media-list");
            _memoCreateMediaAddButton = _root.Q<Button>("memo-create-media-add-button");
            _memoCreateMediaSourceOverlay = _root.Q<VisualElement>("memo-create-media-source-overlay");
            _memoCreateMediaSourceSheet = _root.Q<VisualElement>("memo-create-media-source-sheet");
            _memoCreateMediaGalleryButton = _root.Q<Button>("memo-create-media-gallery-button");
            _memoCreateMediaCameraButton = _root.Q<Button>("memo-create-media-camera-button");
            _memoCreateBackButton = _root.Q<Button>("memo-create-back-button");
            _memoCreateRepairerButton = _root.Q<Button>("memo-create-repairer-button");
            _memoCreateRepairerCard = _root.Q<VisualElement>("memo-create-repairer-card");
            _memoCreateRepairerList = _root.Q<VisualElement>("memo-create-repairer-list");
            _memoCreateRepairerItemsList = _root.Q<VisualElement>("memo-create-repairer-items-list");
            _memoCreateRepairerChevron = _root.Q<VisualElement>("memo-create-repairer-chevron");
            _memoCreateRepairerLabel = _root.Q<Label>("memo-create-repairer-label");
            _memoCreatePageTitle = _root.Q<Label>("memo-create-page-title");
            _memoCreateResetButton = _root.Q<Button>("memo-create-reset-button");
            _memoCreateSubmitButton = _root.Q<Button>("memo-create-submit-button");
            _memoCreateTitleInput = _root.Q<TextField>("memo-create-title-input");
            _memoCreateBodyInput = _root.Q<TextField>("memo-create-body-input");
            _memoCreateUrgencyHighButton = _root.Q<Button>("memo-create-urgency-high");
            _memoCreateUrgencyMiddleButton = _root.Q<Button>("memo-create-urgency-middle");
            _memoCreateUrgencyLowButton = _root.Q<Button>("memo-create-urgency-low");
            _memoCreateDefaultRepairerLabel = _memoCreateRepairerLabel.text;

            mainRoot.Add(_memoCreateMediaSourceOverlay);
            _memoCreateMediaSourceOverlay.BringToFront();
            _memoCreateMediaSourceOverlay.AddToClassList(DIALOG_ANIM_READY_CLASS);
            _memoCreateMediaSourceOverlay.AddToClassList(HIDDEN_CLASS);
            SetMemoCreateDateTimeToNow();
            BuildMemoCreateCalendar();
            BuildMemoCreateTimePicker();
            SetVisible(_memoCreateLoadingOverlay, false);
            HideMapMemoCreatePage();

            _memoCreateBackButton.clicked += OnClickMemoCreateBack;
            _memoCreateRepairerButton.clicked += ToggleMemoCreateRepairerList;
            _memoCreateChecklistAddButton.clicked += AddMemoCreateChecklistInput;
            _memoCreateMediaAddButton.clicked += ShowMemoCreateMediaSourceDialog;
            _memoCreateMediaGalleryButton.clicked += ShowMemoCreateGalleryPicker;
            _memoCreateMediaCameraButton.clicked += ShowMemoCreateCamera;
            _memoCreateMediaSourceOverlay.RegisterCallback<ClickEvent>(OnClickMemoCreateMediaSourceOverlay);
            _memoCreateMediaSourceSheet.RegisterCallback<ClickEvent>(OnClickMemoCreateMediaSourceSheet);
            _memoCreateDateButton.clicked += ShowMemoCreateCalendar;
            _memoCreateTimeButton.clicked += ShowMemoCreateTimePicker;
            _memoCreateCalendarCloseButton.clicked += HideMemoCreateCalendar;
            _memoCreateCalendarPrevButton.clicked += ShowPreviousMemoCreateCalendarMonth;
            _memoCreateCalendarNextButton.clicked += ShowNextMemoCreateCalendarMonth;
            _memoCreateTimeCloseButton.clicked += HideMemoCreateTimePicker;
            _memoCreateHourColumn.RegisterCallback<WheelEvent>(OnMemoCreateHourColumnWheel);
            _memoCreateMinuteColumn.RegisterCallback<WheelEvent>(OnMemoCreateMinuteColumnWheel);
            _memoCreatePeriodColumn.RegisterCallback<WheelEvent>(OnMemoCreatePeriodColumnWheel);
            _memoCreateHourColumn.verticalScroller.valueChanged += OnMemoCreateHourColumnScrollValueChanged;
            _memoCreateMinuteColumn.verticalScroller.valueChanged += OnMemoCreateMinuteColumnScrollValueChanged;
            _memoCreatePeriodColumn.verticalScroller.valueChanged += OnMemoCreatePeriodColumnScrollValueChanged;
            _memoCreateHourColumn.RegisterCallback<PointerUpEvent>(OnMemoCreateHourColumnPointerUp);
            _memoCreateMinuteColumn.RegisterCallback<PointerUpEvent>(OnMemoCreateMinuteColumnPointerUp);
            _memoCreatePeriodColumn.RegisterCallback<PointerUpEvent>(OnMemoCreatePeriodColumnPointerUp);
            _memoCreateHourColumn.RegisterCallback<PointerCancelEvent>(OnMemoCreateHourColumnPointerCancel);
            _memoCreateMinuteColumn.RegisterCallback<PointerCancelEvent>(OnMemoCreateMinuteColumnPointerCancel);
            _memoCreatePeriodColumn.RegisterCallback<PointerCancelEvent>(OnMemoCreatePeriodColumnPointerCancel);
            _memoCreateResetButton.clicked += RequestResetMemoCreateForm;
            _memoCreateSubmitButton.clicked += OnClickMemoCreateSubmit;
            _memoCreateTitleInput.RegisterValueChangedCallback(OnMemoCreateTitleValueChanged);
            _memoCreateBodyInput.RegisterValueChangedCallback(OnMemoCreateBodyValueChanged);
            _memoCreateUrgencyHighButton.clicked += () => SetMemoCreateUrgency("high");
            _memoCreateUrgencyMiddleButton.clicked += () => SetMemoCreateUrgency("middle");
            _memoCreateUrgencyLowButton.clicked += () => SetMemoCreateUrgency("low");
        }

        private void UnregisterMapMemoCreatePage()
        {
            _memoCreateBackButton.clicked -= OnClickMemoCreateBack;
            _memoCreateRepairerButton.clicked -= ToggleMemoCreateRepairerList;
            _memoCreateChecklistAddButton.clicked -= AddMemoCreateChecklistInput;
            _memoCreateMediaAddButton.clicked -= ShowMemoCreateMediaSourceDialog;
            _memoCreateMediaGalleryButton.clicked -= ShowMemoCreateGalleryPicker;
            _memoCreateMediaCameraButton.clicked -= ShowMemoCreateCamera;
            _memoCreateMediaSourceOverlay.UnregisterCallback<ClickEvent>(OnClickMemoCreateMediaSourceOverlay);
            _memoCreateMediaSourceSheet.UnregisterCallback<ClickEvent>(OnClickMemoCreateMediaSourceSheet);
            _memoCreateDateButton.clicked -= ShowMemoCreateCalendar;
            _memoCreateTimeButton.clicked -= ShowMemoCreateTimePicker;
            _memoCreateCalendarCloseButton.clicked -= HideMemoCreateCalendar;
            _memoCreateCalendarPrevButton.clicked -= ShowPreviousMemoCreateCalendarMonth;
            _memoCreateCalendarNextButton.clicked -= ShowNextMemoCreateCalendarMonth;
            _memoCreateTimeCloseButton.clicked -= HideMemoCreateTimePicker;
            _memoCreateHourColumn.UnregisterCallback<WheelEvent>(OnMemoCreateHourColumnWheel);
            _memoCreateMinuteColumn.UnregisterCallback<WheelEvent>(OnMemoCreateMinuteColumnWheel);
            _memoCreatePeriodColumn.UnregisterCallback<WheelEvent>(OnMemoCreatePeriodColumnWheel);
            _memoCreateHourColumn.verticalScroller.valueChanged -= OnMemoCreateHourColumnScrollValueChanged;
            _memoCreateMinuteColumn.verticalScroller.valueChanged -= OnMemoCreateMinuteColumnScrollValueChanged;
            _memoCreatePeriodColumn.verticalScroller.valueChanged -= OnMemoCreatePeriodColumnScrollValueChanged;
            _memoCreateHourColumn.UnregisterCallback<PointerUpEvent>(OnMemoCreateHourColumnPointerUp);
            _memoCreateMinuteColumn.UnregisterCallback<PointerUpEvent>(OnMemoCreateMinuteColumnPointerUp);
            _memoCreatePeriodColumn.UnregisterCallback<PointerUpEvent>(OnMemoCreatePeriodColumnPointerUp);
            _memoCreateHourColumn.UnregisterCallback<PointerCancelEvent>(OnMemoCreateHourColumnPointerCancel);
            _memoCreateMinuteColumn.UnregisterCallback<PointerCancelEvent>(OnMemoCreateMinuteColumnPointerCancel);
            _memoCreatePeriodColumn.UnregisterCallback<PointerCancelEvent>(OnMemoCreatePeriodColumnPointerCancel);
            _memoCreateResetButton.clicked -= RequestResetMemoCreateForm;
            _memoCreateSubmitButton.clicked -= OnClickMemoCreateSubmit;
            _memoCreateTitleInput.UnregisterValueChangedCallback(OnMemoCreateTitleValueChanged);
            _memoCreateBodyInput.UnregisterValueChangedCallback(OnMemoCreateBodyValueChanged);
            foreach (TextField input in _memoCreateChecklistInputs)
            {
                input.UnregisterValueChangedCallback(OnMemoCreateChecklistValueChanged);
            }

            ClearMemoCreateMediaSelections();
        }

        private void ShowMapMemoCreatePage(ScanMapItem map, string kind)
        {
            _memoCreateEditingItem = null;
            _memoCreateOriginalDueText = string.Empty;
            _memoCreateDueChanged = true;
            _memoCreateTargetMap = map;
            RequestTabSwitch(1);
            ResetMemoCreateForm(kind);
            _ = RebuildMemoCreateRepairerListAsync();
            HideMemoDetailPage();
            SetVisible(_memoCreatePage, true);
            SetMemoCreateNavMode(true);
        }

        private void ShowMemoEditPage(MemoDetailItem item)
        {
            if (item.Kind != MemoDetailKind.Text && item.Kind != MemoDetailKind.Checklist && item.Kind != MemoDetailKind.Image)
            {
                PopupManager.ShowMessage("수정할 수 없음", "현재는 텍스트, 체크리스트, 사진 / 동영상 메모만 수정할 수 있습니다.", "확인");
                return;
            }

            ScanMapItem map = _scanMaps.Find(scanMap => string.Equals(scanMap.id, item.MapId, StringComparison.OrdinalIgnoreCase));
            if (map == null)
            {
                PopupManager.ShowMessage("메모 수정 실패", "메모가 연결된 맵을 찾을 수 없습니다.", "확인");
                return;
            }

            _memoCreateEditingItem = item;
            _memoCreateTargetMap = map;
            RequestTabSwitch(1);
            string kind = item.Kind == MemoDetailKind.Checklist ? "checklist" : item.Kind == MemoDetailKind.Image ? "image" : "text";
            ResetMemoCreateForm(kind);
            _memoCreateTitleInput.SetValueWithoutNotify(item.Title);
            _memoCreateBodyInput.SetValueWithoutNotify(item.Body);
            PopulateMemoCreateChecklistInputs(item.ChecklistItems);
            PopulateMemoCreateMediaSelections(item.ImageUrls);
            _memoCreateRepairerPlayerId = item.AssigneePlayerId;
            _memoCreateRepairerLabel.text = string.IsNullOrWhiteSpace(item.Assignee) ? _memoCreateDefaultRepairerLabel : item.Assignee;
            SetMemoCreateUrgency(ToMemoCreateUrgency(item.Urgency));
            ApplyMemoEditDueText(item.DueText);
            _memoCreateOriginalDueText = item.DueText;
            _memoCreateDueChanged = false;
            BuildMemoCreateCalendar();
            BuildMemoCreateTimePicker();
            _ = RebuildMemoCreateRepairerListAsync();
            HideMemoDetailPage();
            SetVisible(_memoCreatePage, true);
            SetMemoCreateNavMode(true);
        }

        private void HideMapMemoCreatePage()
        {
            HideMemoCreateMediaSourceDialog();
            ClearMemoCreateMediaSelections();
            SetVisible(_memoCreatePage, false);
            SetMemoCreateNavMode(false);
        }

        private void OnClickMemoCreateBack()
        {
            MemoDetailItem editingItem = _memoCreateEditingItem;
            _memoCreateEditingItem = null;
            HideMapMemoCreatePage();
            if (editingItem == null)
            {
                RequestTabSwitch(3);
                return;
            }

            RequestTabSwitch(1);
            ShowMemoDetailPage(editingItem);
        }

        private void ResetMemoCreateForm()
        {
            ResetMemoCreateForm(_memoCreateKind);
        }

        private void RequestResetMemoCreateForm()
        {
            PopupManager.ShowConfirm(
                "입력 내용 초기화",
                "입력한 내용을 모두 초기화합니다.",
                "취소",
                "초기화",
                ResetMemoCreateForm);
        }

        private void ResetMemoCreateForm(string kind)
        {
            _memoCreateTitleInput.SetValueWithoutNotify(string.Empty);
            _memoCreateBodyInput.SetValueWithoutNotify(string.Empty);
            InputValidationFeedback.ClearError(_memoCreateTitleContent);
            InputValidationFeedback.ClearError(_memoCreateTextContent);
            InputValidationFeedback.ClearError(_memoCreateMediaBox);
            ClearMemoCreateChecklistInputErrors();
            ClearMemoCreateMediaSelections();
            SetMemoCreateKind(kind);
            RebuildMemoCreateChecklistInputs();
            _memoCreateRepairerPlayerId = string.Empty;
            _memoCreateRepairerLabel.text = _memoCreateDefaultRepairerLabel;
            SetMemoCreateRepairerExpanded(false);
            SetMemoCreateDateTimeToNow();
            SetVisible(_memoCreateCalendar, false);
            SetVisible(_memoCreateTimePicker, false);
            SetMemoCreateUrgency("middle");
            _memoCreateDueChanged = _memoCreateEditingItem != null;
            RefreshMemoCreateModeText();
        }

        private void RefreshMemoCreateModeText()
        {
            bool isEditing = _memoCreateEditingItem != null;
            _memoCreatePageTitle.text = isEditing ? "메모 수정" : "메모 생성";
            _memoCreateSubmitButton.text = isEditing ? "수정하기" : "생성하기";
        }

        private void SetMemoCreateDateTimeToNow()
        {
            DateTime now = DateTime.Now;
            _memoCreateSelectedDate = now.Date;
            _memoCreateCalendarMonth = new DateTime(now.Year, now.Month, 1);
            _memoCreateSelectedHour = now.Hour % 12 == 0 ? 12 : now.Hour % 12;
            _memoCreateSelectedMinute = now.Minute / 5 * 5;
            _memoCreateIsPm = now.Hour >= 12;
            RefreshMemoCreateDateTimeLabels();
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

            if (_isMemoCreateSubmitting)
            {
                return;
            }

            string title = _memoCreateTitleInput.value.Trim();
            string body = _memoCreateBodyInput.value.Trim();
            List<MemoChecklistEntry> checklistItems = BuildMemoCreateChecklistItems();
            if (!ValidateMemoCreateInputs(title, body, checklistItems))
            {
                return;
            }

            _isMemoCreateSubmitting = true;
            SetMemoCreateSubmitting(true);

            try
            {
                string assigneeName = string.IsNullOrWhiteSpace(_memoCreateRepairerPlayerId) ? string.Empty : _memoCreateRepairerLabel.text;
                string dueText = _memoCreateEditingItem != null && !_memoCreateDueChanged
                    ? _memoCreateOriginalDueText
                    : BuildMemoCreateDueText();
                if (_memoCreateKind == "checklist")
                {
                    body = string.Empty;
                }

                List<string> imageUrls = await UploadMemoCreateMediaAsync();
                if (imageUrls == null)
                {
                    PopupManager.ShowMessage("파일 업로드 실패", "사진 또는 동영상을 서버에 저장하지 못했습니다.", "확인");
                    return;
                }

                if (_memoCreateEditingItem != null)
                {
                    MemoDetailItem updatedItem = await UpdateMemoForMapAsync(_memoCreateEditingItem, _memoCreateTargetMap, _memoCreateKind, title, body, _memoCreateUrgency, _memoCreateRepairerPlayerId, assigneeName, dueText, checklistItems, imageUrls);
                    if (updatedItem == null)
                    {
                        return;
                    }

                    _memoCreateEditingItem = null;
                    HideMapMemoCreatePage();
                    RequestTabSwitch(1);
                    ShowMemoDetailPage(updatedItem);
                    return;
                }

                bool isCreated = await CreateMemoForMapAsync(_memoCreateTargetMap, _memoCreateKind, title, body, _memoCreateUrgency, _memoCreateRepairerPlayerId, assigneeName, dueText, checklistItems, imageUrls);
                if (!isCreated)
                {
                    return;
                }

                HideMapMemoCreatePage();
                RequestTabSwitch(3);
            }
            finally
            {
                _isMemoCreateSubmitting = false;
                SetMemoCreateSubmitting(false);
            }
        }

        private bool ValidateMemoCreateInputs(string title, string body, List<MemoChecklistEntry> checklistItems)
        {
            InputValidationFeedback.ClearError(_memoCreateTitleContent);
            InputValidationFeedback.ClearError(_memoCreateTextContent);
            InputValidationFeedback.ClearError(_memoCreateMediaBox);
            ClearMemoCreateChecklistInputErrors();

            bool isTitleMissing = string.IsNullOrWhiteSpace(title);
            bool isContentMissing = _memoCreateKind == "checklist"
                ? checklistItems.Count == 0
                : string.IsNullOrWhiteSpace(body);
            bool isMediaMissing = _memoCreateKind == "image" && _memoCreateMediaSelections.Count == 0;

            if (isTitleMissing)
            {
                InputValidationFeedback.ShowError(_memoCreateTitleContent);
            }

            if (isContentMissing)
            {
                if (_memoCreateKind == "checklist")
                {
                    if (_memoCreateChecklistInputs.Count == 0)
                    {
                        AddMemoCreateChecklistInput();
                    }

                    foreach (TextField input in _memoCreateChecklistInputs)
                    {
                        InputValidationFeedback.ShowError(input.parent);
                    }
                }
                else
                {
                    InputValidationFeedback.ShowError(_memoCreateTextContent);
                }
            }

            if (isMediaMissing)
            {
                InputValidationFeedback.ShowError(_memoCreateMediaBox);
            }

            if (!isTitleMissing && !isContentMissing && !isMediaMissing)
            {
                return true;
            }

            List<VisualElement> invalidInputs = new();
            InputValidationFeedback.AddIfError(invalidInputs, _memoCreateTitleContent);
            InputValidationFeedback.AddIfError(invalidInputs, _memoCreateTextContent);
            InputValidationFeedback.AddIfError(invalidInputs, _memoCreateMediaBox);
            foreach (TextField input in _memoCreateChecklistInputs)
            {
                InputValidationFeedback.AddIfError(invalidInputs, input.parent);
            }
            _ = InputValidationFeedback.ShakeAsync(invalidInputs);
            string contentPrompt = _memoCreateKind == "checklist" ? "체크리스트를" : "내용을";
            if (isMediaMissing)
            {
                PopupManager.ShowMessage("입력 확인", "제목, 내용, 사진 또는 동영상을 확인해주세요.", "확인");
                return false;
            }
            string message = isTitleMissing && isContentMissing
                ? $"제목과 {contentPrompt} 입력해주세요."
                : isTitleMissing
                    ? "제목을 입력해주세요."
                    : $"{contentPrompt} 입력해주세요.";
            PopupManager.ShowMessage("입력 확인", message, "확인");
            return false;
        }

        private void SetMemoCreateSubmitting(bool isSubmitting)
        {
            if (isSubmitting)
            {
                LoadingSpinnerController.ShowOverlay(_memoCreateLoadingOverlay, _memoCreateLoadingSpinner);
            }
            else
            {
                LoadingSpinnerController.HideOverlay(_memoCreateLoadingOverlay, _memoCreateLoadingSpinner);
            }

            _memoCreateSubmitButton.SetEnabled(!isSubmitting);
            bool isEditing = _memoCreateEditingItem != null;
            _memoCreateSubmitButton.text = isSubmitting
                ? isEditing ? "수정 중..." : "생성 중..."
                : isEditing ? "수정하기" : "생성하기";
        }

        private void ToggleMemoCreateRepairerList()
        {
            SetMemoCreateRepairerExpanded(!_memoCreateRepairerExpanded);
            if (_memoCreateRepairerExpanded)
            {
                _ = RebuildMemoCreateRepairerListAsync();
            }
        }

        private void SetMemoCreateKind(string kind)
        {
            _memoCreateKind = kind == "checklist" ? "checklist" : kind == "image" ? "image" : "text";
            bool isChecklist = _memoCreateKind == "checklist";
            bool isMedia = _memoCreateKind == "image";
            _memoCreateContentTitle.text = isChecklist ? "체크리스트 (최대 10개)" : isMedia ? "메모 내용" : "타이핑";
            SetVisible(_memoCreateTitleContent, true);
            SetVisible(_memoCreateTextContent, !isChecklist);
            SetVisible(_memoCreateChecklistContent, isChecklist);
            SetVisible(_memoCreateMediaDivider, isMedia);
            SetVisible(_memoCreateMediaContent, isMedia);
            if (isChecklist && _memoCreateChecklistInputs.Count == 0)
            {
                AddMemoCreateChecklistInput();
            }
        }

        private void RebuildMemoCreateChecklistInputs()
        {
            foreach (TextField input in _memoCreateChecklistInputs)
            {
                input.UnregisterValueChangedCallback(OnMemoCreateChecklistValueChanged);
            }

            _memoCreateChecklistList.Clear();
            _memoCreateChecklistInputs.Clear();
            AddMemoCreateChecklistInput();
        }

        private void PopulateMemoCreateChecklistInputs(List<MemoChecklistItem> checklistItems)
        {
            foreach (TextField input in _memoCreateChecklistInputs)
            {
                input.UnregisterValueChangedCallback(OnMemoCreateChecklistValueChanged);
            }

            _memoCreateChecklistList.Clear();
            _memoCreateChecklistInputs.Clear();
            foreach (MemoChecklistItem item in checklistItems)
            {
                AddMemoCreateChecklistInput(item.Text);
            }

            if (_memoCreateChecklistInputs.Count == 0)
            {
                AddMemoCreateChecklistInput();
            }
        }

        private void ShowMemoCreateMediaSourceDialog()
        {
            if (_memoCreateMediaSelections.Count >= 3)
            {
                return;
            }

            _memoCreateMediaSourceTransitionToken++;
            _memoCreateMediaSourceOverlay.RemoveFromClassList(HIDDEN_CLASS);
            _memoCreateMediaSourceOverlay.RemoveFromClassList(DIALOG_OPEN_CLASS);
            int token = _memoCreateMediaSourceTransitionToken;
            _memoCreateMediaSourceOverlay.schedule.Execute(() =>
            {
                if (token == _memoCreateMediaSourceTransitionToken)
                {
                    _memoCreateMediaSourceOverlay.AddToClassList(DIALOG_OPEN_CLASS);
                }
            }).ExecuteLater(16);
        }

        private void HideMemoCreateMediaSourceDialog()
        {
            _memoCreateMediaSourceTransitionToken++;
            int token = _memoCreateMediaSourceTransitionToken;
            _memoCreateMediaSourceOverlay.RemoveFromClassList(DIALOG_OPEN_CLASS);
            _memoCreateMediaSourceOverlay.schedule.Execute(() =>
            {
                if (token == _memoCreateMediaSourceTransitionToken)
                {
                    _memoCreateMediaSourceOverlay.AddToClassList(HIDDEN_CLASS);
                }
            }).ExecuteLater(240);
        }

        private void OnClickMemoCreateMediaSourceOverlay(ClickEvent evt)
        {
            HideMemoCreateMediaSourceDialog();
        }

        private static void OnClickMemoCreateMediaSourceSheet(ClickEvent evt)
        {
            evt.StopPropagation();
        }

        private void ShowMemoCreateGalleryPicker()
        {
            HideMemoCreateMediaSourceDialog();
            if (NativeGallery.IsMediaPickerBusy())
            {
                return;
            }

            NativeGallery.GetMixedMediasFromGallery(
                OnMemoCreateMediaSelected,
                NativeGallery.MediaType.Image | NativeGallery.MediaType.Video,
                "사진 또는 동영상 선택");
        }

        private void ShowMemoCreateCamera()
        {
            HideMemoCreateMediaSourceDialog();
            if (NativeCamera.IsCameraBusy())
            {
                return;
            }

            NativeCamera.TakePicture(OnMemoCreateCameraPhotoSelected, 2048);
        }

        private void OnMemoCreateCameraPhotoSelected(string path)
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                OnMemoCreateMediaSelected(new[] { path });
            }
        }

        private void OnMemoCreateMediaSelected(string[] paths)
        {
            if (paths == null || paths.Length == 0)
            {
                return;
            }

            int remainingCount = 3 - _memoCreateMediaSelections.Count;
            int addedCount = 0;
            foreach (string path in paths)
            {
                if (addedCount >= remainingCount || string.IsNullOrWhiteSpace(path))
                {
                    break;
                }

                if (_memoCreateMediaSelections.Exists(item => string.Equals(item.Path, path, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                NativeGallery.MediaType mediaType = NativeGallery.GetMediaTypeOfFile(path);
                if ((mediaType != NativeGallery.MediaType.Image && mediaType != NativeGallery.MediaType.Video)
                    || !IsSupportedMemoMediaPath(path))
                {
                    continue;
                }

                Texture2D previewTexture = mediaType == NativeGallery.MediaType.Image
                    ? NativeGallery.LoadImageAtPath(path, 512)
                    : NativeGallery.GetVideoThumbnail(path, 512);
                _memoCreateMediaSelections.Add(new MemoCreateMediaSelection(path, false, mediaType == NativeGallery.MediaType.Video, previewTexture));
                addedCount++;
            }

            InputValidationFeedback.ClearError(_memoCreateMediaBox);
            RebuildMemoCreateMediaItems();
            if (paths.Length > remainingCount)
            {
                PopupManager.ShowMessage("첨부 개수 확인", "사진과 동영상은 최대 3개까지 추가할 수 있습니다.", "확인");
            }
        }

        private void PopulateMemoCreateMediaSelections(List<string> imageUrls)
        {
            ClearMemoCreateMediaSelections();
            foreach (string imageUrl in imageUrls)
            {
                _memoCreateMediaSelections.Add(new MemoCreateMediaSelection(imageUrl, true, IsVideoMediaPath(imageUrl), null));
            }

            RebuildMemoCreateMediaItems();
        }

        private void RebuildMemoCreateMediaItems()
        {
            StopAllMemoCreateMediaSpinners();
            _memoCreateMediaList.Clear();
            foreach (MemoCreateMediaSelection selection in _memoCreateMediaSelections)
            {
                TemplateContainer template = _memoCreateMediaItemAsset.Instantiate();
                VisualElement item = template.Q<VisualElement>("memo-create-media-item");
                VisualElement preview = template.Q<VisualElement>("memo-create-media-preview");
                VisualElement spinner = template.Q<VisualElement>("memo-create-media-spinner");
                Label videoLabel = template.Q<Label>("memo-create-media-video-label");
                Button removeButton = template.Q<Button>("memo-create-media-remove");
                if (selection.PreviewTexture != null)
                {
                    preview.style.backgroundImage = new StyleBackground(selection.PreviewTexture);
                    preview.AddToClassList("has-preview");
                }
                else if (selection.IsRemote && !selection.IsVideo)
                {
                    SetVisible(preview, false);
                    SetVisible(spinner, true);
                    _memoCreateMediaSpinners.Add(spinner);
                    LoadingSpinnerController.Start(spinner);
                    _ = LoadMemoCreateRemoteMediaPreviewAsync(selection, preview, spinner);
                }

                SetVisible(videoLabel, selection.IsVideo);
                item.tooltip = Path.GetFileName(selection.Path);
                removeButton.clicked += () => RemoveMemoCreateMediaSelection(selection);
                _memoCreateMediaList.Add(item);
            }

            SetVisible(_memoCreateMediaAddButton, _memoCreateMediaSelections.Count < 3);
        }

        private async Awaitable LoadMemoCreateRemoteMediaPreviewAsync(MemoCreateMediaSelection selection, VisualElement preview, VisualElement spinner)
        {
            using UnityWebRequest request = UnityWebRequestTexture.GetTexture(GetMemoMediaUrl(selection.Path));
            await ServicesManager.SendRequestAsync(request);
            if (request.result != UnityWebRequest.Result.Success)
            {
                StopMemoCreateMediaSpinner(spinner);
                SetVisible(preview, true);
                return;
            }

            Texture2D texture = DownloadHandlerTexture.GetContent(request);
            if (preview.panel == null || !_memoCreateMediaSelections.Contains(selection))
            {
                Destroy(texture);
                StopMemoCreateMediaSpinner(spinner);
                return;
            }

            selection.PreviewTexture = texture;
            preview.style.backgroundImage = new StyleBackground(texture);
            preview.AddToClassList("has-preview");
            StopMemoCreateMediaSpinner(spinner);
            SetVisible(preview, true);
        }

        private void StopMemoCreateMediaSpinner(VisualElement spinner)
        {
            LoadingSpinnerController.Stop(spinner);
            SetVisible(spinner, false);
            _memoCreateMediaSpinners.Remove(spinner);
        }

        private void RemoveMemoCreateMediaSelection(MemoCreateMediaSelection selection)
        {
            if (selection.PreviewTexture != null)
            {
                Destroy(selection.PreviewTexture);
            }

            _memoCreateMediaSelections.Remove(selection);
            RebuildMemoCreateMediaItems();
        }

        private void ClearMemoCreateMediaSelections()
        {
            StopAllMemoCreateMediaSpinners();
            foreach (MemoCreateMediaSelection selection in _memoCreateMediaSelections)
            {
                if (selection.PreviewTexture != null)
                {
                    Destroy(selection.PreviewTexture);
                }
            }

            _memoCreateMediaSelections.Clear();
            _memoCreateMediaList.Clear();
        }

        private void StopAllMemoCreateMediaSpinners()
        {
            foreach (VisualElement spinner in _memoCreateMediaSpinners)
            {
                LoadingSpinnerController.Stop(spinner);
            }

            _memoCreateMediaSpinners.Clear();
        }

        private async Awaitable<List<string>> UploadMemoCreateMediaAsync()
        {
            var imageUrls = new List<string>();
            var localPaths = new List<string>();
            foreach (MemoCreateMediaSelection selection in _memoCreateMediaSelections)
            {
                if (selection.IsRemote)
                {
                    imageUrls.Add(selection.Path);
                }
                else
                {
                    localPaths.Add(selection.Path);
                }
            }

            MemoMediaUploadResult uploadResult = await _memoService.UploadMemoMediaAsync(localPaths);
            if (!uploadResult.IsSuccess)
            {
                return null;
            }

            imageUrls.AddRange(uploadResult.Urls);
            return imageUrls;
        }

        private static bool IsVideoMediaPath(string path)
        {
            string extension = Path.GetExtension(path.Split('?')[0]).ToLowerInvariant();
            return extension == ".mp4" || extension == ".mov" || extension == ".avi" || extension == ".webm"
                || extension == ".m4v" || extension == ".3gp" || extension == ".mkv";
        }

        private static bool IsSupportedMemoMediaPath(string path)
        {
            string extension = Path.GetExtension(path).ToLowerInvariant();
            return IsVideoMediaPath(path)
                || extension == ".jpg" || extension == ".jpeg" || extension == ".png" || extension == ".gif"
                || extension == ".webp" || extension == ".heic" || extension == ".heif" || extension == ".bmp"
                || extension == ".tif" || extension == ".tiff";
        }

        private void AddMemoCreateChecklistInput()
        {
            AddMemoCreateChecklistInput(string.Empty);
        }

        private void AddMemoCreateChecklistInput(string value)
        {
            if (_memoCreateChecklistInputs.Count >= 10)
            {
                return;
            }

            TemplateContainer item = _memoCreateChecklistInputAsset.Instantiate();
            VisualElement row = item.Q<VisualElement>("memo-create-checklist-row");
            TextField input = item.Q<TextField>("memo-create-checklist-input");
            input.label = string.Empty;
            input.textEdition.placeholder = "체크리스트 내용을 작성해주세요.";
            input.SetValueWithoutNotify(value);
            input.RegisterValueChangedCallback(OnMemoCreateChecklistValueChanged);

            Button removeButton = item.Q<Button>("memo-create-checklist-remove");
            removeButton.clicked += () => RemoveMemoCreateChecklistInput(row, input);

            _memoCreateChecklistList.Add(row);
            _memoCreateChecklistInputs.Add(input);
        }

        private void RemoveMemoCreateChecklistInput(VisualElement row, TextField input)
        {
            input.UnregisterValueChangedCallback(OnMemoCreateChecklistValueChanged);
            _memoCreateChecklistInputs.Remove(input);
            row.RemoveFromHierarchy();
        }

        private void OnMemoCreateTitleValueChanged(ChangeEvent<string> evt)
        {
            InputValidationFeedback.ClearError(_memoCreateTitleContent);
        }

        private void OnMemoCreateBodyValueChanged(ChangeEvent<string> evt)
        {
            InputValidationFeedback.ClearError(_memoCreateTextContent);
        }

        private void OnMemoCreateChecklistValueChanged(ChangeEvent<string> evt)
        {
            InputValidationFeedback.ClearError(((VisualElement)evt.target).parent);
        }

        private void ClearMemoCreateChecklistInputErrors()
        {
            foreach (TextField input in _memoCreateChecklistInputs)
            {
                InputValidationFeedback.ClearError(input.parent);
            }
        }

        private List<MemoChecklistEntry> BuildMemoCreateChecklistItems()
        {
            List<MemoChecklistEntry> items = new();
            for (int i = 0; i < _memoCreateChecklistInputs.Count; i++)
            {
                TextField input = _memoCreateChecklistInputs[i];
                string text = input.value.Trim();
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                bool done = _memoCreateEditingItem != null
                    && i < _memoCreateEditingItem.ChecklistItems.Count
                    && _memoCreateEditingItem.ChecklistItems[i].Done;
                items.Add(new MemoChecklistEntry { text = text, done = done });
            }

            return items;
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
            TemplateContainer item = _memoCreateRepairerRowAsset.Instantiate();
            Button row = item.Q<Button>("memo-create-repairer-row");
            row.EnableInClassList("is-first", _memoCreateRepairerItemsList.childCount == 0);
            string memberId = relationship.Member.Id;
            string memberName = GetMemberDisplayName(relationship.Member);
            row.clicked += () =>
            {
                _memoCreateRepairerPlayerId = memberId;
                _memoCreateRepairerLabel.text = memberName;
                SetMemoCreateRepairerExpanded(false);
            };

            item.Q<Label>("memo-create-repairer-name").text = memberName;
            _memoCreateRepairerItemsList.Add(row);
        }

        private void AddMemoCreateRepairerStatus(string text)
        {
            TemplateContainer item = _memoCreateRepairerStatusAsset.Instantiate();
            VisualElement row = item.Q<VisualElement>("memo-create-repairer-status-row");
            row.EnableInClassList("is-first", _memoCreateRepairerItemsList.childCount == 0);
            item.Q<Label>("memo-create-repairer-status-label").text = text;
            _memoCreateRepairerItemsList.Add(row);
        }

        private void SetMemoCreateUrgency(string urgency)
        {
            _memoCreateUrgency = urgency;
            _memoCreateUrgencyHighButton.EnableInClassList(SELECTED_CLASS, urgency == "high");
            _memoCreateUrgencyMiddleButton.EnableInClassList(SELECTED_CLASS, urgency == "middle");
            _memoCreateUrgencyLowButton.EnableInClassList(SELECTED_CLASS, urgency == "low");
        }

        private static string ToMemoCreateUrgency(MemoUrgency urgency)
        {
            return urgency switch
            {
                MemoUrgency.High => "high",
                MemoUrgency.Low => "low",
                _ => "middle"
            };
        }

        private void ApplyMemoEditDueText(string dueText)
        {
            string value = dueText.StartsWith("마감 ", StringComparison.Ordinal) ? dueText[3..] : dueText;
            if (value.EndsWith("일 전", StringComparison.Ordinal)
                && int.TryParse(value[..^3], out int dayCount)
                && dayCount >= 0
                && dayCount <= 3650)
            {
                _memoCreateSelectedDate = DateTime.Today.AddDays(dayCount);
                _memoCreateCalendarMonth = new DateTime(_memoCreateSelectedDate.Year, _memoCreateSelectedDate.Month, 1);
                RefreshMemoCreateDateTimeLabels();
            }
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
            _memoCreateDueChanged = true;
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

        private void AddMemoCreateTimeOption(ScrollView column, string text, bool isSelected, Action onClick)
        {
            TemplateContainer item = _memoCreateTimeOptionAsset.Instantiate();
            Button option = item.Q<Button>("memo-create-time-option");
            option.text = text;
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

        private void OnMemoCreateHourColumnScrollValueChanged(float value)
        {
            SelectMemoCreateHourFromOffset(value, false);
        }

        private void OnMemoCreateMinuteColumnScrollValueChanged(float value)
        {
            SelectMemoCreateMinuteFromOffset(value, false);
        }

        private void OnMemoCreatePeriodColumnScrollValueChanged(float value)
        {
            SelectMemoCreatePeriodFromOffset(value, false);
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

            SelectMemoCreateHourFromScroll(true);
        }

        private void SnapMemoCreateMinuteColumnFromScroll()
        {
            if (_isMemoCreateTimeScrollSyncing)
            {
                return;
            }

            SelectMemoCreateMinuteFromScroll(true);
        }

        private void SnapMemoCreatePeriodColumnFromScroll()
        {
            if (_isMemoCreateTimeScrollSyncing)
            {
                return;
            }

            SelectMemoCreatePeriodFromScroll(true);
        }

        private void SelectMemoCreateHourFromScroll(bool shouldSnap)
        {
            SelectMemoCreateHourFromOffset(_memoCreateHourColumn.scrollOffset.y, shouldSnap);
        }

        private void SelectMemoCreateMinuteFromScroll(bool shouldSnap)
        {
            SelectMemoCreateMinuteFromOffset(_memoCreateMinuteColumn.scrollOffset.y, shouldSnap);
        }

        private void SelectMemoCreatePeriodFromScroll(bool shouldSnap)
        {
            SelectMemoCreatePeriodFromOffset(_memoCreatePeriodColumn.scrollOffset.y, shouldSnap);
        }

        private void SelectMemoCreateHourFromOffset(float offsetY, bool shouldSnap)
        {
            if (_isMemoCreateTimeScrollSyncing)
            {
                return;
            }

            int hour = Mathf.Clamp(Mathf.RoundToInt(offsetY / 70f) + 1, 1, 12);
            SelectMemoCreateHour(hour, shouldSnap);
        }

        private void SelectMemoCreateMinuteFromOffset(float offsetY, bool shouldSnap)
        {
            if (_isMemoCreateTimeScrollSyncing)
            {
                return;
            }

            int minuteIndex = Mathf.Clamp(Mathf.RoundToInt(offsetY / 70f), 0, 11);
            SelectMemoCreateMinute(minuteIndex * 5, shouldSnap);
        }

        private void SelectMemoCreatePeriodFromOffset(float offsetY, bool shouldSnap)
        {
            if (_isMemoCreateTimeScrollSyncing)
            {
                return;
            }

            int periodIndex = Mathf.Clamp(Mathf.RoundToInt(offsetY / 70f), 0, 1);
            SelectMemoCreatePeriod(periodIndex == 1, shouldSnap);
        }

        private void SelectMemoCreateHour(int hour, bool shouldSnap)
        {
            if (_memoCreateSelectedHour == hour)
            {
                if (shouldSnap)
                {
                    SetMemoCreateTimeColumnOffset(_memoCreateHourColumn, hour - 1);
                }

                return;
            }

            _memoCreateSelectedHour = hour;
            _memoCreateDueChanged = true;
            RefreshMemoCreateDateTimeLabels();
            RefreshMemoCreateTimeColumnSelection(_memoCreateHourColumn, hour - 1);
            if (shouldSnap)
            {
                SetMemoCreateTimeColumnOffset(_memoCreateHourColumn, hour - 1);
            }
        }

        private void SelectMemoCreateMinute(int minute, bool shouldSnap)
        {
            if (_memoCreateSelectedMinute == minute)
            {
                if (shouldSnap)
                {
                    SetMemoCreateTimeColumnOffset(_memoCreateMinuteColumn, minute / 5);
                }

                return;
            }

            _memoCreateSelectedMinute = minute;
            _memoCreateDueChanged = true;
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
            if (_memoCreateIsPm == isPm)
            {
                if (shouldSnap)
                {
                    SetMemoCreateTimeColumnOffset(_memoCreatePeriodColumn, isPm ? 1 : 0);
                }

                return;
            }

            _memoCreateIsPm = isPm;
            _memoCreateDueChanged = true;
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

        private string BuildMemoCreateDueText()
        {
            int dayDelta = (_memoCreateSelectedDate.Date - DateTime.Today).Days;
            if (dayDelta > 0)
            {
                return $"{dayDelta}일 전";
            }

            if (dayDelta == 0)
            {
                return "오늘";
            }

            return "지남";
        }

        private sealed class MemoCreateMediaSelection
        {
            public readonly string Path;
            public readonly bool IsRemote;
            public readonly bool IsVideo;
            public Texture2D PreviewTexture;

            public MemoCreateMediaSelection(string path, bool isRemote, bool isVideo, Texture2D previewTexture)
            {
                Path = path;
                IsRemote = isRemote;
                IsVideo = isVideo;
                PreviewTexture = previewTexture;
            }
        }
    }
}
