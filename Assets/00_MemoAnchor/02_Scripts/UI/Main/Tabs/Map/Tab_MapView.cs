using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MemoAnchor;
using Unity.Services.Authentication;
using Unity.Services.Friends;
using Unity.Services.Friends.Models;
using UnityEngine;
using UnityEngine.UIElements;

namespace MemoAnchor.UI
{
    public partial class MainView
    {
        private const int MAP_FRIEND_INVITE_PAGE_SIZE = 20;

        [SerializeField] private VisualTreeAsset _mapParticipantItemAsset;
        [SerializeField] private VisualTreeAsset _mapFriendInviteItemAsset;

        private readonly ScanMapService _scanMapService = new();
        private readonly HashSet<string> _openMapAddresses = new(StringComparer.Ordinal);
        private readonly List<ScanMapItem> _scanMaps = new();
        private readonly HashSet<string> _selectedMapFriendInvitePlayerIds = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, MapFriendProfileItem> _mapFriendInviteProfiles = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<Relationship> _availableMapFriendInviteRelationships = new();
        private readonly List<MemoItem> _readOnlyMemos = new();
        private ScanMapItem _readOnlyMap;
        private Button _mapListButton, _mapDetailButton, _mapReconstructionButton, _mapPreviewMenuButton, _mapMemoBackButton, _mapMemoSearchClearButton, _mapCreateTextMemoButton, _mapCreateChecklistMemoButton, _mapCreateMediaMemoButton, _mapCreateVoiceMemoButton;
        private Button _mapParticipantsButton, _mapParticipantsInviteButton, _mapParticipantsAddButton;
        private Button _mapFriendInviteBackButton, _mapFriendInviteSubmitButton, _mapFriendInviteLoadMoreButton;
        private VisualElement _mapPreview, _mapMemoPage, _mapMemoListContainer, _mapMemoEmptyState, _mapListOverlay, _mapListSheet, _mapListContent, _mapEmptyState, _mapCreateMemoActions;
        private VisualElement _mapParticipantsOverlay, _mapParticipantsSheet, _mapParticipantsList, _mapParticipantsInviteCodeContent;
        private VisualElement _mapFriendInviteOverlay, _mapFriendInviteSheet, _mapFriendInviteList;
        private Label _mapCurrentSpaceLabel, _mapCurrentAddressLabel, _mapMemoSpaceLabel, _mapMemoAddressLabel, _mapScanTimeLabel, _mapParticipantsTitle, _mapParticipantsManagerSummary, _mapParticipantsRepairerSummary;
        private TextField _mapMemoSearchInput;
        private Label _mapParticipantsInviteIssueLabel, _mapParticipantsInviteTimer, _mapParticipantsInviteCodeLabel;
        private IVisualElementScheduledItem _mapParticipantsInviteSchedule;
        private DateTimeOffset _mapParticipantsInviteExpiresAt;
        private string _mapParticipantsInviteCode = string.Empty;
        private string _selectedMapId;
        private int _mapListTransitionToken, _mapParticipantsTransitionToken, _mapFriendInviteTransitionToken;
        private int _mapFriendInviteVisibleCount;
        private bool _isMapListLoading;

