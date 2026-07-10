using System.Collections.Generic;
using System;
using System.Text;
using Unity.Services.Authentication;
using UnityEngine;
using UnityEngine.UIElements;

namespace MemoAnchor.UI
{
    public partial class MainView
    {
        [SerializeField] private VisualTreeAsset _memoListItemAsset;

        private readonly MemoService _memoService = new();
        private readonly List<MemoDetailItem> _memoDetailItems = new();
        private readonly List<MemoDetailItem> _memoTrashItems = new();
        private readonly HashSet<string> _selectedTrashMemoIds = new(StringComparer.OrdinalIgnoreCase);
        private VisualElement _memoListContainer, _memoDetailPage, _memoDetailMenu, _memoDetailContent, _memoTrashPage, _memoTrashListContainer, _memoLoadingOverlay, _memoLoadingSpinner;
        private Button _memoDetailBackButton, _memoDetailMenuButton, _memoDetailEditButton, _memoDetailDeleteButton, _memoDetailExportButton, _memoTrashButton, _memoTrashBackButton, _memoTrashSelectButton;
        private Button _memoTrashPermanentDeleteButton, _memoTrashRestoreButton;
        private Label _memoDetailPlaceLabel;
        private MemoDetailItem _currentMemoDetailItem;
        private bool _isMemoListLoading;
        private bool _isMemoTrashLoading;
        private bool _isCreatingMemo;
        private bool _isMemoTrashSelecting;

        private void RegisterMemoDetailPage()
        {
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
            _memoTrashButton = _root.Q<Button>("memo-trash-button");
            _memoTrashBackButton = _root.Q<Button>("memo-trash-back-button");
            _memoTrashSelectButton = _root.Q<Button>("memo-trash-select-button");
            _memoTrashPermanentDeleteButton = _root.Q<Button>("memo-trash-permanent-delete-button");
            _memoTrashRestoreButton = _root.Q<Button>("memo-trash-restore-button");
            _memoDetailPlaceLabel = _root.Q<Label>("memo-detail-place-label");

            RebuildMemoList();
            HideMemoDetailPage();
            SetVisible(_memoLoadingOverlay, false);

            _memoDetailBackButton.clicked += HideMemoDetailPage;
            _memoDetailMenuButton.clicked += ToggleMemoDetailMenu;
            _memoDetailEditButton.clicked += ShowCurrentMemoEditPage;
            _memoDetailDeleteButton.clicked += ShowCurrentMemoDeleteConfirm;
            _memoDetailExportButton.clicked += ShareCurrentMemo;
            _memoTrashButton.clicked += ShowMemoTrashPage;
            _memoTrashBackButton.clicked += HideMemoTrashPage;
            _memoTrashSelectButton.clicked += ToggleMemoTrashSelectMode;
            _memoTrashPermanentDeleteButton.clicked += ShowSelectedTrashDeleteConfirm;
            _memoTrashRestoreButton.clicked += ShowSelectedTrashRestoreConfirm;
            _ = RefreshMemoListAsync();
        }

        private void UnregisterMemoDetailPage()
        {
            _memoDetailBackButton.clicked -= HideMemoDetailPage;
            _memoDetailMenuButton.clicked -= ToggleMemoDetailMenu;
            _memoDetailEditButton.clicked -= ShowCurrentMemoEditPage;
            _memoDetailDeleteButton.clicked -= ShowCurrentMemoDeleteConfirm;
            _memoDetailExportButton.clicked -= ShareCurrentMemo;
            _memoTrashButton.clicked -= ShowMemoTrashPage;
            _memoTrashBackButton.clicked -= HideMemoTrashPage;
            _memoTrashSelectButton.clicked -= ToggleMemoTrashSelectMode;
            _memoTrashPermanentDeleteButton.clicked -= ShowSelectedTrashDeleteConfirm;
            _memoTrashRestoreButton.clicked -= ShowSelectedTrashRestoreConfirm;
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
                ApplyMemoListResponse(response);
            }
            finally
            {
                _isMemoListLoading = false;
            }
        }

        private async Awaitable<bool> CreateMemoForMapAsync(ScanMapItem map, string kind, string title, string body, string urgency, string assigneePlayerId, string assigneeName, string dueText, List<MemoChecklistEntry> checklistItems)
        {
            if (_isCreatingMemo)
            {
                return false;
            }

            _isCreatingMemo = true;

            try
            {
                MemoCreateRequest payload = BuildMemoRequest(map, kind, title, body, urgency, assigneePlayerId, assigneeName, dueText, checklistItems);

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

        private async Awaitable<MemoDetailItem> UpdateMemoForMapAsync(MemoDetailItem item, ScanMapItem map, string kind, string title, string body, string urgency, string assigneePlayerId, string assigneeName, string dueText, List<MemoChecklistEntry> checklistItems)
        {
            if (_isCreatingMemo)
            {
                return null;
            }

            _isCreatingMemo = true;

            try
            {
                MemoCreateRequest payload = BuildMemoRequest(map, kind, title, body, urgency, assigneePlayerId, assigneeName, dueText, checklistItems);
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

        private static MemoCreateRequest BuildMemoRequest(ScanMapItem map, string kind, string title, string body, string urgency, string assigneePlayerId, string assigneeName, string dueText, List<MemoChecklistEntry> checklistItems)
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
                checklistItems = checklistItems
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
            RegisterMemoDeleteRow(row);
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
            SetMemoDetailNavMode(true);
            BuildMemoDetailContent(item);
        }

        private void HideMemoDetailPage()
        {
            _currentMemoDetailItem = null;
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

        private static bool CanDeleteMemo(MemoDetailItem item)
        {
            return string.Equals(item.AuthorPlayerId, AuthenticationService.Instance.PlayerId, StringComparison.OrdinalIgnoreCase);
        }

        private bool CanEditMemo(MemoDetailItem item)
        {
            return CanDeleteMemo(item) || CanManageMemo(item);
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
                    builder.AppendLine($"• 이미지 {item.ImageUrls.Count}개");
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
                foreach (string voiceItem in item.VoiceItems)
                {
                    bodyCard.Add(CreateMemoVoiceRow(voiceItem));
                }
            }
            else if (item.Kind == MemoDetailKind.Image)
            {
                VisualElement gallery = new();
                gallery.AddToClassList("memo-image-grid");
                int imageCount = item.ImageUrls.Count > 0 ? item.ImageUrls.Count : 6;
                for (int i = 0; i < imageCount; i++)
                {
                    VisualElement image = new();
                    image.AddToClassList("memo-image-thumb");
                    gallery.Add(image);
                }

                bodyCard.Add(gallery);
            }

            AddMemoDetailFooter(bodyCard, item);
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

        private static VisualElement CreateMemoVoiceRow(string text)
        {
            VisualElement row = new();
            row.AddToClassList("memo-voice-row");
            Label label = new(text);
            label.AddToClassList("memo-voice-label");
            VisualElement icon = new();
            icon.AddToClassList("memo-voice-icon");
            row.Add(label);
            row.Add(icon);
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
            public string Location;
            public string DueText;
            public string Assignee;
            public string Author;
            public string DeletedAt;
            public List<MemoChecklistItem> ChecklistItems = new();
            public List<string> VoiceItems = new();
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
