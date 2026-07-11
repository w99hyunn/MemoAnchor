using System.Collections.Generic;
using System;
using System.Text;
using Unity.Services.Authentication;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;
using UnityEngine.Video;

namespace MemoAnchor.UI
{
    public partial class MainView
    {
        [SerializeField] private VisualTreeAsset _memoListItemAsset;

        private readonly MemoService _memoService = new();
        private readonly List<MemoDetailItem> _memoDetailItems = new();
        private readonly List<MemoDetailItem> _memoTrashItems = new();
        private readonly List<Texture2D> _memoDetailMediaTextures = new();
        private readonly List<VisualElement> _memoDetailMediaSpinners = new();
        private readonly HashSet<string> _selectedTrashMemoIds = new(StringComparer.OrdinalIgnoreCase);
        private VisualElement _memoListContainer, _memoDetailPage, _memoDetailMenu, _memoDetailContent, _memoTrashPage, _memoTrashListContainer, _memoLoadingOverlay, _memoLoadingSpinner;
        private VisualElement _memoMediaViewerOverlay, _memoMediaViewerPanel, _memoMediaViewerSpinner, _memoMediaViewerVideoControls;
        private Image _memoMediaViewerImage;
        private VisualElement _memoMediaViewerPlayIcon;
        private Button _memoDetailBackButton, _memoDetailMenuButton, _memoDetailEditButton, _memoDetailDeleteButton, _memoDetailExportButton, _memoTrashButton, _memoTrashBackButton, _memoTrashSelectButton;
        private Button _memoDetailRequestButton, _memoDetailCompleteButton;
        private Button _memoTrashPermanentDeleteButton, _memoTrashRestoreButton, _memoMediaViewerCloseButton, _memoMediaViewerRotateButton, _memoMediaViewerPlayButton;
        private Label _memoDetailPlaceLabel, _memoMediaViewerTimeLabel;
        private Slider _memoMediaViewerSeekSlider;
        private MemoDetailItem _currentMemoDetailItem;
        private bool _isMemoListLoading;
        private bool _isMemoTrashLoading;
        private bool _isCreatingMemo;
        private bool _isMemoTrashSelecting;
        private Texture2D _memoMediaViewerTexture;
        private RenderTexture _memoMediaViewerVideoTexture;
        private VideoPlayer _memoMediaViewerVideoPlayer;
        private string _memoMediaViewerVideoError = string.Empty;
        private IVisualElementScheduledItem _memoMediaViewerControlsSchedule;
        private int _memoMediaViewerLoadToken;
        private int _memoMediaViewerRotation;
        private int _memoMediaViewerFirstPointerId = -1;
        private int _memoMediaViewerSecondPointerId = -1;
        private Vector2 _memoMediaViewerFirstPointerPosition;
        private Vector2 _memoMediaViewerSecondPointerPosition;
        private float _memoMediaViewerZoom = 1f;
        private float _memoMediaViewerPinchStartDistance;
        private float _memoMediaViewerPinchStartZoom = 1f;

        private void RegisterMemoDetailPage()
        {
            VisualElement mainRoot = _root.Q<VisualElement>("main-root");
            _memoListContainer = _root.Q<VisualElement>("memo-list-container");
            _memoDetailPage = _root.Q<VisualElement>("memo-detail-page");
            _memoDetailMenu = _root.Q<VisualElement>("memo-detail-menu");
            _memoDetailContent = _root.Q<VisualElement>("memo-detail-content");
            _memoTrashPage = _root.Q<VisualElement>("memo-trash-page");
            _memoTrashListContainer = _root.Q<VisualElement>("memo-trash-list-container");
            _memoLoadingOverlay = _root.Q<VisualElement>("memo-loading-overlay");
            _memoLoadingSpinner = _root.Q<VisualElement>("memo-loading-spinner");
            _memoDetailBackButton = _root.Q<Button>("memo-detail-back-button");
            _memoDetailMenuButton = _root.Q<Button>("memo-detail-menu-button");
            _memoDetailEditButton = _root.Q<Button>("memo-detail-edit-button");
            _memoDetailDeleteButton = _root.Q<Button>("memo-detail-delete-button");
            _memoDetailExportButton = _root.Q<Button>("memo-detail-export-button");
            _memoDetailRequestButton = _root.Q<Button>("memo-detail-request-button");
            _memoDetailCompleteButton = _root.Q<Button>("memo-detail-complete-button");
            _memoTrashButton = _root.Q<Button>("memo-trash-button");
            _memoTrashBackButton = _root.Q<Button>("memo-trash-back-button");
            _memoTrashSelectButton = _root.Q<Button>("memo-trash-select-button");
            _memoTrashPermanentDeleteButton = _root.Q<Button>("memo-trash-permanent-delete-button");
            _memoTrashRestoreButton = _root.Q<Button>("memo-trash-restore-button");
            _memoDetailPlaceLabel = _root.Q<Label>("memo-detail-place-label");
            _memoMediaViewerOverlay = _root.Q<VisualElement>("memo-media-viewer-overlay");
            _memoMediaViewerPanel = _root.Q<VisualElement>("memo-media-viewer-panel");
            _memoMediaViewerImage = _root.Q<Image>("memo-media-viewer-image");
            _memoMediaViewerSpinner = _root.Q<VisualElement>("memo-media-viewer-spinner");
            _memoMediaViewerCloseButton = _root.Q<Button>("memo-media-viewer-close-button");
            _memoMediaViewerRotateButton = _root.Q<Button>("memo-media-viewer-rotate-button");
            _memoMediaViewerVideoControls = _root.Q<VisualElement>("memo-media-viewer-video-controls");
            _memoMediaViewerPlayButton = _root.Q<Button>("memo-media-viewer-play-button");
            _memoMediaViewerPlayIcon = _root.Q<VisualElement>("memo-media-viewer-play-icon");
            _memoMediaViewerSeekSlider = _root.Q<Slider>("memo-media-viewer-seek-slider");
            _memoMediaViewerTimeLabel = _root.Q<Label>("memo-media-viewer-time-label");

            if (!TryGetComponent<VideoPlayer>(out _memoMediaViewerVideoPlayer))
            {
                _memoMediaViewerVideoPlayer = gameObject.AddComponent<VideoPlayer>();
            }

            _memoMediaViewerVideoPlayer.playOnAwake = false;
            _memoMediaViewerVideoPlayer.renderMode = VideoRenderMode.RenderTexture;
            _memoMediaViewerVideoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
            _memoMediaViewerVideoPlayer.aspectRatio = VideoAspectRatio.FitInside;
            _memoMediaViewerVideoPlayer.waitForFirstFrame = true;
            _memoMediaViewerVideoPlayer.skipOnDrop = true;
            _memoMediaViewerVideoPlayer.errorReceived += OnMemoMediaViewerVideoError;
            _memoMediaViewerControlsSchedule = _memoMediaViewerVideoControls.schedule.Execute(UpdateMemoMediaViewerControls).Every(100);
            _memoMediaViewerControlsSchedule.Pause();

            mainRoot.Add(_memoMediaViewerOverlay);
            _memoMediaViewerOverlay.BringToFront();
            SetVisible(_memoMediaViewerOverlay, false);
            RebuildMemoList();
            HideMemoDetailPage();
            SetVisible(_memoLoadingOverlay, false);

            _memoDetailBackButton.clicked += HideMemoDetailPage;
            _memoDetailMenuButton.clicked += ToggleMemoDetailMenu;
            _memoDetailEditButton.clicked += ShowCurrentMemoEditPage;
            _memoDetailDeleteButton.clicked += ShowCurrentMemoDeleteConfirm;
            _memoDetailExportButton.clicked += ShareCurrentMemo;
            _memoDetailRequestButton.clicked += OnClickMemoDetailRequest;
            _memoDetailCompleteButton.clicked += OnClickMemoDetailComplete;
            _memoTrashButton.clicked += ShowMemoTrashPage;
            _memoTrashBackButton.clicked += HideMemoTrashPage;
            _memoTrashSelectButton.clicked += ToggleMemoTrashSelectMode;
            _memoTrashPermanentDeleteButton.clicked += ShowSelectedTrashDeleteConfirm;
            _memoTrashRestoreButton.clicked += ShowSelectedTrashRestoreConfirm;
            _memoMediaViewerCloseButton.clicked += HideMemoMediaViewer;
            _memoMediaViewerRotateButton.clicked += RotateMemoMediaViewer;
            _memoMediaViewerPlayButton.clicked += ToggleMemoMediaViewerVideoPlayback;
            _memoMediaViewerSeekSlider.RegisterValueChangedCallback(OnMemoMediaViewerSeekChanged);
            _memoMediaViewerOverlay.RegisterCallback<ClickEvent>(OnClickMemoMediaViewerOverlay);
            _memoMediaViewerPanel.RegisterCallback<ClickEvent>(OnClickMemoMediaViewerPanel);
            _memoMediaViewerPanel.RegisterCallback<PointerDownEvent>(OnMemoMediaViewerPointerDown);
            _memoMediaViewerPanel.RegisterCallback<PointerMoveEvent>(OnMemoMediaViewerPointerMove);
            _memoMediaViewerPanel.RegisterCallback<PointerUpEvent>(OnMemoMediaViewerPointerUp);
            _memoMediaViewerPanel.RegisterCallback<PointerCancelEvent>(OnMemoMediaViewerPointerCancel);
            _ = RefreshMemoListAsync();
        }