        private void RegisterMapPage()
        {
            VisualElement mainRoot = _root.Q<VisualElement>("main-root");
            _mapListButton = _root.Q<Button>("map-list-button");
            _mapDetailButton = _root.Q<Button>("map-detail-button");
            _mapReconstructionButton = _root.Q<Button>("map-reconstruction-button");
            _mapPreviewMenuButton = _root.Q<Button>("map-preview-menu-button");
            _mapMemoBackButton = _root.Q<Button>("map-memo-back-button");
            _mapMemoSearchClearButton = _root.Q<Button>("map-memo-search-clear-button");
            _mapCreateMemoActions = _root.Q<VisualElement>("map-create-memo-actions");
            _mapCreateTextMemoButton = _root.Q<Button>("map-create-text-memo-button");
            _mapCreateChecklistMemoButton = _root.Q<Button>("map-create-checklist-memo-button");
            _mapCreateMediaMemoButton = _root.Q<Button>("map-create-media-memo-button");
            _mapCreateVoiceMemoButton = _root.Q<Button>("map-create-voice-memo-button");
            _mapParticipantsButton = _root.Q<Button>("map-participants-button");
            _mapParticipantsInviteButton = _root.Q<Button>("map-participants-invite-button");
            _mapParticipantsAddButton = _root.Q<Button>("map-participants-add-button");
            _mapFriendInviteBackButton = _root.Q<Button>("map-friend-invite-back-button");
            _mapFriendInviteSubmitButton = _root.Q<Button>("map-friend-invite-submit-button");
            _mapFriendInviteLoadMoreButton = _root.Q<Button>("map-friend-invite-load-more-button");
            _mapPreview = _root.Q<VisualElement>("map-preview");
            _mapMemoPage = _root.Q<VisualElement>("map-memo-page");
            _mapMemoListContainer = _root.Q<VisualElement>("map-memo-list-container");
            _mapMemoEmptyState = _root.Q<VisualElement>("map-memo-empty-state");
            _mapMemoSearchInput = _root.Q<TextField>("map-memo-search-input");
            _mapListOverlay = _root.Q<VisualElement>("map-list-overlay");
            _mapListSheet = _root.Q<VisualElement>("map-list-sheet");
            _mapListContent = _root.Q<VisualElement>("map-list-content");
            _mapCurrentSpaceLabel = _root.Q<Label>("map-current-space-label");
            _mapCurrentAddressLabel = _root.Q<Label>("map-current-address-label");
            _mapMemoSpaceLabel = _root.Q<Label>("map-memo-space-label");
            _mapMemoAddressLabel = _root.Q<Label>("map-memo-address-label");
            _mapScanTimeLabel = _root.Q<Label>("map-scan-time-label");
            _mapEmptyState = _root.Q<VisualElement>("map-empty-state");
            _mapParticipantsOverlay = _root.Q<VisualElement>("map-participants-overlay");
            _mapParticipantsSheet = _root.Q<VisualElement>("map-participants-sheet");
            _mapParticipantsList = _root.Q<VisualElement>("map-participants-list");
            _mapParticipantsInviteCodeContent = _root.Q<VisualElement>("map-participants-invite-code-content");
            _mapParticipantsTitle = _mapParticipantsButton.Q<Label>(className: "map-participants-title");
            _mapParticipantsManagerSummary = _root.Q<Label>("map-participants-manager-summary");
            _mapParticipantsRepairerSummary = _root.Q<Label>("map-participants-repairer-summary");
            _mapParticipantsInviteIssueLabel = _root.Q<Label>("map-participants-invite-issue-label");
            _mapParticipantsInviteTimer = _root.Q<Label>("map-participants-invite-timer");
            _mapParticipantsInviteCodeLabel = _root.Q<Label>("map-participants-invite-code");
            _mapFriendInviteOverlay = _root.Q<VisualElement>("map-friend-invite-overlay");
            _mapFriendInviteSheet = _root.Q<VisualElement>("map-friend-invite-sheet");
            _mapFriendInviteList = _root.Q<VisualElement>("map-friend-invite-list");

            mainRoot.Add(_mapListOverlay);
            _mapListOverlay.BringToFront();
            _mapListOverlay.AddToClassList(DIALOG_ANIM_READY_CLASS);
            _mapListOverlay.AddToClassList(HIDDEN_CLASS);
            mainRoot.Add(_mapParticipantsOverlay);
            _mapParticipantsOverlay.BringToFront();
            _mapParticipantsOverlay.AddToClassList(DIALOG_ANIM_READY_CLASS);
            _mapParticipantsOverlay.AddToClassList(HIDDEN_CLASS);
            mainRoot.Add(_mapFriendInviteOverlay);
            _mapFriendInviteOverlay.BringToFront();
            _mapFriendInviteOverlay.AddToClassList(DIALOG_ANIM_READY_CLASS);
            _mapFriendInviteOverlay.AddToClassList(HIDDEN_CLASS);
            _mapListButton.clicked += ShowMapList;
            _mapDetailButton.clicked += ShowMapDetail;
            _mapReconstructionButton.clicked += OpenSelectedMapReconstruction;
            _mapPreviewMenuButton.clicked += ShowMapMemoPage;
            _mapMemoBackButton.clicked += HideMapMemoPage;
            _mapMemoSearchClearButton.clicked += ClearMapMemoSearch;
            _mapMemoSearchInput.RegisterValueChangedCallback(OnMapMemoSearchChanged);
            _mapCreateTextMemoButton.clicked += OnClickMapCreateTextMemo;
            _mapCreateChecklistMemoButton.clicked += OnClickMapCreateChecklistMemo;
            _mapCreateMediaMemoButton.clicked += OnClickMapCreateMediaMemo;
            _mapCreateVoiceMemoButton.clicked += OnClickMapCreateVoiceMemo;
            _mapParticipantsButton.clicked += ShowMapParticipants;
            _mapParticipantsInviteButton.clicked += OnClickMapParticipantsInvite;
            _mapParticipantsAddButton.clicked += ShowMapFriendInvite;
            _mapFriendInviteBackButton.clicked += HideMapFriendInvite;
            _mapFriendInviteSubmitButton.clicked += OnClickMapFriendInviteSubmit;
            _mapFriendInviteLoadMoreButton.clicked += OnClickMapFriendInviteLoadMore;
            _mapListOverlay.RegisterCallback<ClickEvent>(_ => HideMapList());
            _mapListSheet.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
            _mapParticipantsOverlay.RegisterCallback<ClickEvent>(_ => HideMapParticipants());
            _mapParticipantsSheet.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
            _mapFriendInviteOverlay.RegisterCallback<ClickEvent>(_ => HideMapFriendInvite());
            _mapFriendInviteSheet.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
            _mapParticipantsInviteSchedule = _mapParticipantsInviteButton.schedule.Execute(UpdateMapParticipantsInviteTimer).Every(1000);
            _mapParticipantsInviteSchedule.Pause();
            ApplySelectedMap();
        }

        private void UnregisterMapPage()
        {
            _mapListButton.clicked -= ShowMapList;
            _mapDetailButton.clicked -= ShowMapDetail;
            _mapReconstructionButton.clicked -= OpenSelectedMapReconstruction;
            _mapPreviewMenuButton.clicked -= ShowMapMemoPage;
            _mapMemoBackButton.clicked -= HideMapMemoPage;
            _mapMemoSearchClearButton.clicked -= ClearMapMemoSearch;
            _mapMemoSearchInput.UnregisterValueChangedCallback(OnMapMemoSearchChanged);
            _mapCreateTextMemoButton.clicked -= OnClickMapCreateTextMemo;
            _mapCreateChecklistMemoButton.clicked -= OnClickMapCreateChecklistMemo;
            _mapCreateMediaMemoButton.clicked -= OnClickMapCreateMediaMemo;
            _mapCreateVoiceMemoButton.clicked -= OnClickMapCreateVoiceMemo;
            _mapParticipantsButton.clicked -= ShowMapParticipants;
            _mapParticipantsInviteButton.clicked -= OnClickMapParticipantsInvite;
            _mapParticipantsAddButton.clicked -= ShowMapFriendInvite;
            _mapFriendInviteBackButton.clicked -= HideMapFriendInvite;
            _mapFriendInviteSubmitButton.clicked -= OnClickMapFriendInviteSubmit;
            _mapFriendInviteLoadMoreButton.clicked -= OnClickMapFriendInviteLoadMore;
            _mapParticipantsInviteSchedule.Pause();
        }

        public async Awaitable RefreshMapListAsync()
        {
            if (_isMapListLoading)
            {
                return;
            }

            _isMapListLoading = true;

            try
            {
                ScanMapListResponse response = await _scanMapService.LoadMapsAsync();
                ApplyMapListResponse(response);
            }
            finally
            {
                _isMapListLoading = false;
            }
        }

