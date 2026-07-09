using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace MemoAnchor.UI
{
    public partial class MainView
    {
        private const string MEMO_SEARCH_HISTORY_KEY = "MemoAnchor.MemoSearch.History";
        private const int MEMO_SEARCH_HISTORY_LIMIT = 20;

        private TextField _memoSearchSourceInput, _memoSearchPageInput;
        private Button _memoSearchBackButton, _memoSearchClearButton, _memoSearchPageClearButton;
        private Label _memoSearchEmptyLabel;
        private Label _memoSearchNoResultLabel;
        private VisualElement _memoSearchPage, _memoSearchHistoryList;
        private VisualElement _memoSearchHistorySection;
        private VisualElement _memoSearchResultSection;
        private VisualElement _memoSearchResultList;
        private readonly List<MemoSearchHistoryItem> _memoSearchHistory = new();

        private void RegisterMemoSearchPage()
        {
            _memoSearchSourceInput = _root.Q<TextField>("memo-search-input");
            _memoSearchPage = _root.Q<VisualElement>("memo-search-page");
            _memoSearchPageInput = _root.Q<TextField>("memo-search-page-input");
            _memoSearchHistorySection = _root.Q<VisualElement>("memo-search-history-section");
            _memoSearchHistoryList = _root.Q<VisualElement>("memo-search-history-list");
            _memoSearchEmptyLabel = _root.Q<Label>("memo-search-empty-label");
            _memoSearchResultSection = _root.Q<VisualElement>("memo-search-result-section");
            _memoSearchResultList = _root.Q<VisualElement>("memo-search-result-list");
            _memoSearchNoResultLabel = _root.Q<Label>("memo-search-no-result-label");
            _memoSearchBackButton = _root.Q<Button>("memo-search-back-button");
            _memoSearchClearButton = _root.Q<Button>("memo-search-clear-button");
            _memoSearchPageClearButton = _root.Q<Button>("memo-search-page-clear-button");

            SetVisible(_memoSearchPage, false);
            LoadMemoSearchHistory();
            RebuildMemoSearchHistory();
            RefreshMemoSearchPageState(_memoSearchPageInput.value);
            RefreshMemoSearchClearButton();

            _memoSearchSourceInput.RegisterCallback<FocusInEvent>(ShowMemoSearchPage);
            _memoSearchSourceInput.RegisterCallback<ClickEvent>(ShowMemoSearchPage);
            _memoSearchClearButton.RegisterCallback<PointerDownEvent>(StopMemoSearchClearEvent, TrickleDown.TrickleDown);
            _memoSearchClearButton.RegisterCallback<PointerUpEvent>(OnMemoSearchClearPointerUp, TrickleDown.TrickleDown);
            _memoSearchClearButton.RegisterCallback<ClickEvent>(OnMemoSearchClearClicked, TrickleDown.TrickleDown);
            _memoSearchPageClearButton.RegisterCallback<PointerDownEvent>(StopMemoSearchClearEvent, TrickleDown.TrickleDown);
            _memoSearchPageClearButton.RegisterCallback<PointerUpEvent>(OnMemoSearchClearPointerUp, TrickleDown.TrickleDown);
            _memoSearchPageClearButton.RegisterCallback<ClickEvent>(OnMemoSearchClearClicked, TrickleDown.TrickleDown);
            _memoSearchPageInput.RegisterValueChangedCallback(OnMemoSearchInputChanged);
            _memoSearchPageInput.RegisterCallback<KeyDownEvent>(OnMemoSearchKeyDown, TrickleDown.TrickleDown);
            _memoSearchBackButton.clicked += HideMemoSearchPage;
        }

        private void UnregisterMemoSearchPage()
        {
            _memoSearchSourceInput.UnregisterCallback<FocusInEvent>(ShowMemoSearchPage);
            _memoSearchSourceInput.UnregisterCallback<ClickEvent>(ShowMemoSearchPage);
            _memoSearchClearButton.UnregisterCallback<PointerDownEvent>(StopMemoSearchClearEvent, TrickleDown.TrickleDown);
            _memoSearchClearButton.UnregisterCallback<PointerUpEvent>(OnMemoSearchClearPointerUp, TrickleDown.TrickleDown);
            _memoSearchClearButton.UnregisterCallback<ClickEvent>(OnMemoSearchClearClicked, TrickleDown.TrickleDown);
            _memoSearchPageClearButton.UnregisterCallback<PointerDownEvent>(StopMemoSearchClearEvent, TrickleDown.TrickleDown);
            _memoSearchPageClearButton.UnregisterCallback<PointerUpEvent>(OnMemoSearchClearPointerUp, TrickleDown.TrickleDown);
            _memoSearchPageClearButton.UnregisterCallback<ClickEvent>(OnMemoSearchClearClicked, TrickleDown.TrickleDown);
            _memoSearchPageInput.UnregisterValueChangedCallback(OnMemoSearchInputChanged);
            _memoSearchPageInput.UnregisterCallback<KeyDownEvent>(OnMemoSearchKeyDown, TrickleDown.TrickleDown);
            _memoSearchBackButton.clicked -= HideMemoSearchPage;
        }

        private void ShowMemoSearchPage(FocusInEvent _)
        {
            ShowMemoSearchPage();
        }

        private void ShowMemoSearchPage(ClickEvent evt)
        {
            if (IsMemoSearchClearTarget(evt.target as VisualElement))
            {
                evt.StopPropagation();
                return;
            }

            ShowMemoSearchPage();
        }

        private void ShowMemoSearchPage()
        {
            SetVisible(_memoSearchPage, true);
            _memoSearchPageInput.SetValueWithoutNotify(_memoSearchSourceInput.value);
            RefreshMemoSearchPageState(_memoSearchPageInput.value);
            _memoSearchPageInput.schedule.Execute(() => _memoSearchPageInput.Focus()).ExecuteLater(16);
        }

        private void HideMemoSearchPage()
        {
            SetVisible(_memoSearchPage, false);
            _memoSearchPageInput.Blur();
        }

        private void OnMemoSearchInputChanged(ChangeEvent<string> evt)
        {
            RefreshMemoSearchPageState(evt.newValue);
        }

        private void OnMemoSearchClearClicked(ClickEvent evt)
        {
            ClearMemoSearchInput();
            evt.StopImmediatePropagation();
        }

        private void OnMemoSearchClearPointerUp(PointerUpEvent evt)
        {
            ClearMemoSearchInput();
            evt.StopImmediatePropagation();
        }

        private static void StopMemoSearchClearEvent(PointerDownEvent evt)
        {
            evt.StopImmediatePropagation();
        }

        private void ClearMemoSearchInput()
        {
            _memoSearchSourceInput.SetValueWithoutNotify(string.Empty);
            _memoSearchPageInput.SetValueWithoutNotify(string.Empty);
            RefreshMemoSearchPageState(string.Empty);
            RefreshMemoSearchClearButton();
        }

        private void OnMemoSearchKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter)
            {
                return;
            }

            SubmitMemoSearch(_memoSearchPageInput.value);
            evt.StopPropagation();
        }

        private void SubmitMemoSearch(string query)
        {
            string normalizedQuery = query.Trim();
            _memoSearchSourceInput.SetValueWithoutNotify(normalizedQuery);
            _memoSearchPageInput.SetValueWithoutNotify(normalizedQuery);
            RefreshMemoSearchPageState(normalizedQuery);
            RefreshMemoSearchClearButton();

            if (normalizedQuery.Length > 0)
            {
                AddMemoSearchHistory(normalizedQuery);
            }
        }

        private void RefreshMemoSearchPageState(string query)
        {
            string normalizedQuery = query.Trim();
            bool hasQuery = normalizedQuery.Length > 0;
            _memoSearchSourceInput.SetValueWithoutNotify(normalizedQuery);
            RefreshMemoSearchClearButton();
            SetVisible(_memoSearchHistorySection, !hasQuery);
            SetVisible(_memoSearchResultSection, hasQuery);
            ApplyMemoSearch(normalizedQuery);

            if (hasQuery)
            {
                RebuildMemoSearchResults(normalizedQuery);
            }
            else
            {
                _memoSearchResultList.Clear();
                SetVisible(_memoSearchNoResultLabel, false);
            }
        }

        private void ApplyMemoSearch(string query)
        {
            string normalizedQuery = query.Trim();
            _root.Query<VisualElement>(className: "memo-list-swipe-row").ForEach(row =>
            {
                bool visible = normalizedQuery.Length == 0 || MemoSearchRowMatches(row, normalizedQuery);
                row.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            });
        }

        private void RebuildMemoSearchResults(string query)
        {
            _memoSearchResultList.Clear();
            int resultCount = 0;

            _root.Query<VisualElement>(className: "memo-list-swipe-row").ForEach(row =>
            {
                if (!MemoSearchRowMatches(row, query))
                {
                    return;
                }

                _memoSearchResultList.Add(CreateMemoSearchResultRow(row));
                resultCount++;
            });

            SetVisible(_memoSearchNoResultLabel, resultCount == 0);
        }

        private void RefreshMemoSearchClearButton()
        {
            bool hasQuery = _memoSearchSourceInput.value.Trim().Length > 0;
            SetVisible(_memoSearchClearButton, hasQuery);
            SetVisible(_memoSearchPageClearButton, hasQuery);
        }

        private bool IsMemoSearchClearTarget(VisualElement element)
        {
            while (element != null)
            {
                if (element == _memoSearchClearButton || element == _memoSearchPageClearButton)
                {
                    return true;
                }

                element = element.parent;
            }

            return false;
        }

        private static bool MemoSearchRowMatches(VisualElement row, string query)
        {
            bool matches = false;
            row.Query<Label>().ForEach(label =>
            {
                if (label.text.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    matches = true;
                }
            });

            return matches;
        }

        private VisualElement CreateMemoSearchResultRow(VisualElement sourceRow)
        {
            VisualElement row = new();
            row.AddToClassList("memo-search-result-row");

            VisualElement item = new();
            item.AddToClassList("memo-list-item");
            item.AddToClassList("memo-search-result-item");

            VisualElement body = new();
            body.AddToClassList("memo-list-item-body");

            VisualElement titleRow = new();
            titleRow.AddToClassList("memo-search-result-title-row");

            VisualElement titleWrap = new();
            titleWrap.AddToClassList("memo-search-result-title-wrap");

            VisualElement icon = new();
            icon.AddToClassList("memo-list-item-icon");
            if (sourceRow.userData is MemoDetailItem memoItem)
            {
                icon.AddToClassList(GetMemoListIconClass(memoItem.Kind));
                item.RegisterCallback<ClickEvent>(_ =>
                {
                    HideMemoSearchPage();
                    ShowMemoDetailPage(memoItem);
                });
            }

            Label sourceTitle = sourceRow.Q<Label>(className: "memo-list-item-title");
            Label title = new(sourceTitle.text);
            title.AddToClassList("memo-list-item-title");
            title.style.unityTextAlign = TextAnchor.MiddleLeft;

            VisualElement dot = new();
            dot.AddToClassList("memo-list-item-dot");

            titleWrap.Add(icon);
            titleWrap.Add(title);
            titleRow.Add(titleWrap);
            titleRow.Add(dot);
            body.Add(titleRow);

            AddMemoSearchResultMetaRows(sourceRow, body);
            item.Add(body);
            row.Add(item);
            return row;
        }

        private static void AddMemoSearchResultMetaRows(VisualElement sourceRow, VisualElement body)
        {
            List<Label> sourceMetaLabels = new();
            sourceRow.Query<Label>(className: "memo-list-item-meta").ForEach(sourceMetaLabels.Add);

            for (int i = 0; i < sourceMetaLabels.Count; i += 2)
            {
                VisualElement metaRow = new();
                metaRow.AddToClassList("memo-list-item-meta-row");

                AddMemoSearchResultMetaLabel(metaRow, sourceMetaLabels[i].text);
                if (i + 1 < sourceMetaLabels.Count)
                {
                    AddMemoSearchResultMetaLabel(metaRow, sourceMetaLabels[i + 1].text);
                }

                body.Add(metaRow);
            }
        }

        private static void AddMemoSearchResultMetaLabel(VisualElement row, string text)
        {
            Label metaLabel = new(text);
            metaLabel.AddToClassList("memo-list-item-meta");
            row.Add(metaLabel);
        }

        private void AddMemoSearchHistory(string query)
        {
            _memoSearchHistory.RemoveAll(item => string.Equals(item.Query, query, StringComparison.OrdinalIgnoreCase));
            _memoSearchHistory.Insert(0, new MemoSearchHistoryItem(query, DateTime.Today.ToString("MM.dd")));

            if (_memoSearchHistory.Count > MEMO_SEARCH_HISTORY_LIMIT)
            {
                _memoSearchHistory.RemoveRange(MEMO_SEARCH_HISTORY_LIMIT, _memoSearchHistory.Count - MEMO_SEARCH_HISTORY_LIMIT);
            }

            SaveMemoSearchHistory();
            RebuildMemoSearchHistory();
        }

        private void RemoveMemoSearchHistory(MemoSearchHistoryItem item)
        {
            _memoSearchHistory.Remove(item);
            SaveMemoSearchHistory();
            RebuildMemoSearchHistory();
        }

        private void RebuildMemoSearchHistory()
        {
            _memoSearchHistoryList.Clear();
            SetVisible(_memoSearchEmptyLabel, _memoSearchHistory.Count == 0);

            foreach (MemoSearchHistoryItem item in _memoSearchHistory)
            {
                VisualElement row = new();
                row.AddToClassList("memo-search-history-row");

                Button contentButton = new();
                contentButton.AddToClassList("memo-search-history-content");
                contentButton.clicked += () => SubmitMemoSearch(item.Query);

                VisualElement icon = new();
                icon.AddToClassList("memo-search-history-icon");

                Label queryLabel = new(item.Query);
                queryLabel.AddToClassList("memo-search-history-query");

                Label dateLabel = new(item.Date);
                dateLabel.AddToClassList("memo-search-history-date");

                Button removeButton = new();
                removeButton.AddToClassList("memo-search-history-remove");
                removeButton.AddToClassList("common-icon-button");
                removeButton.RegisterCallback<ClickEvent>(evt =>
                {
                    RemoveMemoSearchHistory(item);
                    evt.StopPropagation();
                });

                VisualElement removeIcon = new();
                removeIcon.AddToClassList("common-icon-button-icon");
                removeIcon.AddToClassList("memo-search-history-remove-icon");

                contentButton.Add(icon);
                contentButton.Add(queryLabel);
                contentButton.Add(dateLabel);
                removeButton.Add(removeIcon);
                row.Add(contentButton);
                row.Add(removeButton);
                _memoSearchHistoryList.Add(row);
            }
        }

        private void LoadMemoSearchHistory()
        {
            _memoSearchHistory.Clear();
            string json = PlayerPrefs.GetString(MEMO_SEARCH_HISTORY_KEY, string.Empty);
            if (string.IsNullOrEmpty(json))
            {
                return;
            }

            MemoSearchHistoryState state = JsonUtility.FromJson<MemoSearchHistoryState>(json);
            _memoSearchHistory.AddRange(state.Items);
        }

        private void SaveMemoSearchHistory()
        {
            var state = new MemoSearchHistoryState
            {
                Items = _memoSearchHistory
            };

            PlayerPrefs.SetString(MEMO_SEARCH_HISTORY_KEY, JsonUtility.ToJson(state));
            PlayerPrefs.Save();
        }

        [Serializable]
        private class MemoSearchHistoryState
        {
            public List<MemoSearchHistoryItem> Items = new();
        }

        [Serializable]
        private class MemoSearchHistoryItem
        {
            public MemoSearchHistoryItem()
            {
            }

            public MemoSearchHistoryItem(string query, string date)
            {
                Query = query;
                Date = date;
            }

            public string Query;
            public string Date;
        }
    }
}
