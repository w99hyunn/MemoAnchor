using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace MemoAnchor.UI
{
    public partial class MainTabView
    {
        private const string MEMO_SEARCH_HISTORY_KEY = "MemoAnchor.MemoSearch.History";
        private const int MEMO_SEARCH_HISTORY_LIMIT = 20;

        private Label _memoSearchEmptyLabel;
        private readonly List<MemoSearchHistoryItem> _memoSearchHistory = new();

        private void RegisterMemoSearchPage()
        {
            _memoSearchSourceInput = _root.Q<TextField>("memo-search-input");
            _memoSearchPage = _root.Q<VisualElement>("memo-search-page");
            _memoSearchPageInput = _root.Q<TextField>("memo-search-page-input");
            _memoSearchHistoryList = _root.Q<VisualElement>("memo-search-history-list");
            _memoSearchEmptyLabel = _root.Q<Label>("memo-search-empty-label");
            _memoSearchBackButton = _root.Q<Button>("memo-search-back-button");

            SetVisible(_memoSearchPage, false);
            LoadMemoSearchHistory();
            RebuildMemoSearchHistory();

            _memoSearchSourceInput.RegisterCallback<FocusInEvent>(ShowMemoSearchPage);
            _memoSearchSourceInput.RegisterCallback<ClickEvent>(ShowMemoSearchPage);
            _memoSearchPageInput.RegisterCallback<KeyDownEvent>(OnMemoSearchKeyDown, TrickleDown.TrickleDown);
            _memoSearchBackButton.clicked += HideMemoSearchPage;
        }

        private void UnregisterMemoSearchPage()
        {
            _memoSearchSourceInput.UnregisterCallback<FocusInEvent>(ShowMemoSearchPage);
            _memoSearchSourceInput.UnregisterCallback<ClickEvent>(ShowMemoSearchPage);
            _memoSearchPageInput.UnregisterCallback<KeyDownEvent>(OnMemoSearchKeyDown, TrickleDown.TrickleDown);
            _memoSearchBackButton.clicked -= HideMemoSearchPage;
        }

        private void ShowMemoSearchPage(FocusInEvent _)
        {
            ShowMemoSearchPage();
        }

        private void ShowMemoSearchPage(ClickEvent _)
        {
            ShowMemoSearchPage();
        }

        private void ShowMemoSearchPage()
        {
            SetVisible(_memoSearchPage, true);
            _memoSearchPageInput.value = _memoSearchSourceInput.value;
            _memoSearchPageInput.schedule.Execute(() => _memoSearchPageInput.Focus()).ExecuteLater(16);
        }

        private void HideMemoSearchPage()
        {
            SetVisible(_memoSearchPage, false);
            _memoSearchPageInput.Blur();
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
            _memoSearchSourceInput.value = normalizedQuery;
            _memoSearchPageInput.value = normalizedQuery;
            ApplyMemoSearch(normalizedQuery);

            if (normalizedQuery.Length > 0)
            {
                AddMemoSearchHistory(normalizedQuery);
            }
        }

        private void ApplyMemoSearch(string query)
        {
            string normalizedQuery = query.Trim();
            _root.Query<VisualElement>(className: "memo-list-swipe-row").ForEach(row =>
            {
                Label title = row.Q<Label>(className: "memo-list-item-title");
                bool visible = normalizedQuery.Length == 0 || title.text.IndexOf(normalizedQuery, StringComparison.OrdinalIgnoreCase) >= 0;
                row.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            });
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
                removeButton.RegisterCallback<ClickEvent>(evt =>
                {
                    RemoveMemoSearchHistory(item);
                    evt.StopPropagation();
                });

                VisualElement removeIcon = new();
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