        private void UnregisterMemoDetailPage()
        {
            StopMemoVoicePreview();
            ClearMemoDetailMediaTextures();
            HideMemoMediaViewer();
            _memoDetailBackButton.clicked -= HideMemoDetailPage;
            _memoDetailMenuButton.clicked -= ToggleMemoDetailMenu;
            _memoDetailEditButton.clicked -= ShowCurrentMemoEditPage;
            _memoDetailDeleteButton.clicked -= ShowCurrentMemoDeleteConfirm;
            _memoDetailExportButton.clicked -= ShareCurrentMemo;
            _memoDetailRequestButton.clicked -= OnClickMemoDetailRequest;
            _memoDetailCompleteButton.clicked -= OnClickMemoDetailComplete;
            _memoTrashButton.clicked -= ShowMemoTrashPage;
            _memoTrashBackButton.clicked -= HideMemoTrashPage;
            _memoTrashSelectButton.clicked -= ToggleMemoTrashSelectMode;
            _memoTrashPermanentDeleteButton.clicked -= ShowSelectedTrashDeleteConfirm;
            _memoTrashRestoreButton.clicked -= ShowSelectedTrashRestoreConfirm;
            _memoMediaViewerCloseButton.clicked -= HideMemoMediaViewer;
            _memoMediaViewerRotateButton.clicked -= RotateMemoMediaViewer;
            _memoMediaViewerPlayButton.clicked -= ToggleMemoMediaViewerVideoPlayback;
            _memoMediaViewerSeekSlider.UnregisterValueChangedCallback(OnMemoMediaViewerSeekChanged);
            _memoMediaViewerOverlay.UnregisterCallback<ClickEvent>(OnClickMemoMediaViewerOverlay);
            _memoMediaViewerPanel.UnregisterCallback<ClickEvent>(OnClickMemoMediaViewerPanel);
            _memoMediaViewerPanel.UnregisterCallback<PointerDownEvent>(OnMemoMediaViewerPointerDown);
            _memoMediaViewerPanel.UnregisterCallback<PointerMoveEvent>(OnMemoMediaViewerPointerMove);
            _memoMediaViewerPanel.UnregisterCallback<PointerUpEvent>(OnMemoMediaViewerPointerUp);
            _memoMediaViewerPanel.UnregisterCallback<PointerCancelEvent>(OnMemoMediaViewerPointerCancel);
            _memoMediaViewerVideoPlayer.errorReceived -= OnMemoMediaViewerVideoError;
        }

        public async Awaitable RefreshMemoListAsync()
        {
            if (_isMemoListLoading)
            {
                return;
            }

            _isMemoListLoading = true;

            try
            {
                MemoListResponse response = await _memoService.LoadMemosAsync();
                foreach (MemoItem memo in _readOnlyMemos)
                {
                    if (!response.memos.Exists(item => item.id == memo.id))
                    {
                        response.memos.Add(memo);
                    }
                }
                ApplyMemoListResponse(response);
            }
            finally
            {
                _isMemoListLoading = false;
            }
        }

        private async Awaitable<bool> CreateMemoForMapAsync(ScanMapItem map, string kind, string title, string body, string urgency, string assigneePlayerId, string assigneeName, string dueText, List<MemoChecklistEntry> checklistItems, List<MemoVoiceEntry> voiceItems, List<string> imageUrls)
        {
            if (_isCreatingMemo)
            {
                return false;
            }

            _isCreatingMemo = true;

            try
            {
                MemoCreateRequest payload = BuildMemoRequest(map, kind, title, body, urgency, assigneePlayerId, assigneeName, dueText, checklistItems, voiceItems, imageUrls);

                MemoCreateResult result = await _memoService.CreateMemoAsync(payload);
                if (!result.IsSuccess)
                {
                    PopupManager.ShowMessage("메모 생성 실패", "서버에 메모를 저장하지 못했습니다.", "확인");
                    return false;
                }

                MemoListResponse response = result.MemoList;
                if (response.memos.Count == 0)
                {
                    response = await _memoService.LoadMemosAsync();
                }

                ApplyMemoListResponse(response);
                return true;
            }
            finally
            {
                _isCreatingMemo = false;
            }
        }

        private async Awaitable<MemoDetailItem> UpdateMemoForMapAsync(MemoDetailItem item, ScanMapItem map, string kind, string title, string body, string urgency, string assigneePlayerId, string assigneeName, string dueText, List<MemoChecklistEntry> checklistItems, List<MemoVoiceEntry> voiceItems, List<string> imageUrls)
        {
            if (_isCreatingMemo)
            {
                return null;
            }

            _isCreatingMemo = true;

            try
            {
                MemoCreateRequest payload = BuildMemoRequest(map, kind, title, body, urgency, assigneePlayerId, assigneeName, dueText, checklistItems, voiceItems, imageUrls);
                MemoCreateResult result = await _memoService.UpdateMemoAsync(item.Id, payload);
                if (!result.IsSuccess)
                {
                    PopupManager.ShowMessage("메모 수정 실패", "서버에 수정 내용을 저장하지 못했습니다.", "확인");
                    return null;
                }

                MemoListResponse response = result.MemoList;
                if (response.memos.Count == 0)
                {
                    response = await _memoService.LoadMemosAsync();
                }

                ApplyMemoListResponse(response);
                return result.Memo == null ? _memoDetailItems.Find(memo => memo.Id == item.Id) : CreateMemoDetailItem(result.Memo);
            }
            finally
            {
                _isCreatingMemo = false;
            }
        }

        private static MemoCreateRequest BuildMemoRequest(ScanMapItem map, string kind, string title, string body, string urgency, string assigneePlayerId, string assigneeName, string dueText, List<MemoChecklistEntry> checklistItems, List<MemoVoiceEntry> voiceItems, List<string> imageUrls)
        {
            return new MemoCreateRequest
            {
                mapId = map.id,
                locationName = map.spaceName,
                kind = kind,
                urgency = urgency,
                title = title,
                body = body,
                assigneePlayerId = assigneePlayerId,
                assigneeName = assigneeName,
                dueText = dueText,
                checklistItems = checklistItems,
                voiceItems = voiceItems,
                imageUrls = imageUrls
            };
        }

        private void ApplyMemoListResponse(MemoListResponse response)
        {
            _memoDetailItems.Clear();
            foreach (MemoItem memo in response.memos)
            {
                _memoDetailItems.Add(CreateMemoDetailItem(memo));
            }

            RebuildMemoList();
        }