        private void ApplyMapListResponse(ScanMapListResponse response)
        {
            _scanMaps.Clear();
            _scanMaps.AddRange(response.maps);
            if (_readOnlyMap != null && !_scanMaps.Exists(map => map.id == _readOnlyMap.id))
            {
                _scanMaps.Add(_readOnlyMap);
            }

            if (_scanMaps.Count == 0)
            {
                _selectedMapId = string.Empty;
                _openMapAddresses.Clear();
            }
            else if (string.IsNullOrWhiteSpace(_selectedMapId) || !_scanMaps.Exists(map => map.id == _selectedMapId))
            {
                _selectedMapId = _scanMaps[0].id;
                _openMapAddresses.Add(GetMapAddressKey(_scanMaps[0]));
            }

            RebuildMapList();
            ApplySelectedMap();
            ScanMapItem selectedMap = GetSelectedMap();
            if (selectedMap != null)
            {
                RebuildMapParticipants(selectedMap);
            }

            RebuildMemoList();
        }

        private void ShowMapList()
        {
            RebuildMapList();
            _mapListTransitionToken++;
            _mapListOverlay.RemoveFromClassList(HIDDEN_CLASS);
            _mapListOverlay.RemoveFromClassList(DIALOG_OPEN_CLASS);

            int token = _mapListTransitionToken;
            _mapListOverlay.schedule.Execute(() =>
            {
                if (token != _mapListTransitionToken)
                {
                    return;
                }

                _mapListOverlay.AddToClassList(DIALOG_OPEN_CLASS);
            }).ExecuteLater(16);
        }

        private void ShowMapDetail()
        {
            ScanMapItem map = GetSelectedMap();
            if (map == null)
            {
                return;
            }

            string role = map.currentUserRole switch
            {
                "manager" => "관리자",
                "repairer" => "수리자",
                "read-only" => "읽기모드",
                _ => map.currentUserRole
            };
            string participantCount = string.Equals(map.currentUserRole, "read-only", StringComparison.OrdinalIgnoreCase)
                ? "비공개"
                : $"{map.members.Count}명";
            string details = $"<b>주소</b>\n{GetMapAddressKey(map)}\n\n<b>스캔 일시</b>\n{FormatScanTime(map.scanCreatedAt)}\n\n<b>내 역할</b>\n{role}\n\n<b>참여 인원</b>\n{participantCount}";
            if (string.Equals(map.currentUserRole, "manager", StringComparison.OrdinalIgnoreCase))
            {
                PopupManager.ShowConfirm(map.spaceName, details, "닫기", "맵 삭제", () => ShowDeleteMapConfirm(map));
                return;
            }

            PopupManager.ShowMessage(map.spaceName, details, "닫기");
        }

        private void ShowDeleteMapConfirm(ScanMapItem map)
        {
            PopupManager.ShowConfirm("맵 삭제", $"{map.spaceName} 맵과 해당 맵의 모든 메모를 삭제할까요?", "취소", "삭제", () => ShowDeleteMapNameInput(map));
        }

        private void ShowDeleteMapNameInput(ScanMapItem map, bool nameMismatch = false)
        {
            string message = nameMismatch
                ? $"맵 이름이 일치하지 않습니다.\n삭제하려면 '{map.spaceName}'을 다시 입력해주세요."
                : $"삭제하려면 맵 이름 '{map.spaceName}'을 입력해주세요.";
            PopupManager.ShowTextInput("맵 삭제 확인", message, string.Empty, "맵 이름 입력", "취소", "삭제", value => ConfirmDeleteMapName(map, value));
        }

        private void ConfirmDeleteMapName(ScanMapItem map, string value)
        {
            if (!string.Equals(value.Trim(), map.spaceName, StringComparison.Ordinal))
            {
                ShowDeleteMapNameInput(map, true);
                return;
            }

            _ = DeleteMapAsync(map);
        }

        private async Awaitable DeleteMapAsync(ScanMapItem map)
        {
            LoadingSpinnerController.ShowOverlay(_mainLoadingOverlay, _mainLoadingSpinner);
            try
            {
                ScanMapListResponse response = await _scanMapService.DeleteMapAsync(map.id);
                if (response == null)
                {
                    PopupManager.ShowMessage("맵 삭제 실패", "서버에서 맵을 삭제하지 못했습니다.", "확인");
                    return;
                }

                if (string.Equals(_readOnlyMap?.id, map.id, StringComparison.OrdinalIgnoreCase))
                {
                    _readOnlyMap = null;
                    _readOnlyMemos.Clear();
                }
                _scanMaps.Clear();
                _scanMaps.AddRange(response.maps);
                _selectedMapId = _scanMaps.Count > 0 ? _scanMaps[0].id : null;
                RebuildMapList();
                ApplySelectedMap();
                await RefreshMemoListAsync();
            }
            finally
            {
                LoadingSpinnerController.HideOverlay(_mainLoadingOverlay, _mainLoadingSpinner);
            }
        }

        private void ShowMapMemoPage()
        {
            ScanMapItem map = GetSelectedMap();
            if (map == null)
            {
                return;
            }

            _mapMemoSpaceLabel.text = map.spaceName;
            _mapMemoAddressLabel.text = string.IsNullOrWhiteSpace(map.roadAddress) ? map.address : map.roadAddress;
            _mapMemoSearchInput.SetValueWithoutNotify(string.Empty);
            SetVisible(_mapMemoSearchClearButton, false);
            SetVisible(_mapMemoPage, true);
            RebuildMapMemoList();
            _ = RefreshMapMemoPageAsync();
        }

        private async Awaitable RefreshMapMemoPageAsync()
        {
            await RefreshMemoListAsync();
            if (!_mapMemoPage.ClassListContains(HIDDEN_CLASS))
            {
                RebuildMapMemoList();
            }
        }

        private void HideMapMemoPage()
        {
            SetVisible(_mapMemoPage, false);
        }

        private void OnMapMemoSearchChanged(ChangeEvent<string> evt)
        {
            SetVisible(_mapMemoSearchClearButton, !string.IsNullOrEmpty(evt.newValue));
            RebuildMapMemoList();
        }

        private void ClearMapMemoSearch()
        {
            _mapMemoSearchInput.value = string.Empty;
        }

