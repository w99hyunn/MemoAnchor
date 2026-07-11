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
        private Button _mapListButton, _mapPreviewMenuButton, _mapCreateTextMemoButton, _mapCreateChecklistMemoButton, _mapCreateMediaMemoButton, _mapCreateVoiceMemoButton;
        private Button _mapParticipantsButton, _mapParticipantsInviteButton, _mapParticipantsAddButton;
        private Button _mapFriendInviteBackButton, _mapFriendInviteSubmitButton, _mapFriendInviteLoadMoreButton;
        private VisualElement _mapPreview, _mapListOverlay, _mapListSheet, _mapListContent, _mapEmptyState, _mapCreateMemoActions;
        private VisualElement _mapParticipantsOverlay, _mapParticipantsSheet, _mapParticipantsList, _mapParticipantsInviteCodeContent;
        private VisualElement _mapFriendInviteOverlay, _mapFriendInviteSheet, _mapFriendInviteList;
        private Label _mapCurrentSpaceLabel, _mapCurrentAddressLabel, _mapScanTimeLabel, _mapParticipantsManagerSummary, _mapParticipantsRepairerSummary;
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
            _mapPreviewMenuButton = _root.Q<Button>("map-preview-menu-button");
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
            _mapListOverlay = _root.Q<VisualElement>("map-list-overlay");
            _mapListSheet = _root.Q<VisualElement>("map-list-sheet");
            _mapListContent = _root.Q<VisualElement>("map-list-content");
            _mapCurrentSpaceLabel = _root.Q<Label>("map-current-space-label");
            _mapCurrentAddressLabel = _root.Q<Label>("map-current-address-label");
            _mapScanTimeLabel = _root.Q<Label>("map-scan-time-label");
            _mapEmptyState = _root.Q<VisualElement>("map-empty-state");
            _mapParticipantsOverlay = _root.Q<VisualElement>("map-participants-overlay");
            _mapParticipantsSheet = _root.Q<VisualElement>("map-participants-sheet");
            _mapParticipantsList = _root.Q<VisualElement>("map-participants-list");
            _mapParticipantsInviteCodeContent = _root.Q<VisualElement>("map-participants-invite-code-content");
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
            _mapPreviewMenuButton.clicked += ShowMapList;
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
            _mapPreviewMenuButton.clicked -= ShowMapList;
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
                _scanMaps.Clear();
                _scanMaps.AddRange(response.maps);

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
            }
            finally
            {
                _isMapListLoading = false;
            }
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
            _mapPreview.EnableInClassList("is-empty", !hasMap);
            SetVisible(_mapParticipantsButton, hasMap);
            SetVisible(_mapScanTimeLabel, hasMap);
            SetVisible(_mapEmptyState, !hasMap);
            SetVisible(_mapCreateMemoActions, hasMap);

            if (!hasMap)
            {
                _mapCurrentSpaceLabel.text = "3D MAP";
                _mapCurrentAddressLabel.text = string.Empty;
                _mapScanTimeLabel.text = string.Empty;
                _mapParticipantsManagerSummary.text = string.Empty;
                _mapParticipantsRepairerSummary.text = string.Empty;
                return;
            }

            _mapCurrentSpaceLabel.text = selectedMap.spaceName;
            _mapCurrentAddressLabel.text = GetMapAddressKey(selectedMap);
            _mapScanTimeLabel.text = $"스캔일시 : {FormatScanTime(selectedMap.scanCreatedAt)}";
            RefreshMapParticipantsSummary(selectedMap);
            ApplyMapParticipantsInvite(selectedMap);
        }

        private void RefreshMapParticipantsSummary(ScanMapItem map)
        {
            var managerNames = new List<string>();
            var repairerNames = new List<string>();
            foreach (ScanMapMemberItem member in map.members)
            {
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
            if (map == null)
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
            var participatingPlayerIds = new HashSet<string>(map.members.Select(member => member.playerId), StringComparer.OrdinalIgnoreCase);
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
            LoadingSpinnerController.ShowOverlay(_memoLoadingOverlay, _memoLoadingSpinner);
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
                LoadingSpinnerController.HideOverlay(_memoLoadingOverlay, _memoLoadingSpinner);
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
            LoadingSpinnerController.ShowOverlay(_memoLoadingOverlay, _memoLoadingSpinner);
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
                LoadingSpinnerController.HideOverlay(_memoLoadingOverlay, _memoLoadingSpinner);
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
                item.EnableInClassList("is-manager", isManager);
                template.Q<Label>("map-participant-name").text = string.IsNullOrWhiteSpace(member.name) ? member.playerId : member.name;
                template.Q<Label>("map-participant-company").text = member.companyName;
                template.Q<Label>("map-participant-role").text = isManager ? "관리자" : string.Empty;

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

            LoadingSpinnerController.ShowOverlay(_memoLoadingOverlay, _memoLoadingSpinner);
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
                LoadingSpinnerController.HideOverlay(_memoLoadingOverlay, _memoLoadingSpinner);
            }
        }

        private void RequestPromoteMapParticipant(ScanMapMemberItem member)
        {
            PopupManager.ShowConfirm("관리자 지정", $"{member.name} 님을 관리자로 지정합니다.", "취소", "지정", () => _ = PromoteMapParticipantAsync(member));
        }

        private async Awaitable PromoteMapParticipantAsync(ScanMapMemberItem member)
        {
            ScanMapItem map = GetSelectedMap();
            LoadingSpinnerController.ShowOverlay(_memoLoadingOverlay, _memoLoadingSpinner);
            try
            {
                ScanMapListResponse response = await _scanMapService.PromoteMemberAsync(map.id, member.playerId);
                ApplyMapMemberMutationResponse(response, "관리자 변경 실패");
            }
            finally
            {
                LoadingSpinnerController.HideOverlay(_memoLoadingOverlay, _memoLoadingSpinner);
            }
        }

        private void RequestRemoveMapParticipant(ScanMapMemberItem member)
        {
            PopupManager.ShowConfirm("참여자 삭제", $"{member.name} 님을 맵에서 삭제합니다.", "취소", "삭제", () => _ = RemoveMapParticipantAsync(member));
        }

        private async Awaitable RemoveMapParticipantAsync(ScanMapMemberItem member)
        {
            ScanMapItem map = GetSelectedMap();
            LoadingSpinnerController.ShowOverlay(_memoLoadingOverlay, _memoLoadingSpinner);
            try
            {
                ScanMapListResponse response = await _scanMapService.RemoveMemberAsync(map.id, member.playerId);
                ApplyMapMemberMutationResponse(response, "참여자 삭제 실패");
            }
            finally
            {
                LoadingSpinnerController.HideOverlay(_memoLoadingOverlay, _memoLoadingSpinner);
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