        private static MemoDetailItem CreateMemoDetailItem(MemoItem memo)
        {
            var item = new MemoDetailItem
            {
                Id = memo.id,
                MapId = memo.mapId,
                Kind = ParseMemoKind(memo.kind),
                Urgency = ParseMemoUrgency(memo.urgency),
                Place = GetFirstNonEmpty(memo.locationName, memo.mapName, "3D MAP"),
                Title = GetFirstNonEmpty(memo.title, "새 메모"),
                Body = GetFirstNonEmpty(memo.body, string.Empty),
                AuthorPlayerId = GetFirstNonEmpty(memo.authorPlayerId, string.Empty),
                AssigneePlayerId = GetFirstNonEmpty(memo.assigneePlayerId, string.Empty),
                WorkStatus = GetFirstNonEmpty(memo.workStatus, "active"),
                Location = BuildMemoLocation(memo),
                DueText = GetFirstNonEmpty(memo.dueText, string.Empty),
                Assignee = GetFirstNonEmpty(memo.assigneeName, string.Empty),
                Author = GetFirstNonEmpty(memo.authorName, string.Empty),
                DeletedAt = GetFirstNonEmpty(memo.deletedAt, string.Empty)
            };

            foreach (MemoChecklistEntry checklistItem in memo.checklistItems)
            {
                item.ChecklistItems.Add(new MemoChecklistItem(checklistItem.text, checklistItem.done));
            }

            item.VoiceItems.AddRange(memo.voiceItems);
            item.ImageUrls.AddRange(memo.imageUrls);
            return item;
        }

        private void RebuildMemoList()
        {
            _memoListContainer.Clear();
            bool isEmpty = _memoDetailItems.Count == 0;
            _memoListContainer.EnableInClassList("is-empty", isEmpty);
            _memoListContainer.parent.EnableInClassList("is-memo-list-empty", isEmpty);
            if (isEmpty)
            {
                AddMemoListEmptyState();
                CacheMemoFilterRows();
                return;
            }

            foreach (MemoDetailItem item in _memoDetailItems)
            {
                AddMemoListRow(item);
            }

            CacheMemoFilterRows();
        }

        private void AddMemoListEmptyState()
        {
            VisualElement emptyState = new();
            emptyState.AddToClassList("memo-list-empty-state");

            Label title = new("메모가 없습니다");
            title.AddToClassList("map-empty-title");
            emptyState.Add(title);

            Label subtitle = new("참여 중인 장소에 생성된 메모가 없습니다.");
            subtitle.AddToClassList("map-empty-subtitle");
            emptyState.Add(subtitle);

            _memoListContainer.Add(emptyState);
        }

        private void AddMemoListRow(MemoDetailItem item)
        {
            TemplateContainer template = _memoListItemAsset.Instantiate();
            VisualElement row = template.Q<VisualElement>(className: "memo-list-swipe-row");
            row.userData = item;

            ApplyMemoListRow(template, item);

            VisualElement foreground = template.Q<VisualElement>("memo-list-item-foreground");
            foreground.RegisterCallback<ClickEvent>(evt =>
            {
                if (row.ClassListContains(MEMO_DELETE_OPEN_CLASS))
                {
                    if (row.ClassListContains(MEMO_DELETE_PRESS_CLASS))
                    {
                        row.RemoveFromClassList(MEMO_DELETE_PRESS_CLASS);
                    }
                    else
                    {
                        row.RemoveFromClassList(MEMO_DELETE_OPEN_CLASS);
                    }

                    evt.StopPropagation();
                    return;
                }

                ShowMemoDetailPage(item);
            });
            if (!IsReadOnlyMap(item.MapId))
            {
                RegisterMemoDeleteRow(row);
            }
            _memoListContainer.Add(template);
        }

        private static void ApplyMemoListRow(VisualElement row, MemoDetailItem item)
        {
            VisualElement icon = row.Q<VisualElement>("memo-list-item-icon");
            icon.RemoveFromClassList("memo-list-item-icon-text");
            icon.RemoveFromClassList("memo-list-item-icon-check");
            icon.RemoveFromClassList("memo-list-item-icon-mic");
            icon.RemoveFromClassList("memo-list-item-icon-gallery");
            icon.AddToClassList(GetMemoListIconClass(item.Kind));

            row.Q<Label>("memo-list-item-title").text = item.Title;
            row.Q<Label>("memo-list-item-location-label").text = item.Location;
            row.Q<Label>("memo-list-item-empty-label").text = string.Empty;
            row.Q<Label>("memo-list-item-due-label").text = string.IsNullOrEmpty(item.DueText)
                ? string.Empty
                : $"마감 {item.DueText}";
            row.Q<Label>("memo-list-item-assignee-label").text = item.Assignee;
        }

        private void ShowMemoDetailPage(MemoDetailItem item)
        {
            _currentMemoDetailItem = item;
            bool canManageMemo = CanManageMemo(item) || CanDeleteMemo(item);
            _memoDetailPlaceLabel.text = item.Place;
            _memoDetailContent.Clear();
            HideMemoDetailMenu();
            SetVisible(_memoDetailMenuButton, canManageMemo);
            SetVisible(_memoDetailPage, true);
            ApplyMemoDetailWorkActions(item);
            BuildMemoDetailContent(item);
        }

        private void HideMemoDetailPage()
        {
            _currentMemoDetailItem = null;
            StopMemoVoicePreview();
            HideMemoMediaViewer();
            ClearMemoDetailMediaTextures();
            SetVisible(_memoDetailPage, false);
            HideMemoDetailMenu();
            SetMemoDetailNavMode(false);
        }

        private void ToggleMemoDetailMenu()
        {
            if (_memoDetailMenuButton.ClassListContains(HIDDEN_CLASS))
            {
                return;
            }

            bool shouldShow = _memoDetailMenu.ClassListContains(HIDDEN_CLASS);
            SetVisible(_memoDetailMenu, shouldShow);
            if (shouldShow)
            {
                _memoDetailMenu.BringToFront();
            }
        }

        private void HideMemoDetailMenu()
        {
            SetVisible(_memoDetailMenu, false);
        }

        private bool CanManageMemo(MemoDetailItem item)
        {
            ScanMapItem map = _scanMaps.Find(scanMap => string.Equals(scanMap.id, item.MapId, StringComparison.OrdinalIgnoreCase));
            return string.Equals(map?.currentUserRole, "manager", StringComparison.OrdinalIgnoreCase);
        }

        private bool IsReadOnlyMap(string mapId)
        {
            ScanMapItem map = _scanMaps.Find(item => string.Equals(item.id, mapId, StringComparison.OrdinalIgnoreCase));
            return string.Equals(map?.currentUserRole, "read-only", StringComparison.OrdinalIgnoreCase);
        }

        private bool CanDeleteMemo(MemoDetailItem item)
        {
            if (IsReadOnlyMap(item.MapId))
            {
                return false;
            }
            return string.Equals(item.AuthorPlayerId, AuthenticationService.Instance.PlayerId, StringComparison.OrdinalIgnoreCase);
        }

        private bool CanEditMemo(MemoDetailItem item)
        {
            return CanDeleteMemo(item) || CanManageMemo(item);
        }

