using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;

namespace MemoAnchor.UI
{
    public partial class MainView
    {
        private readonly List<Texture2D> _homeMemoMediaTextures = new();
        private VisualElement _homeMemoCardContainer;
        private Label _homeMemoEmptyLabel;

        private void RegisterHomeMemoCards()
        {
            _homeMemoCardContainer = _root.Q<VisualElement>("home-memo-card-container");
            _homeMemoEmptyLabel = _root.Q<Label>("home-memo-empty-label");
            RebuildHomeMemoCards();
        }

        private void UnregisterHomeMemoCards()
        {
            ClearHomeMemoMediaTextures();
        }

        private void RebuildHomeMemoCards()
        {
            if (_homeMemoCardContainer == null)
            {
                return;
            }

            ClearHomeMemoMediaTextures();
            _homeMemoCardContainer.Clear();

            List<MemoDetailItem> visibleItems = _memoDetailItems.FindAll(IsHomeMemoVisible);
            if (visibleItems.Count == 0)
            {
                _homeMemoEmptyLabel.text = _isMemoWorkMode
                    ? "수리자로 배정된 메모가 없습니다."
                    : "완료 처리할 메모가 없습니다.";
                SetVisible(_homeMemoEmptyLabel, true);
                return;
            }

            SetVisible(_homeMemoEmptyLabel, false);
            foreach (MemoDetailItem item in visibleItems)
            {
                _homeMemoCardContainer.Add(_isMemoWorkMode
                    ? CreateHomeWorkMemoItem(item)
                    : CreateHomeCompletionRequestItem(item));
            }
        }

