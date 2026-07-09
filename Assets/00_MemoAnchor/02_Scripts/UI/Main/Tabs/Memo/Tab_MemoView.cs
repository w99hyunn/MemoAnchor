using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace MemoAnchor.UI
{
    public partial class MainView
    {
        private readonly MemoService _memoService = new();
        private readonly List<MemoDetailItem> _memoDetailItems = new();
        private VisualElement _memoListContainer, _memoDetailPage, _memoDetailMenu, _memoDetailContent;
        private Button _memoDetailBackButton, _memoDetailMenuButton;
        private Label _memoDetailPlaceLabel;
        private bool _isMemoListLoading;
        private bool _isCreatingMemo;

        private void RegisterMemoDetailPage()
        {
            _memoListContainer = _root.Q<VisualElement>("memo-list-container");
            _memoDetailPage = _root.Q<VisualElement>("memo-detail-page");
            _memoDetailMenu = _root.Q<VisualElement>("memo-detail-menu");
            _memoDetailContent = _root.Q<VisualElement>("memo-detail-content");
            _memoDetailBackButton = _root.Q<Button>("memo-detail-back-button");
            _memoDetailMenuButton = _root.Q<Button>("memo-detail-menu-button");
            _memoDetailPlaceLabel = _root.Q<Label>("memo-detail-place-label");

            RebuildMemoList();
            HideMemoDetailPage();

            _memoDetailBackButton.clicked += HideMemoDetailPage;
            _memoDetailMenuButton.clicked += ToggleMemoDetailMenu;
            _ = RefreshMemoListAsync();
        }

        private void UnregisterMemoDetailPage()
        {
            _memoDetailBackButton.clicked -= HideMemoDetailPage;
            _memoDetailMenuButton.clicked -= ToggleMemoDetailMenu;
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
                var payload = new MemoCreateRequest
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
                Location = BuildMemoLocation(memo),
                DueText = GetFirstNonEmpty(memo.dueText, string.Empty),
                Assignee = GetFirstNonEmpty(memo.assigneeName, string.Empty),
                Author = GetFirstNonEmpty(memo.authorName, string.Empty)
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
                return;
            }

            foreach (MemoDetailItem item in _memoDetailItems)
            {
                AddMemoListRow(item);
            }
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
            VisualElement row = new();
            row.AddToClassList("memo-list-swipe-row");
            row.userData = item;

            Button deleteButton = new();
            deleteButton.name = "memo-list-delete-button";
            deleteButton.AddToClassList("memo-list-delete-button");
            VisualElement deleteIcon = new();
            deleteIcon.AddToClassList("memo-list-delete-icon");
            deleteButton.Add(deleteIcon);

            VisualElement foreground = new();
            foreground.name = "memo-list-item-foreground";
            foreground.AddToClassList("memo-list-item");
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

            VisualElement body = new();
            body.AddToClassList("memo-list-item-body");

            VisualElement titleRow = new();
            titleRow.AddToClassList("memo-list-item-title-row");
            VisualElement titleWrap = new();
            titleWrap.AddToClassList("memo-list-item-title-wrap");

            VisualElement icon = new();
            icon.AddToClassList("memo-list-item-icon");
            icon.AddToClassList(GetMemoListIconClass(item.Kind));

            Label title = new(item.Title);
            title.AddToClassList("memo-list-item-title");

            VisualElement dot = new();
            dot.AddToClassList("memo-list-item-dot");

            titleWrap.Add(icon);
            titleWrap.Add(title);
            titleRow.Add(titleWrap);
            titleRow.Add(dot);
            body.Add(titleRow);
            AddMemoListMetaRow(body, item.Location, string.Empty);
            AddMemoListMetaRow(body, item.DueText, item.Assignee);

            foreground.Add(body);
            row.Add(deleteButton);
            row.Add(foreground);
            RegisterMemoDeleteRow(row);
            _memoListContainer.Add(row);

            VisualElement divider = new();
            divider.AddToClassList("memo-list-divider");
            _memoListContainer.Add(divider);
        }

        private static void AddMemoListMetaRow(VisualElement body, string first, string second)
        {
            VisualElement row = new();
            row.AddToClassList("memo-list-item-meta-row");
            Label firstLabel = new(first);
            firstLabel.AddToClassList("memo-list-item-meta");
            Label secondLabel = new(second);
            secondLabel.AddToClassList("memo-list-item-meta");
            row.Add(firstLabel);
            row.Add(secondLabel);
            body.Add(row);
        }

        private void ShowMemoDetailPage(MemoDetailItem item)
        {
            bool canManageMemo = CanManageMemo(item);
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
            public string Location;
            public string DueText;
            public string Assignee;
            public string Author;
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