        private void ApplyMemoDetailWorkActions(MemoDetailItem item)
        {
            bool isReadOnly = IsReadOnlyMap(item.MapId);
            bool isManager = CanManageMemo(item);
            bool isAssignee = string.Equals(item.AssigneePlayerId, AuthenticationService.Instance.PlayerId, StringComparison.OrdinalIgnoreCase);
            bool isRequested = string.Equals(item.WorkStatus, "completion-requested", StringComparison.OrdinalIgnoreCase);
            bool isCompleted = string.Equals(item.WorkStatus, "completed", StringComparison.OrdinalIgnoreCase);
            bool showManagerActions = !isReadOnly && isManager && isRequested;
            bool showAssigneeAction = !isReadOnly && !isManager && isAssignee && !isCompleted;

            SetVisible(_memoDetailRequestButton, showManagerActions);
            SetVisible(_memoDetailCompleteButton, showManagerActions || showAssigneeAction);
            _memoDetailBottomBar.EnableInClassList("is-single-action", showAssigneeAction);
            if (showManagerActions)
            {
                _memoDetailRequestButton.text = "보완 요청";
                _memoDetailCompleteButton.text = "완료 확정";
            }
            else if (showAssigneeAction)
            {
                _memoDetailCompleteButton.text = isRequested ? "작업 완료 요청 전송 완료" : "작업 완료";
            }

            SetMemoDetailNavMode(showManagerActions || showAssigneeAction);
        }

        private void OnClickMemoDetailRequest()
        {
            if (_currentMemoDetailItem != null)
            {
                _ = SetMemoWorkStatusAsync(_currentMemoDetailItem, "active");
            }
        }

        private void OnClickMemoDetailComplete()
        {
            if (_currentMemoDetailItem == null)
            {
                return;
            }

            bool isManager = CanManageMemo(_currentMemoDetailItem);
            bool isRequested = string.Equals(_currentMemoDetailItem.WorkStatus, "completion-requested", StringComparison.OrdinalIgnoreCase);
            if (!isManager && isRequested)
            {
                PopupManager.ShowConfirm("작업 완료 취소", "작업 완료 요청을 취소할까요?", "아니요", "취소하기",
                    () => _ = SetMemoWorkStatusAsync(_currentMemoDetailItem, "active"));
                return;
            }

            _ = SetMemoWorkStatusAsync(_currentMemoDetailItem, isManager ? "completed" : "completion-requested");
        }

        private async Awaitable SetMemoWorkStatusAsync(MemoDetailItem item, string status)
        {
            SetMemoServerWaiting(true);
            try
            {
                MemoListResponse response = await _memoService.SetMemoWorkStatusAsync(item.Id, status);
                ApplyMemoListResponse(response);
                MemoDetailItem updatedItem = _memoDetailItems.Find(memo => memo.Id == item.Id);
                if (updatedItem != null)
                {
                    ShowMemoDetailPage(updatedItem);
                }
            }
            finally
            {
                SetMemoServerWaiting(false);
            }
        }

        private void ShowCurrentMemoEditPage()
        {
            HideMemoDetailMenu();
            if (_currentMemoDetailItem == null)
            {
                return;
            }

            if (!CanEditMemo(_currentMemoDetailItem))
            {
                PopupManager.ShowMessage("수정할 수 없음", "메모 작성자 또는 맵 관리자만 수정할 수 있습니다.", "확인");
                return;
            }

            ShowMemoEditPage(_currentMemoDetailItem);
        }

        private void ShowCurrentMemoDeleteConfirm()
        {
            HideMemoDetailMenu();
            if (_currentMemoDetailItem == null)
            {
                return;
            }

            ShowMemoDeleteConfirm(_currentMemoDetailItem);
        }

        private void ShareCurrentMemo()
        {
            HideMemoDetailMenu();
            if (_currentMemoDetailItem == null)
            {
                return;
            }

            string subject = $"MemoAnchor - {_currentMemoDetailItem.Title}";
            string shareText = BuildMemoShareText(_currentMemoDetailItem);
            if (!NativeShareService.ShareText(subject, shareText))
            {
                PopupManager.ShowMessage("공유 내용 복사", "공유할 메모 내용을 클립보드에 복사했습니다.", "확인");
            }
        }

        private static string BuildMemoShareText(MemoDetailItem item)
        {
            StringBuilder builder = new();
            builder.AppendLine("[MemoAnchor 메모]");
            builder.AppendLine();
            builder.AppendLine("제목");
            builder.AppendLine(item.Title);

            if (item.Kind == MemoDetailKind.Checklist)
            {
                builder.AppendLine();
                builder.AppendLine("체크리스트");
                foreach (MemoChecklistItem checklistItem in item.ChecklistItems)
                {
                    builder.Append(checklistItem.Done ? "☑ " : "☐ ");
                    builder.AppendLine(checklistItem.Text);
                }
            }
            else if (!string.IsNullOrWhiteSpace(item.Body))
            {
                builder.AppendLine();
                builder.AppendLine("내용");
                builder.AppendLine(item.Body);
            }

            if (item.VoiceItems.Count > 0 || item.ImageUrls.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("첨부");
                if (item.VoiceItems.Count > 0)
                {
                    builder.AppendLine($"• 음성 메모 {item.VoiceItems.Count}개");
                }

                if (item.ImageUrls.Count > 0)
                {
                    builder.AppendLine($"• 사진 / 동영상 {item.ImageUrls.Count}개");
                }
            }

            builder.AppendLine();
            builder.AppendLine("────────────");
            builder.AppendLine("상세 정보");
            AppendMemoShareField(builder, "위치", item.Location);
            AppendMemoShareField(builder, "마감", item.DueText);
            AppendMemoShareField(builder, "긴급도", GetMemoUrgencyShareText(item.Urgency));
            AppendMemoShareField(builder, "수리자", item.Assignee);
            AppendMemoShareField(builder, "작성자", item.Author);
            return builder.ToString().TrimEnd();
        }

        private static void AppendMemoShareField(StringBuilder builder, string label, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                builder.Append("• ").Append(label).Append(": ").AppendLine(value);
            }
        }

        private static string GetMemoUrgencyShareText(MemoUrgency urgency)
        {
            return urgency switch
            {
                MemoUrgency.High => "높음",
                MemoUrgency.Low => "낮음",
                _ => "보통"
            };
        }

        private void ShowMemoDeleteConfirm(MemoDetailItem item)
        {
            if (!CanDeleteMemo(item))
            {
                PopupManager.ShowMessage("삭제할 수 없음", "자신이 작성한 메모만 삭제할 수 있습니다.", "확인");
                return;
            }

            PopupManager.ShowConfirm("메모 삭제", "이 메모를 휴지통으로 이동할까요?", "취소", "삭제", () => _ = MoveMemoToTrashAsync(item));
        }

        private async Awaitable MoveMemoToTrashAsync(MemoDetailItem item)
        {
            SetMemoServerWaiting(true);

            try
            {
                MemoListResponse response = await _memoService.MoveMemoToTrashAsync(item.Id);
                ApplyMemoListResponse(response);
                HideMemoDetailPage();
            }
            finally
            {
                SetMemoServerWaiting(false);
            }
        }

        private void ShowMemoTrashPage()
        {
            SetVisible(_memoTrashPage, true);
            SetMemoFilterNavMode(false);
            SetMemoTrashNavMode(true, false);
            _ = RefreshMemoTrashAsync();
        }

        private void HideMemoTrashPage()
        {
            SetMemoTrashSelectMode(false);
            SetVisible(_memoTrashPage, false);
            SetMemoTrashNavMode(false, false);
        }

        private void ToggleMemoTrashSelectMode()
        {
            SetMemoTrashSelectMode(!_isMemoTrashSelecting);
        }

        private void SetMemoTrashSelectMode(bool selecting)
        {
            _isMemoTrashSelecting = selecting;
            _memoTrashSelectButton.text = selecting ? "취소" : "선택";
            _memoTrashPage.EnableInClassList("is-trash-selecting", selecting);
            if (!selecting)
            {
                _selectedTrashMemoIds.Clear();
            }

            _memoTrashListContainer.Query<VisualElement>(className: "memo-list-swipe-row").ForEach(row =>
            {
                row.RemoveFromClassList(MEMO_DELETE_OPEN_CLASS);
                row.EnableInClassList("is-trash-selected", row.userData is MemoDetailItem item && _selectedTrashMemoIds.Contains(item.Id));
            });
            RefreshMemoTrashActionBar();
        }