        private bool IsHomeMemoVisible(MemoDetailItem item)
        {
            ScanMapItem map = _scanMaps.Find(scanMap => string.Equals(scanMap.id, item.MapId, StringComparison.OrdinalIgnoreCase));
            string role = _isMemoWorkMode ? "repairer" : "manager";
            if (map == null || !string.Equals(map.currentUserRole, role, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return _isMemoWorkMode
                || string.Equals(item.WorkStatus, "completion-requested", StringComparison.OrdinalIgnoreCase);
        }

        private VisualElement CreateHomeWorkMemoItem(MemoDetailItem item)
        {
            VisualElement card = CreateHomeMemoCard(item);

            Label title = new(item.Title);
            title.AddToClassList("memo-title");
            card.Add(title);

            VisualElement divider = new();
            divider.AddToClassList("memo-divider");
            card.Add(divider);

            AddHomeMemoBody(card, item);
            AddHomeMemoFooter(card, item, false);
            return card;
        }

        private VisualElement CreateHomeCompletionRequestItem(MemoDetailItem item)
        {
            VisualElement card = CreateHomeMemoCard(item);
            card.AddToClassList("home-completion-request-card");

            VisualElement titleRow = new();
            titleRow.AddToClassList("home-completion-title-row");
            Label title = new(item.Title);
            title.AddToClassList("home-completion-title");
            Label arrow = new("→");
            arrow.AddToClassList("home-completion-arrow");
            titleRow.Add(title);
            titleRow.Add(arrow);
            card.Add(titleRow);

            VisualElement actions = new();
            actions.AddToClassList("home-completion-actions");
            Button supplementButton = new() { text = "×  보완" };
            supplementButton.AddToClassList("home-completion-action");
            supplementButton.AddToClassList("is-supplement");
            supplementButton.RegisterCallback<ClickEvent>(evt =>
            {
                evt.StopPropagation();
                _ = SetMemoWorkStatusAsync(item, "active", false);
            });
            Button completeButton = new() { text = "✓  완료" };
            completeButton.AddToClassList("home-completion-action");
            completeButton.AddToClassList("is-complete");
            completeButton.RegisterCallback<ClickEvent>(evt =>
            {
                evt.StopPropagation();
                _ = SetMemoWorkStatusAsync(item, "completed", false);
            });
            actions.Add(supplementButton);
            actions.Add(completeButton);
            card.Add(actions);

            AddHomeMemoFooter(card, item, true);
            return card;
        }

        private VisualElement CreateHomeMemoCard(MemoDetailItem item)
        {
            VisualElement card = new();
            card.AddToClassList("memo-card");
            card.RegisterCallback<ClickEvent>(_ =>
            {
                RequestTabSwitch(1);
                ShowMemoDetailPage(item);
            });
            return card;
        }

        private void AddHomeMemoBody(VisualElement card, MemoDetailItem item)
        {
            if (item.Kind == MemoDetailKind.Text)
            {
                Label body = new(item.Body);
                body.AddToClassList("home-memo-body-text");
                card.Add(body);
                return;
            }

            if (item.Kind == MemoDetailKind.Voice)
            {
                int voiceCount = Math.Min(item.VoiceItems.Count, 3);
                for (int i = 0; i < voiceCount; i++)
                {
                    MemoVoiceEntry voiceItem = item.VoiceItems[i];
                    VisualElement row = new();
                    row.AddToClassList("memo-check-row");
                    row.AddToClassList("home-memo-voice-row");
                    Label voiceName = new(voiceItem.name);
                    voiceName.AddToClassList("memo-check-text");
                    VisualElement micIcon = new();
                    micIcon.AddToClassList("memo-voice-icon");
                    row.Add(voiceName);
                    row.Add(micIcon);
                    card.Add(row);
                }

                return;
            }

            if (item.Kind == MemoDetailKind.Checklist)
            {
                int checklistCount = Math.Min(item.ChecklistItems.Count, 3);
                for (int i = 0; i < checklistCount; i++)
                {
                    MemoChecklistItem checklistItem = item.ChecklistItems[i];
                    VisualElement row = new();
                    row.AddToClassList("memo-check-row");
                    row.EnableInClassList("is-complete", checklistItem.Done);
                    Label checklistText = new(checklistItem.Text);
                    checklistText.AddToClassList("memo-check-text");
                    VisualElement checkmark = new();
                    checkmark.AddToClassList("home-memo-checkmark");
                    row.Add(checklistText);
                    row.Add(checkmark);
                    card.Add(row);
                }

                return;
            }

            ScrollView gallery = new(ScrollViewMode.Horizontal)
            {
                horizontalScrollerVisibility = ScrollerVisibility.Hidden,
                verticalScrollerVisibility = ScrollerVisibility.Hidden
            };
            gallery.AddToClassList("home-memo-image-gallery");
            for (int i = 0; i < item.ImageUrls.Count; i++)
            {
                VisualElement preview = new();
                preview.AddToClassList("home-memo-image-preview");
                gallery.Add(preview);
                if (!IsVideoMediaPath(item.ImageUrls[i]))
                {
                    _ = LoadHomeMemoImageAsync(preview, item.ImageUrls[i]);
                }
            }

            card.Add(gallery);
        }

        private void AddHomeMemoFooter(VisualElement card, MemoDetailItem item, bool isManagerCard)
        {
            VisualElement footer = new();
            footer.AddToClassList("memo-footer");

            string personName = isManagerCard ? item.Author : item.Assignee;
            string roleLabel = isManagerCard ? "관리자" : "수리자";
            VisualElement person = new();
            person.AddToClassList("home-memo-footer-person");
            Label role = new(roleLabel);
            role.AddToClassList("memo-footer-text");
            role.AddToClassList("home-memo-footer-role");
            person.Add(role);
            if (!string.IsNullOrWhiteSpace(personName))
            {
                Label name = new(personName);
                name.AddToClassList("memo-footer-text");
                name.AddToClassList("home-memo-footer-person-name");
                person.Add(name);
            }

            Label location = new(FormatHomeMemoLocation(item.Location));
            location.AddToClassList("memo-footer-text");
            location.AddToClassList("home-memo-footer-location");
            Label due = new(FormatHomeMemoDueText(item.DueText));
            due.AddToClassList("memo-footer-text");
            due.AddToClassList("memo-footer-date");

            footer.Add(person);
            footer.Add(location);
            footer.Add(due);
            card.Add(footer);
        }

        private async Awaitable LoadHomeMemoImageAsync(VisualElement preview, string imageUrl)
        {
            using UnityWebRequest request = UnityWebRequestTexture.GetTexture(GetMemoMediaUrl(imageUrl));
            await ServicesManager.SendRequestAsync(request);
            if (request.result != UnityWebRequest.Result.Success)
            {
                return;
            }

            Texture2D texture = DownloadHandlerTexture.GetContent(request);
            if (preview.panel == null)
            {
                Destroy(texture);
                return;
            }

            _homeMemoMediaTextures.Add(texture);
            preview.style.backgroundImage = new StyleBackground(texture);
            preview.AddToClassList("has-preview");
        }

        private void ClearHomeMemoMediaTextures()
        {
            foreach (Texture2D texture in _homeMemoMediaTextures)
            {
                Destroy(texture);
            }

            _homeMemoMediaTextures.Clear();
        }

        private static string FormatHomeMemoDueText(string dueText)
        {
            if (string.IsNullOrWhiteSpace(dueText))
            {
                return string.Empty;
            }

            return dueText.TrimStart().StartsWith("~", StringComparison.Ordinal) ? dueText : $"~ {dueText}";
        }

        private static string FormatHomeMemoLocation(string location)
        {
            int districtEnd = location.IndexOf("구 ", StringComparison.Ordinal);
            return districtEnd < 0 ? location : location[(districtEnd + 2)..].TrimStart();
        }
    }
}