        private void RebuildMapMemoList()
        {
            ScanMapItem map = GetSelectedMap();
            string query = _mapMemoSearchInput.value?.Trim() ?? string.Empty;
            List<MemoDetailItem> items = _memoDetailItems
                .Where(item => string.Equals(item.MapId, map.id, StringComparison.OrdinalIgnoreCase))
                .Where(item => string.IsNullOrEmpty(query)
                    || item.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || item.Body.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || item.Assignee.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();

            _mapMemoListContainer.Clear();
            _mapMemoListContainer.parent.EnableInClassList("is-memo-list-empty", items.Count == 0);
            SetVisible(_mapMemoEmptyState, items.Count == 0);
            foreach (MemoDetailItem item in items)
            {
                TemplateContainer template = _memoListItemAsset.Instantiate();
                ApplyMemoListRow(template, item);
                template.Q<VisualElement>("memo-list-item-foreground").RegisterCallback<ClickEvent>(_ =>
                {
                    HideMapMemoPage();
                    RequestTabSwitch(1);
                    ShowMemoDetailPage(item, true);
                });
                _mapMemoListContainer.Add(template);
            }
        }

        private void HideMapList()
        {
            _mapListTransitionToken++;
            int token = _mapListTransitionToken;

            _mapListOverlay.RemoveFromClassList(DIALOG_OPEN_CLASS);
            _mapListOverlay.schedule.Execute(() =>
            {
                if (token != _mapListTransitionToken)
                {
                    return;
                }

                _mapListOverlay.AddToClassList(HIDDEN_CLASS);
            }).ExecuteLater(240);
        }

        private void RebuildMapList()
        {
            RebuildHomeMapCards();
            _mapListContent.Clear();

            Dictionary<string, List<ScanMapItem>> mapsByAddress = new(StringComparer.Ordinal);
            foreach (ScanMapItem map in _scanMaps)
            {
                string address = GetMapAddressKey(map);
                if (!mapsByAddress.TryGetValue(address, out List<ScanMapItem> maps))
                {
                    maps = new List<ScanMapItem>();
                    mapsByAddress.Add(address, maps);
                }

                maps.Add(map);
            }

            foreach (KeyValuePair<string, List<ScanMapItem>> pair in mapsByAddress)
            {
                AddMapAddressRow(pair.Key, pair.Value);
            }
        }

        private void AddMapAddressRow(string address, List<ScanMapItem> maps)
        {
            bool isOpen = _openMapAddresses.Contains(address);
            Button addressRow = new();
            addressRow.AddToClassList("scan-address-list-item");
            addressRow.AddToClassList("scan-address-building-item");
            addressRow.AddToClassList("map-list-address-row");
            addressRow.clicked += () =>
            {
                if (!_openMapAddresses.Remove(address))
                {
                    _openMapAddresses.Add(address);
                }

                RebuildMapList();
            };

            Label addressLabel = new(address);
            addressLabel.AddToClassList("scan-address-list-text");
            addressLabel.AddToClassList("map-list-address-text");

            VisualElement chevron = new();
            chevron.AddToClassList("map-list-chevron");
            chevron.EnableInClassList("is-open", isOpen);

            addressRow.Add(addressLabel);
            addressRow.Add(chevron);
            _mapListContent.Add(addressRow);

            if (!isOpen)
            {
                return;
            }

            foreach (ScanMapItem map in maps)
            {
                AddMapSpaceRow(map);
            }
        }

        private void AddMapSpaceRow(ScanMapItem map)
        {
            Button spaceRow = new();
            spaceRow.AddToClassList("scan-address-list-item");
            spaceRow.AddToClassList("map-list-space-row");
            spaceRow.EnableInClassList(SELECTED_CLASS, map.id == _selectedMapId);
            spaceRow.clicked += () =>
            {
                _selectedMapId = map.id;
                _openMapAddresses.Add(GetMapAddressKey(map));
                ApplySelectedMap();
                RebuildMapList();
                HideMapList();
            };

            Label spaceLabel = new(map.spaceName);
            spaceLabel.AddToClassList("scan-address-list-text");
            spaceLabel.AddToClassList("map-list-space-text");

            spaceRow.Add(spaceLabel);
            _mapListContent.Add(spaceRow);
        }

        private void ApplySelectedMap()
        {
            ScanMapItem selectedMap = GetSelectedMap();
            bool hasMap = selectedMap != null;
            bool isReadOnly = hasMap && string.Equals(selectedMap.currentUserRole, "read-only", StringComparison.OrdinalIgnoreCase);
            _mapPreview.EnableInClassList("is-empty", !hasMap);
            _mapParticipantsButton.EnableInClassList("is-read-only", isReadOnly);
            SetVisible(_mapParticipantsButton, hasMap);
            SetVisible(_mapParticipantsTitle, hasMap && !isReadOnly);
            SetVisible(_mapScanTimeLabel, hasMap);
            SetVisible(_mapReconstructionButton, hasMap);
            SetVisible(_mapEmptyState, !hasMap);
            SetVisible(_mapCreateMemoActions, hasMap && !isReadOnly);

            if (!hasMap)
            {
                _mapCurrentSpaceLabel.text = "3D MAP";
                _mapCurrentAddressLabel.text = string.Empty;
                _mapScanTimeLabel.text = string.Empty;
                _mapReconstructionButton.text = string.Empty;
                _mapParticipantsManagerSummary.text = string.Empty;
                _mapParticipantsRepairerSummary.text = string.Empty;
                return;
            }

            _mapCurrentSpaceLabel.text = selectedMap.spaceName;
            _mapCurrentAddressLabel.text = GetMapAddressKey(selectedMap);
            _mapScanTimeLabel.text = $"스캔일시 : {FormatScanTime(selectedMap.scanCreatedAt)}";
            ApplyMapReconstructionState(selectedMap);
            if (isReadOnly)
            {
                _mapParticipantsManagerSummary.text = string.Empty;
                _mapParticipantsRepairerSummary.text = "읽기모드";
            }
            else
            {
                RefreshMapParticipantsSummary(selectedMap);
            }
            ApplyMapParticipantsInvite(selectedMap);
        }

        private void ApplyMapReconstructionState(ScanMapItem map)
        {
            string state = map.reconstructionState?.Trim().ToLowerInvariant() ?? string.Empty;
            _mapReconstructionButton.EnableInClassList("is-failed", state == "failed");
            _mapReconstructionButton.text = state switch
            {
                "done" => "3D MAP 보기",
                "queued" => "3D MAP 생성 대기 중",
                "uploading" => "3D MAP 업로드 중",
                "processing" => "3D MAP 생성 중",
                "failed" => "3D MAP 생성 실패",
                _ => "스캔 결과 없음"
            };
        }

        private void OpenSelectedMapReconstruction()
        {
            ScanMapItem map = GetSelectedMap();
            if (string.IsNullOrWhiteSpace(map.reconstructionScanId))
            {
                PopupManager.ShowMessage("3D MAP", "저장된 스캔 결과가 없습니다.", "확인");
                return;
            }

            if (string.Equals(map.reconstructionState, "failed", StringComparison.OrdinalIgnoreCase))
            {
                string message = string.IsNullOrWhiteSpace(map.reconstructionMessage)
                    ? "서버에서 맵 생성에 실패했습니다."
                    : map.reconstructionMessage;
                PopupManager.ShowMessage("3D MAP 생성 실패", message, "확인");
                return;
            }

            _ = OpenSelectedMapReconstructionAsync(map);
        }

        private async Awaitable OpenSelectedMapReconstructionAsync(ScanMapItem map)
        {
            MapScanSession.BeginResultView(map.id, map.reconstructionScanId, map.reconstructionResultFile);
            SceneHistoryManager.SaveCurrentScene();
            await _fadeTransition.FadeOutAndLoadSceneAsync(MapScanSession.SCAN_SCENE_NAME);
        }

        private void RefreshMapParticipantsSummary(ScanMapItem map)
        {
            var managerNames = new List<string>();
            var repairerNames = new List<string>();
            foreach (ScanMapMemberItem member in map.members)
            {
                if (string.Equals(member.role, "read-only", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string name = string.IsNullOrWhiteSpace(member.name) ? member.playerId : member.name;
                if (string.Equals(member.role, "manager", StringComparison.OrdinalIgnoreCase))
                {
                    managerNames.Add(name);
                }
                else
                {
                    repairerNames.Add(name);
                }
            }
            _mapParticipantsManagerSummary.text = string.Join(",", managerNames) + (managerNames.Count > 0 && repairerNames.Count > 0 ? "," : string.Empty);
            _mapParticipantsRepairerSummary.text = string.Join(",", repairerNames);
        }

        private void ShowMapParticipants()
        {
            ScanMapItem map = GetSelectedMap();
            if (map == null || string.Equals(map.currentUserRole, "read-only", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            RebuildMapParticipants(map);
            ApplyMapParticipantsInvite(map);
            if (!string.IsNullOrWhiteSpace(_mapParticipantsInviteCode) && _mapParticipantsInviteExpiresAt > DateTimeOffset.UtcNow)
            {
                _mapParticipantsInviteSchedule.Resume();
            }
            _mapParticipantsTransitionToken++;
            _mapParticipantsOverlay.RemoveFromClassList(HIDDEN_CLASS);
            _mapParticipantsOverlay.RemoveFromClassList(DIALOG_OPEN_CLASS);
            int token = _mapParticipantsTransitionToken;
            _mapParticipantsOverlay.schedule.Execute(() =>
            {
                if (token == _mapParticipantsTransitionToken)
                {
                    _mapParticipantsOverlay.AddToClassList(DIALOG_OPEN_CLASS);
                }
            }).ExecuteLater(16);
        }

        private async Awaitable OpenReadOnlyMapAsync(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                PopupManager.ShowMessage("참여 코드", "참여 코드를 입력해주세요.", "확인");
                return;
            }

            LoadingSpinnerController.ShowOverlay(_mainLoadingOverlay, _mainLoadingSpinner);
            try
            {
                ReadOnlyMapResponse response = await _scanMapService.OpenReadOnlyMapAsync(code.Trim());
                if (response?.map == null)
                {
                    PopupManager.ShowMessage("참여 코드", "유효한 참여 코드를 찾지 못했습니다.", "확인");
                    return;
                }

                HideScanActionDialog();
                if (_friendsInitialized && FriendsService.Instance.Friends.Any(relationship =>
                    string.Equals(relationship.Member.Id, response.creatorPlayerId, StringComparison.OrdinalIgnoreCase)))
                {
                    await JoinMapAsMemberAsync(code.Trim());
                    return;
                }
                PopupManager.ShowConfirm("읽기모드", "해당 프로젝트 생성자와 친구가 아닙니다.\n읽기모드로 보시겠습니까?", "친구 요청 보내기", "네",
                    () => _ = RequestReadOnlyMapCreatorFriendAsync(response.creatorPlayerId),
                    () => _ = JoinMapAsReaderAsync(code.Trim()));
            }
            finally
            {
                LoadingSpinnerController.HideOverlay(_mainLoadingOverlay, _mainLoadingSpinner);
            }
        }

        private async Awaitable JoinMapAsReaderAsync(string code)
        {
            LoadingSpinnerController.ShowOverlay(_mainLoadingOverlay, _mainLoadingSpinner);
            try
            {
                ReadOnlyMapResponse response = await _scanMapService.OpenReadOnlyMapAsync(code, false, true);
                if (response?.map == null)
                {
                    PopupManager.ShowMessage("읽기모드", "읽기모드로 참여하지 못했습니다.", "확인");
                    return;
                }

                EnterReadOnlyMap(response);
            }
            finally
            {
                LoadingSpinnerController.HideOverlay(_mainLoadingOverlay, _mainLoadingSpinner);
            }
        }

        private async Awaitable JoinMapAsMemberAsync(string code)
        {
            ReadOnlyMapResponse response = await _scanMapService.OpenReadOnlyMapAsync(code, true);
            if (response?.map == null)
            {
                PopupManager.ShowMessage("맵 참여", "맵에 참여하지 못했습니다.", "확인");
                return;
            }

            _readOnlyMap = null;
            _readOnlyMemos.Clear();
            _scanMaps.RemoveAll(map => map.id == response.map.id);
            _scanMaps.Add(response.map);
            _selectedMapId = response.map.id;
            ApplySelectedMap();
            RequestTabSwitch(3);
        }

        private async Awaitable RequestReadOnlyMapCreatorFriendAsync(string creatorPlayerId)
        {
            if (string.IsNullOrWhiteSpace(creatorPlayerId))
            {
                return;
            }

            try
            {
                await FriendsService.Instance.AddFriendAsync(creatorPlayerId);
                PopupManager.ShowMessage("친구 요청", "프로젝트 생성자에게 친구 요청을 보냈습니다.", "확인");
            }
            catch (Exception exception) when (IsFriendsRecoverableException(exception))
            {
                PopupManager.ShowMessage("친구 요청", "친구 요청을 보내지 못했습니다.", "확인");
            }
        }

        private void EnterReadOnlyMap(ReadOnlyMapResponse response)
        {
            _readOnlyMap = response.map;
            _readOnlyMemos.Clear();
            _readOnlyMemos.AddRange(response.memos);
            _scanMaps.RemoveAll(map => map.id == _readOnlyMap.id);
            _scanMaps.Add(_readOnlyMap);
            _selectedMapId = _readOnlyMap.id;
            ApplySelectedMap();
            RequestTabSwitch(3);
        }

        private void HideMapParticipants()
        {
            _mapParticipantsTransitionToken++;
            int token = _mapParticipantsTransitionToken;
            _mapParticipantsOverlay.RemoveFromClassList(DIALOG_OPEN_CLASS);
            _mapParticipantsInviteSchedule.Pause();
            _mapParticipantsOverlay.schedule.Execute(() =>
            {
                if (token == _mapParticipantsTransitionToken)
                {
                    _mapParticipantsOverlay.AddToClassList(HIDDEN_CLASS);
                }
            }).ExecuteLater(240);
        }

        private void ShowMapFriendInvite()
        {
            _ = ShowMapFriendInviteAsync();
        }

        private async Awaitable ShowMapFriendInviteAsync()
        {
            _selectedMapFriendInvitePlayerIds.Clear();
            _mapFriendInviteSubmitButton.SetEnabled(false);
            _mapFriendInviteTransitionToken++;
            _mapFriendInviteOverlay.RemoveFromClassList(HIDDEN_CLASS);
            _mapFriendInviteOverlay.RemoveFromClassList(DIALOG_OPEN_CLASS);
            int token = _mapFriendInviteTransitionToken;
            _mapFriendInviteOverlay.schedule.Execute(() =>
            {
                if (token == _mapFriendInviteTransitionToken)
                {
                    _mapFriendInviteOverlay.AddToClassList(DIALOG_OPEN_CLASS);
                }
            }).ExecuteLater(16);

            if (!_friendsInitialized)
            {
                await InitializeFriendsAsync();
            }
            if (token != _mapFriendInviteTransitionToken)
            {
                return;
            }

            BuildAvailableMapFriendInviteRelationships();
            _mapFriendInviteVisibleCount = Mathf.Min(MAP_FRIEND_INVITE_PAGE_SIZE, _availableMapFriendInviteRelationships.Count);
            await LoadMapFriendInviteProfilesAsync(0, _mapFriendInviteVisibleCount);
            if (token != _mapFriendInviteTransitionToken)
            {
                return;
            }
            RebuildMapFriendInviteList();
        }

        private void HideMapFriendInvite()
        {
            _mapFriendInviteTransitionToken++;
            int token = _mapFriendInviteTransitionToken;
            _mapFriendInviteOverlay.RemoveFromClassList(DIALOG_OPEN_CLASS);
            _mapFriendInviteOverlay.schedule.Execute(() =>
            {
                if (token == _mapFriendInviteTransitionToken)
                {
                    _mapFriendInviteOverlay.AddToClassList(HIDDEN_CLASS);
                }
            }).ExecuteLater(240);
        }

        private void RebuildMapFriendInviteList()
        {
            _mapFriendInviteList.Clear();
            for (int i = 0; i < _mapFriendInviteVisibleCount; i++)
            {
                AddMapFriendInviteItem(_availableMapFriendInviteRelationships[i]);
            }

            if (_availableMapFriendInviteRelationships.Count == 0)
            {
                AddMapFriendInviteStatus(FriendsService.Instance.Friends.Count == 0
                    ? "등록된 친구가 없습니다."
                    : "초대할 수 있는 친구가 없습니다.");
            }
            SetVisible(_mapFriendInviteLoadMoreButton, _mapFriendInviteVisibleCount < _availableMapFriendInviteRelationships.Count);
        }

        private void BuildAvailableMapFriendInviteRelationships()
        {
            _availableMapFriendInviteRelationships.Clear();
            ScanMapItem map = GetSelectedMap();
            var participatingPlayerIds = new HashSet<string>(map.members
                .Where(member => !string.Equals(member.role, "read-only", StringComparison.OrdinalIgnoreCase))
                .Select(member => member.playerId), StringComparer.OrdinalIgnoreCase);
            foreach (Relationship relationship in FriendsService.Instance.Friends)
            {
                if (!participatingPlayerIds.Contains(relationship.Member.Id))
                {
                    _availableMapFriendInviteRelationships.Add(relationship);
                }
            }
        }

        private async Awaitable LoadMapFriendInviteProfilesAsync(int startIndex, int count)
        {
            if (count <= 0)
            {
                return;
            }

            var playerIds = _availableMapFriendInviteRelationships
                .Skip(startIndex)
                .Take(count)
                .Select(relationship => relationship.Member.Id)
                .ToList();
            LoadingSpinnerController.ShowOverlay(_mainLoadingOverlay, _mainLoadingSpinner);
            try
            {
                MapFriendProfilesResponse response = await _scanMapService.LoadFriendProfilesAsync(playerIds);
                if (response == null)
                {
                    return;
                }
                foreach (MapFriendProfileItem profile in response.profiles)
                {
                    _mapFriendInviteProfiles[profile.playerId] = profile;
                }
            }
            finally
            {
                LoadingSpinnerController.HideOverlay(_mainLoadingOverlay, _mainLoadingSpinner);
            }
        }

        private void OnClickMapFriendInviteLoadMore()
        {
            _ = LoadMoreMapFriendInvitesAsync();
        }

        private async Awaitable LoadMoreMapFriendInvitesAsync()
        {
            int startIndex = _mapFriendInviteVisibleCount;
            int nextCount = Mathf.Min(MAP_FRIEND_INVITE_PAGE_SIZE, _availableMapFriendInviteRelationships.Count - startIndex);
            await LoadMapFriendInviteProfilesAsync(startIndex, nextCount);
            _mapFriendInviteVisibleCount += nextCount;
            RebuildMapFriendInviteList();
        }

        private void AddMapFriendInviteItem(Relationship relationship)
        {
            TemplateContainer template = _mapFriendInviteItemAsset.Instantiate();
            Button item = template.Q<Button>("map-friend-invite-item");
            Label companyLabel = template.Q<Label>("map-friend-invite-company");
            string playerId = relationship.Member.Id;
            _mapFriendInviteProfiles.TryGetValue(playerId, out MapFriendProfileItem profile);
            template.Q<Label>("map-friend-invite-name").text = string.IsNullOrWhiteSpace(profile?.name)
                ? GetMemberDisplayName(relationship.Member)
                : profile.name;
            companyLabel.text = profile?.companyName ?? string.Empty;
            item.EnableInClassList(SELECTED_CLASS, _selectedMapFriendInvitePlayerIds.Contains(playerId));
            item.clicked += () =>
            {
                if (!_selectedMapFriendInvitePlayerIds.Remove(playerId))
                {
                    _selectedMapFriendInvitePlayerIds.Add(playerId);
                }
                item.EnableInClassList(SELECTED_CLASS, _selectedMapFriendInvitePlayerIds.Contains(playerId));
                _mapFriendInviteSubmitButton.SetEnabled(_selectedMapFriendInvitePlayerIds.Count > 0);
            };
            _mapFriendInviteList.Add(item);
        }

        private void AddMapFriendInviteStatus(string message)
        {
            TemplateContainer template = _mapFriendInviteItemAsset.Instantiate();
            Button item = template.Q<Button>("map-friend-invite-item");
            template.Q<Label>("map-friend-invite-name").text = message;
            SetVisible(template.Q<Label>("map-friend-invite-company"), false);
            SetVisible(template.Q<VisualElement>("map-friend-invite-check"), false);
            item.SetEnabled(false);
            _mapFriendInviteList.Add(item);
        }

        private void OnClickMapFriendInviteSubmit()
        {
            _ = InviteSelectedMapFriendsAsync();
        }

        private async Awaitable InviteSelectedMapFriendsAsync()
        {
            if (_selectedMapFriendInvitePlayerIds.Count == 0)
            {
                return;
            }

            ScanMapItem map = GetSelectedMap();
            var members = new List<InviteMapMemberRequestItem>(_selectedMapFriendInvitePlayerIds.Count);
            foreach (Relationship relationship in _availableMapFriendInviteRelationships)
            {
                string playerId = relationship.Member.Id;
                if (!_selectedMapFriendInvitePlayerIds.Contains(playerId))
                {
                    continue;
                }

                _mapFriendInviteProfiles.TryGetValue(playerId, out MapFriendProfileItem profile);
                members.Add(new InviteMapMemberRequestItem
                {
                    playerId = playerId,
                    name = string.IsNullOrWhiteSpace(profile?.name) ? GetMemberDisplayName(relationship.Member) : profile.name,
                    companyName = profile?.companyName ?? string.Empty
                });
            }
            LoadingSpinnerController.ShowOverlay(_mainLoadingOverlay, _mainLoadingSpinner);
            try
            {
                ScanMapListResponse response = await _scanMapService.InviteMembersAsync(map.id, members);
                if (response == null)
                {
                    PopupManager.ShowMessage("멤버 초대 실패", "선택한 친구를 초대하지 못했습니다.", "확인");
                    return;
                }

                ApplyMapMemberMutationResponse(response, "멤버 초대 실패");
                HideMapFriendInvite();
            }
            finally
            {
                LoadingSpinnerController.HideOverlay(_mainLoadingOverlay, _mainLoadingSpinner);
            }
        }

        private void RebuildMapParticipants(ScanMapItem map)
        {
            _mapParticipantsList.Clear();
            bool canManage = string.Equals(map.currentUserRole, "manager", StringComparison.OrdinalIgnoreCase);
            SetVisible(_mapParticipantsInviteButton, canManage);
            SetVisible(_mapParticipantsAddButton, canManage);
            foreach (ScanMapMemberItem member in map.members)
            {
                TemplateContainer template = _mapParticipantItemAsset.Instantiate();
                VisualElement item = template.Q<VisualElement>("map-participant-item");
                bool isManager = string.Equals(member.role, "manager", StringComparison.OrdinalIgnoreCase);
                bool isReadOnly = string.Equals(member.role, "read-only", StringComparison.OrdinalIgnoreCase);
                item.EnableInClassList("is-manager", isManager);
                template.Q<Label>("map-participant-name").text = string.IsNullOrWhiteSpace(member.name) ? member.playerId : member.name;
                template.Q<Label>("map-participant-company").text = member.companyName;
                template.Q<Label>("map-participant-role").text = isManager ? "관리자" : isReadOnly ? "읽기모드" : string.Empty;

                Button promoteButton = template.Q<Button>("map-participant-promote-button");
                Button removeButton = template.Q<Button>("map-participant-remove-button");
                SetVisible(promoteButton, canManage && !isManager);
                SetVisible(removeButton, canManage && !string.Equals(member.playerId, AuthenticationService.Instance.PlayerId, StringComparison.OrdinalIgnoreCase));
                promoteButton.clicked += () => RequestPromoteMapParticipant(member);
                removeButton.clicked += () => RequestRemoveMapParticipant(member);
                _mapParticipantsList.Add(item);
            }
        }

        private void ApplyMapParticipantsInvite(ScanMapItem map)
        {
            _mapParticipantsInviteCode = map.inviteCode ?? string.Empty;
            _mapParticipantsInviteExpiresAt = DateTimeOffset.TryParse(map.inviteCodeExpiresAt, out DateTimeOffset expiresAt)
                ? expiresAt
                : DateTimeOffset.MinValue;
            UpdateMapParticipantsInviteTimer();
        }

        private void UpdateMapParticipantsInviteTimer()
        {
            TimeSpan remaining = _mapParticipantsInviteExpiresAt - DateTimeOffset.UtcNow;
            bool isActive = !string.IsNullOrWhiteSpace(_mapParticipantsInviteCode) && remaining > TimeSpan.Zero;
            SetVisible(_mapParticipantsInviteIssueLabel, !isActive);
            SetVisible(_mapParticipantsInviteCodeContent, isActive);
            if (!isActive)
            {
                _mapParticipantsInviteCode = string.Empty;
                _mapParticipantsInviteSchedule.Pause();
                return;
            }

            int seconds = Mathf.Max(0, Mathf.CeilToInt((float)remaining.TotalSeconds));
            _mapParticipantsInviteTimer.text = $"{seconds / 60:00}:{seconds % 60:00}";
            _mapParticipantsInviteCodeLabel.text = _mapParticipantsInviteCode;
        }

        private void OnClickMapParticipantsInvite()
        {
            if (!string.IsNullOrWhiteSpace(_mapParticipantsInviteCode) && _mapParticipantsInviteExpiresAt > DateTimeOffset.UtcNow)
            {
                GUIUtility.systemCopyBuffer = _mapParticipantsInviteCode;
                PopupManager.ShowMessage("초대 코드 복사", "초대 코드를 클립보드에 복사했습니다.", "확인");
                return;
            }
            _ = IssueMapInviteAsync();
        }

        private async Awaitable IssueMapInviteAsync()
        {
            ScanMapItem map = GetSelectedMap();
            if (map == null)
            {
                return;
            }

            LoadingSpinnerController.ShowOverlay(_mainLoadingOverlay, _mainLoadingSpinner);
            try
            {
                MapInviteResponse response = await _scanMapService.IssueInviteAsync(map.id);
                if (response == null || !DateTimeOffset.TryParse(response.expiresAt, out DateTimeOffset expiresAt))
                {
                    PopupManager.ShowMessage("초대 코드 발급 실패", "초대 코드를 발급하지 못했습니다.", "확인");
                    return;
                }
                map.inviteCode = response.code;
                map.inviteCodeExpiresAt = response.expiresAt;
                _mapParticipantsInviteCode = response.code;
                _mapParticipantsInviteExpiresAt = expiresAt;
                UpdateMapParticipantsInviteTimer();
                _mapParticipantsInviteSchedule.Resume();
            }
            finally
            {
                LoadingSpinnerController.HideOverlay(_mainLoadingOverlay, _mainLoadingSpinner);
            }
        }

        private void RequestPromoteMapParticipant(ScanMapMemberItem member)
        {
            bool isReadOnly = string.Equals(member.role, "read-only", StringComparison.OrdinalIgnoreCase);
            string title = isReadOnly ? "수리자 전환" : "관리자 지정";
            string message = isReadOnly
                ? $"{member.name} 님을 수리자로 전환합니다."
                : $"{member.name} 님을 관리자로 지정합니다.";
            PopupManager.ShowConfirm(title, message, "취소", isReadOnly ? "전환" : "지정", () => _ = PromoteMapParticipantAsync(member));
        }

        private async Awaitable PromoteMapParticipantAsync(ScanMapMemberItem member)
        {
            ScanMapItem map = GetSelectedMap();
            LoadingSpinnerController.ShowOverlay(_mainLoadingOverlay, _mainLoadingSpinner);
            try
            {
                ScanMapListResponse response = await _scanMapService.PromoteMemberAsync(map.id, member.playerId);
                ApplyMapMemberMutationResponse(response, "관리자 변경 실패");
            }
            finally
            {
                LoadingSpinnerController.HideOverlay(_mainLoadingOverlay, _mainLoadingSpinner);
            }
        }

        private void RequestRemoveMapParticipant(ScanMapMemberItem member)
        {
            PopupManager.ShowConfirm("참여자 삭제", $"{member.name} 님을 맵에서 삭제합니다.", "취소", "삭제", () => _ = RemoveMapParticipantAsync(member));
        }

        private async Awaitable RemoveMapParticipantAsync(ScanMapMemberItem member)
        {
            ScanMapItem map = GetSelectedMap();
            LoadingSpinnerController.ShowOverlay(_mainLoadingOverlay, _mainLoadingSpinner);
            try
            {
                ScanMapListResponse response = await _scanMapService.RemoveMemberAsync(map.id, member.playerId);
                ApplyMapMemberMutationResponse(response, "참여자 삭제 실패");
            }
            finally
            {
                LoadingSpinnerController.HideOverlay(_mainLoadingOverlay, _mainLoadingSpinner);
            }
        }

        private void ApplyMapMemberMutationResponse(ScanMapListResponse response, string failureTitle)
        {
            if (response == null)
            {
                PopupManager.ShowMessage(failureTitle, "서버 요청을 처리하지 못했습니다.", "확인");
                return;
            }

            _scanMaps.Clear();
            _scanMaps.AddRange(response.maps);
            RebuildMapList();
            ApplySelectedMap();
            RebuildMemoList();
            ScanMapItem map = GetSelectedMap();
            if (map != null)
            {
                RebuildMapParticipants(map);
            }
        }

        private void OnClickMapCreateTextMemo()
        {
            ShowMapMemoCreatePageForSelectedMap("text");
        }

        private void OnClickMapCreateChecklistMemo()
        {
            ShowMapMemoCreatePageForSelectedMap("checklist");
        }

        private void OnClickMapCreateMediaMemo()
        {
            ShowMapMemoCreatePageForSelectedMap("image");
        }

        private void OnClickMapCreateVoiceMemo()
        {
            ShowMapMemoCreatePageForSelectedMap("voice");
        }

        private void ShowMapMemoCreatePageForSelectedMap(string kind)
        {
            ScanMapItem selectedMap = GetSelectedMap();
            if (selectedMap == null)
            {
                return;
            }

            ShowMapMemoCreatePage(selectedMap, kind);
        }

        private ScanMapItem GetSelectedMap()
        {
            return _scanMaps.Find(map => map.id == _selectedMapId);
        }

        private static string GetMapAddressKey(ScanMapItem map)
        {
            return string.IsNullOrWhiteSpace(map.roadAddress) ? map.address : map.roadAddress;
        }

        private static string FormatScanTime(string scanCreatedAt)
        {
            if (!DateTimeOffset.TryParse(scanCreatedAt, out DateTimeOffset parsed))
            {
                return string.Empty;
            }

            return parsed.LocalDateTime.ToString("yyyy/MM/dd h:mm tt", CultureInfo.InvariantCulture);
        }
    }
}