        private void RefreshMemoTrashActionBar()
        {
            SetMemoTrashNavMode(_memoTrashPage != null && !_memoTrashPage.ClassListContains(HIDDEN_CLASS), _selectedTrashMemoIds.Count > 0);
        }

        private async Awaitable RefreshMemoTrashAsync()
        {
            if (_isMemoTrashLoading)
            {
                return;
            }

            _isMemoTrashLoading = true;
            SetMemoServerWaiting(true);

            try
            {
                MemoListResponse response = await _memoService.LoadTrashedMemosAsync();
                ApplyMemoTrashResponse(response);
            }
            finally
            {
                _isMemoTrashLoading = false;
                SetMemoServerWaiting(false);
            }
        }

        private void ApplyMemoTrashResponse(MemoListResponse response)
        {
            _memoTrashItems.Clear();
            foreach (MemoItem memo in response.memos)
            {
                _memoTrashItems.Add(CreateMemoDetailItem(memo));
            }

            RebuildMemoTrashList();
        }

        private void RebuildMemoTrashList()
        {
            _memoTrashListContainer.Clear();
            SetMemoTrashSelectMode(false);
            _memoTrashListContainer.EnableInClassList("is-empty", _memoTrashItems.Count == 0);
            _memoTrashListContainer.parent.EnableInClassList("is-memo-list-empty", _memoTrashItems.Count == 0);

            if (_memoTrashItems.Count == 0)
            {
                AddMemoTrashEmptyState();
                return;
            }

            foreach (MemoDetailItem item in _memoTrashItems)
            {
                AddMemoTrashRow(item);
            }
        }

        private void AddMemoTrashEmptyState()
        {
            VisualElement emptyState = new();
            emptyState.AddToClassList("memo-list-empty-state");

            Label title = new("휴지통이 비어 있습니다");
            title.AddToClassList("map-empty-title");
            emptyState.Add(title);

            Label subtitle = new("삭제한 메모가 없습니다.");
            subtitle.AddToClassList("map-empty-subtitle");
            emptyState.Add(subtitle);

            _memoTrashListContainer.Add(emptyState);
        }

        private void AddMemoTrashRow(MemoDetailItem item)
        {
            TemplateContainer template = _memoListItemAsset.Instantiate();
            VisualElement row = template.Q<VisualElement>(className: "memo-list-swipe-row");
            row.userData = item;
            ApplyMemoListRow(template, item);

            VisualElement foreground = template.Q<VisualElement>("memo-list-item-foreground");
            foreground.RegisterCallback<ClickEvent>(evt =>
            {
                if (_isMemoTrashSelecting)
                {
                    ToggleTrashMemoSelection(row, item);
                    evt.StopPropagation();
                    return;
                }

                PopupManager.ShowConfirm("메모 복구", "이 메모를 다시 지도에 표시할까요?", "취소", "복구", () => _ = RestoreMemoAsync(item));
            });

            row.EnableInClassList("is-trash-selected", _selectedTrashMemoIds.Contains(item.Id));

            RegisterMemoDeleteRow(row, trashItem =>
            {
                PopupManager.ShowConfirm("영구 삭제", "휴지통에서 완전히 삭제할까요?", "취소", "삭제", () => _ = DeleteMemoPermanentlyAsync(trashItem));
            });

            _memoTrashListContainer.Add(template);
        }

        private async Awaitable RestoreMemoAsync(MemoDetailItem item)
        {
            SetMemoServerWaiting(true);

            try
            {
                MemoListResponse response = await _memoService.RestoreMemoAsync(item.Id);
                _selectedTrashMemoIds.Remove(item.Id);
                ApplyMemoTrashResponse(response);
                await RefreshMemoListAsync();
            }
            finally
            {
                SetMemoServerWaiting(false);
            }
        }

        private async Awaitable DeleteMemoPermanentlyAsync(MemoDetailItem item)
        {
            SetMemoServerWaiting(true);

            try
            {
                MemoListResponse response = await _memoService.DeleteMemoPermanentlyAsync(item.Id);
                _selectedTrashMemoIds.Remove(item.Id);
                ApplyMemoTrashResponse(response);
            }
            finally
            {
                SetMemoServerWaiting(false);
            }
        }

        private void ToggleTrashMemoSelection(VisualElement row, MemoDetailItem item)
        {
            if (!_isMemoTrashSelecting)
            {
                return;
            }

            if (!_selectedTrashMemoIds.Add(item.Id))
            {
                _selectedTrashMemoIds.Remove(item.Id);
            }

            row.EnableInClassList("is-trash-selected", _selectedTrashMemoIds.Contains(item.Id));
            RefreshMemoTrashActionBar();
        }

        private void ShowSelectedTrashRestoreConfirm()
        {
            if (_selectedTrashMemoIds.Count == 0)
            {
                return;
            }

            PopupManager.ShowConfirm("메모 복구", "선택한 메모를 다시 지도에 표시할까요?", "취소", "복구", () => _ = RestoreSelectedTrashMemosAsync());
        }

        private void ShowSelectedTrashDeleteConfirm()
        {
            if (_selectedTrashMemoIds.Count == 0)
            {
                return;
            }

            PopupManager.ShowConfirm("영구 삭제", "선택한 메모를 휴지통에서 완전히 삭제할까요?", "취소", "삭제", () => _ = DeleteSelectedTrashMemosAsync());
        }

        private async Awaitable RestoreSelectedTrashMemosAsync()
        {
            string[] memoIds = new string[_selectedTrashMemoIds.Count];
            _selectedTrashMemoIds.CopyTo(memoIds);
            MemoListResponse response = null;

            SetMemoServerWaiting(true);

            try
            {
                foreach (string memoId in memoIds)
                {
                    response = await _memoService.RestoreMemoAsync(memoId);
                }

                _selectedTrashMemoIds.Clear();
                ApplyMemoTrashResponse(response ?? new MemoListResponse());
                await RefreshMemoListAsync();
            }
            finally
            {
                SetMemoServerWaiting(false);
            }
        }

        private async Awaitable DeleteSelectedTrashMemosAsync()
        {
            string[] memoIds = new string[_selectedTrashMemoIds.Count];
            _selectedTrashMemoIds.CopyTo(memoIds);
            MemoListResponse response = null;

            SetMemoServerWaiting(true);

            try
            {
                foreach (string memoId in memoIds)
                {
                    response = await _memoService.DeleteMemoPermanentlyAsync(memoId);
                }

                _selectedTrashMemoIds.Clear();
                ApplyMemoTrashResponse(response ?? new MemoListResponse());
            }
            finally
            {
                SetMemoServerWaiting(false);
            }
        }

        private void SetMemoServerWaiting(bool isWaiting)
        {
            if (isWaiting)
            {
                LoadingSpinnerController.ShowOverlay(_memoLoadingOverlay, _memoLoadingSpinner);
            }
            else
            {
                LoadingSpinnerController.HideOverlay(_memoLoadingOverlay, _memoLoadingSpinner);
            }
        }

        private void BuildMemoDetailContent(MemoDetailItem item)
        {
            ClearMemoDetailMediaTextures();
            VisualElement preview = new();
            preview.AddToClassList("memo-preview-box");
            _memoDetailContent.Add(preview);

            VisualElement titleCard = new();
            titleCard.AddToClassList("memo-card");
            titleCard.AddToClassList("memo-title-card");
            Label title = new(item.Title);
            title.AddToClassList("memo-card-title");
            VisualElement badge = new();
            badge.AddToClassList("memo-badge");
            badge.AddToClassList(GetMemoBadgeClass(item.Urgency));
            titleCard.Add(title);
            titleCard.Add(badge);
            _memoDetailContent.Add(titleCard);

            VisualElement bodyCard = new();
            bodyCard.AddToClassList("memo-card");
            bodyCard.AddToClassList("memo-body-card");
            bodyCard.AddToClassList(GetMemoBodyKindClass(item.Kind));
            _memoDetailContent.Add(bodyCard);

            if (item.Kind == MemoDetailKind.Text)
            {
                Label body = new(item.Body);
                body.AddToClassList("memo-body-text");
                bodyCard.Add(body);
            }
            else if (item.Kind == MemoDetailKind.Checklist)
            {
                foreach (MemoChecklistItem checklistItem in item.ChecklistItems)
                {
                    bodyCard.Add(CreateMemoChecklistRow(checklistItem));
                }
            }
            else if (item.Kind == MemoDetailKind.Voice)
            {
                foreach (MemoVoiceEntry voiceItem in item.VoiceItems)
                {
                    bodyCard.Add(CreateMemoVoiceRow(voiceItem));
                }
            }
            else if (item.Kind == MemoDetailKind.Image)
            {
                if (!string.IsNullOrWhiteSpace(item.Body))
                {
                    Label body = new(item.Body);
                    body.AddToClassList("memo-body-text");
                    bodyCard.Add(body);
                }

                VisualElement gallery = new();
                gallery.AddToClassList("wrap-row");
                foreach (string imageUrl in item.ImageUrls)
                {
                    TemplateContainer template = _memoCreateMediaItemAsset.Instantiate();
                    VisualElement mediaItem = template.Q<VisualElement>("memo-create-media-item");
                    VisualElement mediaPreview = template.Q<VisualElement>("memo-create-media-preview");
                    VisualElement mediaSpinner = template.Q<VisualElement>("memo-create-media-spinner");
                    Label videoLabel = template.Q<Label>("memo-create-media-video-label");
                    Button removeButton = template.Q<Button>("memo-create-media-remove");
                    bool isVideo = IsVideoMediaPath(imageUrl);
                    SetVisible(videoLabel, isVideo);
                    SetVisible(removeButton, false);
                    mediaItem.RegisterCallback<ClickEvent>(_ =>
                    {
                        ShowMemoMediaViewer(imageUrl, isVideo);
                    });
                    if (!isVideo)
                    {
                        SetVisible(mediaPreview, false);
                        SetVisible(mediaSpinner, true);
                        _memoDetailMediaSpinners.Add(mediaSpinner);
                        LoadingSpinnerController.Start(mediaSpinner);
                        _ = LoadMemoDetailMediaPreviewAsync(mediaPreview, mediaSpinner, imageUrl);
                    }

                    gallery.Add(mediaItem);
                }

                bodyCard.Add(gallery);
            }

            AddMemoDetailFooter(bodyCard, item);
        }

        private async Awaitable LoadMemoDetailMediaPreviewAsync(VisualElement preview, VisualElement spinner, string imageUrl)
        {
            using UnityWebRequest request = UnityWebRequestTexture.GetTexture(GetMemoMediaUrl(imageUrl));
            await ServicesManager.SendRequestAsync(request);
            if (request.result != UnityWebRequest.Result.Success)
            {
                StopMemoDetailMediaSpinner(spinner);
                SetVisible(preview, true);
                return;
            }

            Texture2D texture = DownloadHandlerTexture.GetContent(request);
            if (preview.panel == null)
            {
                Destroy(texture);
                StopMemoDetailMediaSpinner(spinner);
                return;
            }

            _memoDetailMediaTextures.Add(texture);
            preview.style.backgroundImage = new StyleBackground(texture);
            preview.AddToClassList("has-preview");
            StopMemoDetailMediaSpinner(spinner);
            SetVisible(preview, true);
        }

        private void StopMemoDetailMediaSpinner(VisualElement spinner)
        {
            LoadingSpinnerController.Stop(spinner);
            SetVisible(spinner, false);
            _memoDetailMediaSpinners.Remove(spinner);
        }

        private void ShowMemoMediaViewer(string mediaUrl, bool isVideo)
        {
            if (isVideo)
            {
                _ = LoadMemoMediaViewerVideoAsync(mediaUrl);
            }
            else
            {
                _ = LoadMemoMediaViewerImageAsync(mediaUrl);
            }
        }

        private async Awaitable LoadMemoMediaViewerImageAsync(string imageUrl)
        {
            _memoMediaViewerLoadToken++;
            int token = _memoMediaViewerLoadToken;
            StopMemoMediaViewerVideo();
            ClearMemoMediaViewerTexture();
            ResetMemoMediaViewerTransform();
            _memoMediaViewerImage.image = null;
            SetVisible(_memoMediaViewerImage, false);
            StartMemoMediaViewerSpinner();
            SetVisible(_memoMediaViewerRotateButton, true);
            SetVisible(_memoMediaViewerVideoControls, false);
            SetVisible(_memoMediaViewerOverlay, true);
            _memoMediaViewerOverlay.BringToFront();

            using UnityWebRequest request = UnityWebRequestTexture.GetTexture(GetMemoMediaUrl(imageUrl));
            await ServicesManager.SendRequestAsync(request);
            if (token != _memoMediaViewerLoadToken)
            {
                return;
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                HideMemoMediaViewer();
                PopupManager.ShowMessage("사진 불러오기 실패", "사진을 불러오지 못했습니다.", "확인");
                return;
            }

            _memoMediaViewerTexture = DownloadHandlerTexture.GetContent(request);
            _memoMediaViewerImage.image = _memoMediaViewerTexture;
            StopMemoMediaViewerSpinner();
            SetVisible(_memoMediaViewerImage, true);
        }

        private async Awaitable LoadMemoMediaViewerVideoAsync(string videoUrl)
        {
            _memoMediaViewerLoadToken++;
            int token = _memoMediaViewerLoadToken;
            ClearMemoMediaViewerTexture();
            StopMemoMediaViewerVideo();
            ResetMemoMediaViewerTransform();
            _memoMediaViewerImage.image = null;
            SetVisible(_memoMediaViewerImage, false);
            StartMemoMediaViewerSpinner();
            SetVisible(_memoMediaViewerRotateButton, false);
            SetVisible(_memoMediaViewerVideoControls, false);
            SetVisible(_memoMediaViewerOverlay, true);
            _memoMediaViewerOverlay.BringToFront();

            _memoMediaViewerVideoError = string.Empty;
            _memoMediaViewerVideoPlayer.source = VideoSource.Url;
            _memoMediaViewerVideoPlayer.url = GetMemoMediaUrl(videoUrl);
            _memoMediaViewerVideoPlayer.Prepare();
            float prepareStartedAt = Time.realtimeSinceStartup;
            while (token == _memoMediaViewerLoadToken
                && !_memoMediaViewerVideoPlayer.isPrepared
                && string.IsNullOrEmpty(_memoMediaViewerVideoError)
                && Time.realtimeSinceStartup - prepareStartedAt < 30f)
            {
                await Awaitable.NextFrameAsync();
            }

            if (token != _memoMediaViewerLoadToken)
            {
                return;
            }

            if (!_memoMediaViewerVideoPlayer.isPrepared)
            {
                HideMemoMediaViewer();
                PopupManager.ShowMessage("동영상 불러오기 실패", "동영상을 재생하지 못했습니다.", "확인");
                return;
            }

            int sourceWidth = _memoMediaViewerVideoPlayer.width > 0 ? (int)_memoMediaViewerVideoPlayer.width : Screen.width;
            int sourceHeight = _memoMediaViewerVideoPlayer.height > 0 ? (int)_memoMediaViewerVideoPlayer.height : Screen.height;
            float renderScale = Mathf.Min(1f, 1920f / Mathf.Max(sourceWidth, sourceHeight));
            int renderWidth = Mathf.Max(2, Mathf.RoundToInt(sourceWidth * renderScale));
            int renderHeight = Mathf.Max(2, Mathf.RoundToInt(sourceHeight * renderScale));
            _memoMediaViewerVideoTexture = new RenderTexture(renderWidth, renderHeight, 0, RenderTextureFormat.ARGB32);
            _memoMediaViewerVideoTexture.Create();
            _memoMediaViewerVideoPlayer.targetTexture = _memoMediaViewerVideoTexture;
            _memoMediaViewerImage.image = _memoMediaViewerVideoTexture;
            StopMemoMediaViewerSpinner();
            SetVisible(_memoMediaViewerImage, true);
            _memoMediaViewerVideoPlayer.Play();
            SetVisible(_memoMediaViewerVideoControls, true);
            UpdateMemoMediaViewerControls();
            _memoMediaViewerControlsSchedule.Resume();
        }

        private void HideMemoMediaViewer()
        {
            _memoMediaViewerLoadToken++;
            StopMemoMediaViewerSpinner();
            ClearMemoMediaViewerTexture();
            StopMemoMediaViewerVideo();
            ResetMemoMediaViewerTransform();
            _memoMediaViewerImage.image = null;
            SetVisible(_memoMediaViewerVideoControls, false);
            SetVisible(_memoMediaViewerOverlay, false);
        }

        private void StartMemoMediaViewerSpinner()
        {
            SetVisible(_memoMediaViewerSpinner, true);
            LoadingSpinnerController.Start(_memoMediaViewerSpinner);
        }

        private void StopMemoMediaViewerSpinner()
        {
            LoadingSpinnerController.Stop(_memoMediaViewerSpinner);
            SetVisible(_memoMediaViewerSpinner, false);
        }

        private void ClearMemoMediaViewerTexture()
        {
            if (_memoMediaViewerTexture != null)
            {
                Destroy(_memoMediaViewerTexture);
                _memoMediaViewerTexture = null;
            }
        }

        private void StopMemoMediaViewerVideo()
        {
            _memoMediaViewerControlsSchedule.Pause();
            _memoMediaViewerVideoPlayer.Stop();
            _memoMediaViewerPlayIcon.RemoveFromClassList("is-playing");
            _memoMediaViewerVideoPlayer.targetTexture = null;
            _memoMediaViewerVideoPlayer.url = string.Empty;
            if (_memoMediaViewerVideoTexture != null)
            {
                _memoMediaViewerVideoTexture.Release();
                Destroy(_memoMediaViewerVideoTexture);
                _memoMediaViewerVideoTexture = null;
            }
        }

        private void ToggleMemoMediaViewerVideoPlayback()
        {
            if (!_memoMediaViewerVideoPlayer.isPrepared)
            {
                return;
            }

            if (_memoMediaViewerVideoPlayer.isPlaying)
            {
                _memoMediaViewerVideoPlayer.Pause();
            }
            else
            {
                if (_memoMediaViewerVideoPlayer.length > 0
                    && _memoMediaViewerVideoPlayer.time >= _memoMediaViewerVideoPlayer.length - 0.05d)
                {
                    _memoMediaViewerVideoPlayer.time = 0d;
                }

                _memoMediaViewerVideoPlayer.Play();
            }

            UpdateMemoMediaViewerControls();
        }

        private void OnMemoMediaViewerSeekChanged(ChangeEvent<float> evt)
        {
            if (_memoMediaViewerVideoPlayer.isPrepared)
            {
                _memoMediaViewerVideoPlayer.time = evt.newValue;
                UpdateMemoMediaViewerControls();
            }
        }

        private void UpdateMemoMediaViewerControls()
        {
            if (!_memoMediaViewerVideoPlayer.isPrepared)
            {
                return;
            }

            float duration = (float)_memoMediaViewerVideoPlayer.length;
            float currentTime = Mathf.Clamp((float)_memoMediaViewerVideoPlayer.time, 0f, duration);
            UpdateMemoPlaybackSlider(_memoMediaViewerSeekSlider, currentTime, duration);
            _memoMediaViewerPlayIcon.EnableInClassList("is-playing", _memoMediaViewerVideoPlayer.isPlaying);
            _memoMediaViewerTimeLabel.text = $"{FormatMemoPlaybackTime(currentTime)} / {FormatMemoPlaybackTime(duration)}";
        }

        private static void UpdateMemoPlaybackSlider(Slider slider, float currentTime, float duration)
        {
            slider.highValue = Mathf.Max(0.01f, duration);
            slider.SetValueWithoutNotify(Mathf.Clamp(currentTime, 0f, duration));
        }

        private static string FormatMemoPlaybackTime(float seconds)
        {
            int totalSeconds = Mathf.Max(0, Mathf.FloorToInt(seconds));
            int hours = totalSeconds / 3600;
            int minutes = totalSeconds % 3600 / 60;
            int remainingSeconds = totalSeconds % 60;
            return hours > 0
                ? $"{hours:00}:{minutes:00}:{remainingSeconds:00}"
                : $"{minutes:00}:{remainingSeconds:00}";
        }

        private void OnMemoMediaViewerVideoError(VideoPlayer source, string message)
        {
            _memoMediaViewerVideoError = message;
        }

        private void OnClickMemoMediaViewerOverlay(ClickEvent evt)
        {
            HideMemoMediaViewer();
        }

        private static void OnClickMemoMediaViewerPanel(ClickEvent evt)
        {
            evt.StopPropagation();
        }

        private void RotateMemoMediaViewer()
        {
            _memoMediaViewerRotation = (_memoMediaViewerRotation + 90) % 360;
            ApplyMemoMediaViewerTransform();
        }

        private void OnMemoMediaViewerPointerDown(PointerDownEvent evt)
        {
            if (_memoMediaViewerFirstPointerId < 0)
            {
                _memoMediaViewerFirstPointerId = evt.pointerId;
                _memoMediaViewerFirstPointerPosition = evt.position;
                return;
            }

            if (_memoMediaViewerSecondPointerId < 0 && evt.pointerId != _memoMediaViewerFirstPointerId)
            {
                _memoMediaViewerSecondPointerId = evt.pointerId;
                _memoMediaViewerSecondPointerPosition = evt.position;
                _memoMediaViewerPinchStartDistance = Vector2.Distance(_memoMediaViewerFirstPointerPosition, _memoMediaViewerSecondPointerPosition);
                _memoMediaViewerPinchStartZoom = _memoMediaViewerZoom;
            }
        }

        private void OnMemoMediaViewerPointerMove(PointerMoveEvent evt)
        {
            if (evt.pointerId == _memoMediaViewerFirstPointerId)
            {
                _memoMediaViewerFirstPointerPosition = evt.position;
            }
            else if (evt.pointerId == _memoMediaViewerSecondPointerId)
            {
                _memoMediaViewerSecondPointerPosition = evt.position;
            }
            else
            {
                return;
            }

            if (_memoMediaViewerSecondPointerId < 0 || _memoMediaViewerPinchStartDistance <= 0f)
            {
                return;
            }

            float distance = Vector2.Distance(_memoMediaViewerFirstPointerPosition, _memoMediaViewerSecondPointerPosition);
            _memoMediaViewerZoom = Mathf.Clamp(_memoMediaViewerPinchStartZoom * distance / _memoMediaViewerPinchStartDistance, 1f, 4f);
            ApplyMemoMediaViewerTransform();
        }

        private void OnMemoMediaViewerPointerUp(PointerUpEvent evt)
        {
            RemoveMemoMediaViewerPointer(evt.pointerId);
        }

        private void OnMemoMediaViewerPointerCancel(PointerCancelEvent evt)
        {
            RemoveMemoMediaViewerPointer(evt.pointerId);
        }

        private void RemoveMemoMediaViewerPointer(int pointerId)
        {
            if (pointerId == _memoMediaViewerFirstPointerId)
            {
                _memoMediaViewerFirstPointerId = _memoMediaViewerSecondPointerId;
                _memoMediaViewerFirstPointerPosition = _memoMediaViewerSecondPointerPosition;
                _memoMediaViewerSecondPointerId = -1;
            }
            else if (pointerId == _memoMediaViewerSecondPointerId)
            {
                _memoMediaViewerSecondPointerId = -1;
            }

            _memoMediaViewerPinchStartDistance = 0f;
            _memoMediaViewerPinchStartZoom = _memoMediaViewerZoom;
        }

        private void ResetMemoMediaViewerTransform()
        {
            _memoMediaViewerRotation = 0;
            _memoMediaViewerZoom = 1f;
            _memoMediaViewerPinchStartDistance = 0f;
            _memoMediaViewerPinchStartZoom = 1f;
            _memoMediaViewerFirstPointerId = -1;
            _memoMediaViewerSecondPointerId = -1;
            ApplyMemoMediaViewerTransform();
        }

        private void ApplyMemoMediaViewerTransform()
        {
            float width = _memoMediaViewerImage.resolvedStyle.width;
            float height = _memoMediaViewerImage.resolvedStyle.height;
            bool isQuarterTurn = _memoMediaViewerRotation == 90 || _memoMediaViewerRotation == 270;
            float rotationFitScale = isQuarterTurn && width > 0f && height > 0f
                ? Mathf.Min(width / height, height / width)
                : 1f;
            float scale = _memoMediaViewerZoom * rotationFitScale;
            _memoMediaViewerImage.style.scale = new StyleScale(new Scale(new Vector3(scale, scale, 1f)));
            _memoMediaViewerImage.style.rotate = new Rotate(new Angle(_memoMediaViewerRotation));
        }

        private void ClearMemoDetailMediaTextures()
        {
            foreach (VisualElement spinner in _memoDetailMediaSpinners)
            {
                LoadingSpinnerController.Stop(spinner);
            }

            _memoDetailMediaSpinners.Clear();
            foreach (Texture2D texture in _memoDetailMediaTextures)
            {
                Destroy(texture);
            }

            _memoDetailMediaTextures.Clear();
        }

        private static string GetMemoMediaUrl(string imageUrl)
        {
            return imageUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || imageUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                ? imageUrl
                : ServicesManager.BuildServerUrl(imageUrl);
        }

        private static VisualElement CreateMemoChecklistRow(MemoChecklistItem item)
        {
            VisualElement row = new();
            row.AddToClassList("memo-check-row");

            Label label = new(item.Text);
            label.AddToClassList("memo-check-text");
            Toggle toggle = new();
            toggle.AddToClassList("memo-check-toggle");
            toggle.SetValueWithoutNotify(item.Done);

            row.Add(label);
            row.Add(toggle);
            return row;
        }

        private VisualElement CreateMemoVoiceRow(MemoVoiceEntry voiceItem)
        {
            TemplateContainer template = _memoVoiceItemAsset.Instantiate();
            VisualElement row = template.Q<VisualElement>("memo-voice-item");
            row.AddToClassList("is-detail");
            TextField nameInput = template.Q<TextField>("memo-voice-item-name");
            Button playButton = template.Q<Button>("memo-voice-item-play-button");
            Button removeButton = template.Q<Button>("memo-voice-item-remove-button");
            nameInput.SetValueWithoutNotify(voiceItem.name);
            nameInput.textEdition.isReadOnly = true;
            nameInput.pickingMode = PickingMode.Ignore;
            SetVisible(removeButton, false);
            playButton.SetEnabled(!string.IsNullOrWhiteSpace(voiceItem.url));
            ConfigureMemoVoicePreview(template, voiceItem.url, true, playButton);
            return row;
        }

        private static void AddMemoDetailFooter(VisualElement bodyCard, MemoDetailItem item)
        {
            VisualElement footer = new();
            footer.AddToClassList("memo-card-footer");
            footer.Add(CreateMemoDetailFooterItem("\uC791\uC131\uC790", item.Author));
            footer.Add(CreateMemoDetailFooterItem("\uC218\uB9AC\uC790", item.Assignee));
            footer.Add(CreateMemoDetailFooterItem("\uB9C8\uAC10", item.DueText));
            bodyCard.Add(footer);
        }

        private static VisualElement CreateMemoDetailFooterItem(string prefix, string text)
        {
            VisualElement item = new();
            item.AddToClassList("memo-card-footer-item");

            Label prefixLabel = new(prefix);
            prefixLabel.AddToClassList("memo-card-footer-label");
            prefixLabel.AddToClassList("memo-card-footer-prefix");
            item.Add(prefixLabel);

            if (!string.IsNullOrWhiteSpace(text))
            {
                Label valueLabel = new(text);
                valueLabel.AddToClassList("memo-card-footer-label");
                item.Add(valueLabel);
            }

            return item;
        }

        private static string GetMemoListIconClass(MemoDetailKind kind)
        {
            if (kind == MemoDetailKind.Checklist)
            {
                return "memo-list-item-icon-check";
            }

            if (kind == MemoDetailKind.Voice)
            {
                return "memo-list-item-icon-mic";
            }

            if (kind == MemoDetailKind.Image)
            {
                return "memo-list-item-icon-gallery";
            }

            return "memo-list-item-icon-text";
        }

        private static string GetMemoBadgeClass(MemoUrgency urgency)
        {
            if (urgency == MemoUrgency.High)
            {
                return "memo-badge-high";
            }

            if (urgency == MemoUrgency.Low)
            {
                return "memo-badge-low";
            }

            return "memo-badge-middle";
        }

        private static string GetMemoBodyKindClass(MemoDetailKind kind)
        {
            if (kind == MemoDetailKind.Checklist)
            {
                return "memo-body-checklist";
            }

            if (kind == MemoDetailKind.Voice)
            {
                return "memo-body-voice";
            }

            if (kind == MemoDetailKind.Image)
            {
                return "memo-body-image";
            }

            return "memo-body-text-kind";
        }

        private static MemoDetailKind ParseMemoKind(string kind)
        {
            string normalizedKind = kind?.Trim().ToLowerInvariant();
            if (normalizedKind == "checklist")
            {
                return MemoDetailKind.Checklist;
            }

            if (normalizedKind == "voice" || normalizedKind == "voicememo")
            {
                return MemoDetailKind.Voice;
            }

            if (normalizedKind == "image" || normalizedKind == "gallery")
            {
                return MemoDetailKind.Image;
            }

            return MemoDetailKind.Text;
        }

        private static MemoUrgency ParseMemoUrgency(string urgency)
        {
            string normalizedUrgency = urgency?.Trim().ToLowerInvariant();
            if (normalizedUrgency == "high" || normalizedUrgency == "1")
            {
                return MemoUrgency.High;
            }

            if (normalizedUrgency == "low" || normalizedUrgency == "3")
            {
                return MemoUrgency.Low;
            }

            return MemoUrgency.Middle;
        }

        private static string BuildMemoLocation(MemoItem memo)
        {
            string address = GetFirstNonEmpty(memo.address, string.Empty);
            string place = GetFirstNonEmpty(memo.locationName, memo.mapName, string.Empty);
            if (string.IsNullOrWhiteSpace(address))
            {
                return place;
            }

            if (string.IsNullOrWhiteSpace(place))
            {
                return address;
            }

            return $"{address} - {place}";
        }

        private static string GetFirstNonEmpty(params string[] values)
        {
            foreach (string value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return string.Empty;
        }

        private enum MemoDetailKind
        {
            Text,
            Checklist,
            Voice,
            Image
        }

        private enum MemoUrgency
        {
            High,
            Middle,
            Low
        }

        private class MemoDetailItem
        {
            public string Id;
            public string MapId;
            public MemoDetailKind Kind;
            public MemoUrgency Urgency;
            public string Place;
            public string Title;
            public string Body;
            public string AuthorPlayerId;
            public string AssigneePlayerId;
            public string WorkStatus;
            public string Location;
            public string DueText;
            public string Assignee;
            public string Author;
            public string DeletedAt;
            public List<MemoChecklistItem> ChecklistItems = new();
            public List<MemoVoiceEntry> VoiceItems = new();
            public List<string> ImageUrls = new();
        }

        private class MemoChecklistItem
        {
            public MemoChecklistItem(string text, bool done)
            {
                Text = text;
                Done = done;
            }

            public string Text;
            public bool Done;
        }
    }
}
