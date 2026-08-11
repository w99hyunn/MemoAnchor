using System;
using System.Collections.Generic;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Friends;
using Unity.Services.Friends.Exceptions;
using Unity.Services.Friends.Models;
using Unity.Services.Friends.Notifications;
using UnityEngine;
using UnityEngine.UIElements;

namespace MemoAnchor.UI
{
    public partial class MainView
    {
        [SerializeField] private VisualTreeAsset _alertRequestItemAsset;

        private bool _friendsInitialized;
        private bool _friendsCallbacksRegistered;
        private bool _isFriendsInitializing;
        private bool _isFriendsRefreshing;
        private bool _friendsRefreshQueued;
        private bool _isFriendRequestSubmitting;
        private bool _friendsInitializationFailed;

        public event Action FriendRequestAlertsChanged;
        public event Action FriendsCacheChanged;
        public bool AreFriendsInitialized => _friendsInitialized;
        public bool DidFriendsInitializationFail => _friendsInitializationFailed;

        public void EnsureFriendsInitialized()
        {
            _ = InitializeFriendsAsync();
        }

        private async Awaitable InitializeFriendsAsync()
        {
            if (_friendsInitialized || _isFriendsInitializing)
            {
                return;
            }

            _isFriendsInitializing = true;
            _friendsInitializationFailed = false;

            try
            {
                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    return;
                }

                _ = EnsurePlayerNameAsync();
                await FriendsService.Instance.InitializeAsync();
                _friendsInitialized = true;
                RegisterFriendsCallbacks();
                RebuildProfileFriendList();
                RebuildOpenAlertFriendRequests();
                FriendsCacheChanged?.Invoke();

                await RefreshFriendProfilesAsync();
                RebuildProfileFriendList();
                FriendsCacheChanged?.Invoke();
            }
            catch (Exception exception) when (IsFriendsRecoverableException(exception))
            {
                Debug.LogWarning($"UGS Friends initialization failed: {exception.Message}");
                _friendsInitializationFailed = true;
                RebuildProfileFriendList();
                FriendsCacheChanged?.Invoke();
            }
            finally
            {
                _isFriendsInitializing = false;
            }
        }

        private void RegisterFriendsCallbacks()
        {
            if (_friendsCallbacksRegistered)
            {
                return;
            }

            FriendsService.Instance.RelationshipAdded += HandleRelationshipAdded;
            FriendsService.Instance.RelationshipDeleted += HandleRelationshipDeleted;
            FriendsService.Instance.PresenceUpdated += HandlePresenceUpdated;
            _friendsCallbacksRegistered = true;
        }

        private void UnregisterFriendsCallbacks()
        {
            if (!_friendsCallbacksRegistered)
            {
                return;
            }

            FriendsService.Instance.RelationshipAdded -= HandleRelationshipAdded;
            FriendsService.Instance.RelationshipDeleted -= HandleRelationshipDeleted;
            FriendsService.Instance.PresenceUpdated -= HandlePresenceUpdated;
            _friendsCallbacksRegistered = false;
        }

        private void HandleRelationshipAdded(IRelationshipAddedEvent relationshipEvent)
        {
            RebuildProfileFriendList();
            RebuildOpenAlertFriendRequests();
            FriendsCacheChanged?.Invoke();
            _ = RefreshFriendsAsync();
        }

        private void HandleRelationshipDeleted(IRelationshipDeletedEvent relationshipEvent)
        {
            RebuildProfileFriendList();
            RebuildOpenAlertFriendRequests();
            FriendsCacheChanged?.Invoke();
            _ = RefreshFriendsAsync();
        }

        private void HandlePresenceUpdated(IPresenceUpdatedEvent presenceEvent)
        {
            RebuildProfileFriendList();
        }

        private async Awaitable RefreshFriendsAsync()
        {
            if (!_friendsInitialized)
            {
                return;
            }

            if (_isFriendsRefreshing)
            {
                _friendsRefreshQueued = true;
                return;
            }

            do
            {
                _friendsRefreshQueued = false;
                _isFriendsRefreshing = true;

                try
                {
                    await RefreshFriendProfilesAsync();
                    RebuildProfileFriendList();
                    RebuildOpenAlertFriendRequests();
                    FriendsCacheChanged?.Invoke();
                }
                catch (Exception exception) when (IsFriendsRecoverableException(exception))
                {
                    Debug.LogWarning($"UGS Friends refresh failed: {exception.Message}");
                }
                finally
                {
                    _isFriendsRefreshing = false;
                }
            } while (_friendsRefreshQueued);
        }

        private async Awaitable RefreshFriendProfilesAsync()
        {
            var refreshedProfiles = new Dictionary<string, MapFriendProfileItem>(StringComparer.OrdinalIgnoreCase);
            var playerIds = new System.Collections.Generic.List<string>(MAP_FRIEND_INVITE_PAGE_SIZE);
            foreach (Relationship relationship in FriendsService.Instance.Friends)
            {
                playerIds.Add(relationship.Member.Id);
                if (playerIds.Count < MAP_FRIEND_INVITE_PAGE_SIZE)
                {
                    continue;
                }

                if (!await LoadFriendProfilesAsync(playerIds, refreshedProfiles))
                {
                    return;
                }
                playerIds.Clear();
            }

            if (playerIds.Count > 0 && !await LoadFriendProfilesAsync(playerIds, refreshedProfiles))
            {
                return;
            }

            _mapFriendInviteProfiles.Clear();
            foreach (KeyValuePair<string, MapFriendProfileItem> profile in refreshedProfiles)
            {
                _mapFriendInviteProfiles[profile.Key] = profile.Value;
            }
        }

        public void CopyCachedFriendOptions(List<ScanFriendOption> destination)
        {
            destination.Clear();
            if (!_friendsInitialized)
            {
                return;
            }

            foreach (Relationship relationship in FriendsService.Instance.Friends)
            {
                string memberId = relationship.Member.Id;
                _mapFriendInviteProfiles.TryGetValue(memberId, out MapFriendProfileItem profile);
                destination.Add(new ScanFriendOption(
                    memberId,
                    string.IsNullOrWhiteSpace(profile?.name) ? GetMemberDisplayName(relationship.Member) : profile.name,
                    profile?.companyName ?? string.Empty));
            }
        }

        private async Awaitable<bool> LoadFriendProfilesAsync(
            System.Collections.Generic.List<string> playerIds,
            Dictionary<string, MapFriendProfileItem> destination)
        {
            MapFriendProfilesResponse response = await _scanMapService.LoadFriendProfilesAsync(playerIds);
            if (response == null)
            {
                return false;
            }

            foreach (MapFriendProfileItem profile in response.profiles)
            {
                destination[profile.playerId] = profile;
            }
            return true;
        }

        private async Awaitable EnsurePlayerNameAsync()
        {
            try
            {
                string playerName = await AuthenticationService.Instance.GetPlayerNameAsync(false);
                if (!string.IsNullOrWhiteSpace(playerName))
                {
                    ApplyPlayerNameWithTag(playerName);
                    return;
                }

                string profileName = BuildPlayerName(MemoAnchor.PlayerSession.Profile.Name);
                if (string.IsNullOrWhiteSpace(profileName))
                {
                    return;
                }

                string updatedPlayerName = await AuthenticationService.Instance.UpdatePlayerNameAsync(profileName);
                ApplyPlayerNameWithTag(updatedPlayerName);
            }
            catch (Exception exception) when (exception is AuthenticationException or RequestFailedException)
            {
                Debug.LogWarning($"UGS PlayerName sync failed: {exception.Message}");
            }
        }

        private void RebuildProfileFriendList()
        {
            _profileFriendItemsList.Clear();

            if (!_friendsInitialized)
            {
                AddProfileFriendStatus("친구 정보를 불러오는 중입니다.");
                return;
            }

            if (FriendsService.Instance.Friends.Count == 0)
            {
                AddProfileFriendStatus("등록된 친구가 없습니다.");
                return;
            }

            foreach (Relationship relationship in FriendsService.Instance.Friends)
            {
                AddProfileFriendRow(relationship);
            }
        }

        private void AddProfileFriendRow(Relationship relationship)
        {
            TemplateContainer template = _memoCreateRepairerRowAsset.Instantiate();
            Button row = template.Q<Button>("memo-create-repairer-row");
            string memberId = relationship.Member.Id;
            _mapFriendInviteProfiles.TryGetValue(memberId, out MapFriendProfileItem profile);
            template.Q<Label>("memo-create-repairer-name").text = string.IsNullOrWhiteSpace(profile?.name)
                ? GetMemberDisplayName(relationship.Member)
                : profile.name;
            template.Q<Label>("memo-create-repairer-company").text = profile?.companyName ?? string.Empty;

            Button deleteButton = new();
            deleteButton.AddToClassList("profile-friend-delete-button");

            VisualElement deleteIcon = new();
            deleteIcon.AddToClassList("profile-friend-delete-icon");
            deleteButton.Add(deleteIcon);

            string memberName = GetMemberDisplayName(relationship.Member);
            deleteButton.clicked += () => ShowFriendDeleteConfirm(memberId, memberName);
            row.Add(deleteButton);

            _profileFriendItemsList.Add(row);
        }

        private void AddProfileFriendStatus(string text)
        {
            VisualElement row = new();
            row.AddToClassList("profile-friend-row");

            Label label = new(text);
            label.AddToClassList("profile-friend-company");
            row.Add(label);

            _profileFriendItemsList.Add(row);
        }

        private void ShowFriendDeleteConfirm(string memberId, string memberName)
        {
            PopupManager.ShowConfirm("친구 삭제", $"{memberName}님을 친구 목록에서 삭제할까요?", "취소", "삭제", () => ConfirmFriendDelete(memberId));
        }

        private void ConfirmFriendDelete(string memberId)
        {
            _ = DeleteFriendAsync(memberId);
        }

        private async Awaitable DeleteFriendAsync(string memberId)
        {
            try
            {
                await FriendsService.Instance.DeleteFriendAsync(memberId);
                await RefreshFriendsAsync();
            }
            catch (Exception exception) when (IsFriendsRecoverableException(exception))
            {
                Debug.LogWarning($"UGS Friends delete failed: {exception.Message}");
            }
        }

        private void RebuildOpenAlertFriendRequests()
        {
            FriendRequestAlertsChanged?.Invoke();
        }

        public int AddFriendRequestAlertsTo(VisualElement alertRequestList)
        {
            if (!_friendsInitialized)
            {
                AddRequestStatus(alertRequestList, "친구 정보를 불러오는 중입니다.");
                return 0;
            }

            if (FriendsService.Instance.IncomingFriendRequests.Count == 0)
            {
                return 0;
            }

            foreach (Relationship request in FriendsService.Instance.IncomingFriendRequests)
            {
                AddFriendRequestAlert(alertRequestList, request);
            }

            return FriendsService.Instance.IncomingFriendRequests.Count;
        }

        private void AddFriendRequestAlert(VisualElement alertRequestList, Relationship request)
        {
            TemplateContainer item = _alertRequestItemAsset.Instantiate();
            string memberId = request.Member.Id;
            string memberName = GetMemberDisplayName(request.Member);

            item.Q<Label>("alert-primary-text").text = $"{memberName}님께서 친구요청을 보냈습니다.";
            item.Q<Label>("alert-secondary-text").style.display = DisplayStyle.None;
            item.Q<Label>("alert-time-text").text = string.Empty;
            item.Q<Button>("alert-reject-button").clicked += () => _ = DeclineFriendRequestAsync(memberId);
            item.Q<Button>("alert-accept-button").clicked += () => _ = AcceptFriendRequestAsync(memberId);

            alertRequestList.Add(item);
        }

        private void AddRequestStatus(VisualElement alertRequestList, string title)
        {
            TemplateContainer item = _alertRequestItemAsset.Instantiate();
            item.Q<Label>("alert-primary-text").text = title;

            Label secondaryText = item.Q<Label>("alert-secondary-text");
            secondaryText.text = string.Empty;
            secondaryText.style.display = DisplayStyle.None;

            item.Q<Label>("alert-time-text").text = string.Empty;
            item.Q<VisualElement>(className: "alert-action-row").style.display = DisplayStyle.None;
            alertRequestList.Add(item);
        }

        private async Awaitable AcceptFriendRequestAsync(string memberId)
        {
            try
            {
                await FriendsService.Instance.AddFriendAsync(memberId);
                await RefreshFriendsAsync();
            }
            catch (Exception exception) when (IsFriendsRecoverableException(exception))
            {
                Debug.LogWarning($"UGS Friends accept failed: {exception.Message}");
            }
        }

        private async Awaitable DeclineFriendRequestAsync(string memberId)
        {
            try
            {
                await FriendsService.Instance.DeleteIncomingFriendRequestAsync(memberId);
                await RefreshFriendsAsync();
            }
            catch (Exception exception) when (IsFriendsRecoverableException(exception))
            {
                Debug.LogWarning($"UGS Friends decline failed: {exception.Message}");
            }
        }

        private static string GetMemberDisplayName(Member member)
        {
            return string.IsNullOrWhiteSpace(member.Profile.Name) ? member.Id : member.Profile.Name;
        }

        private static string BuildPlayerName(string profileName)
        {
            if (string.IsNullOrWhiteSpace(profileName))
            {
                return string.Empty;
            }

            return profileName.Replace(" ", string.Empty);
        }

        private static bool IsFriendsRecoverableException(Exception exception)
        {
            return exception is FriendsServiceException or InvalidOperationException or AuthenticationException or RequestFailedException;
        }

        private void OnProfileFriendAddClicked(ClickEvent evt)
        {
            evt.StopPropagation();
            ShowProfileFriendAddPopup();
        }

        private void ShowProfileFriendAddPopup()
        {
            PopupManager.ShowTextInput(
                "친구 추가",
                "친구코드를 입력해주세요.\r\n(이름#태그 형식)",
                string.Empty,
                "친구 코드 입력",
                "취소",
                "추가",
                ShowMissingFriendCodePopup,
                SubmitFriendRequest);
        }

        private void ShowMissingFriendCodePopup()
        {
            PopupManager.ShowMessage("친구 추가", "친구코드를 입력해주세요.", "확인", ShowProfileFriendAddPopup);
        }

        private void SubmitFriendRequest(string friendCode)
        {
            _ = SubmitFriendRequestAsync(friendCode);
        }

        private async Awaitable SubmitFriendRequestAsync(string friendCode)
        {
            if (_isFriendRequestSubmitting)
            {
                return;
            }

            _isFriendRequestSubmitting = true;
            _profileFriendAddButton.SetEnabled(false);

            try
            {
                if (!_friendsInitialized)
                {
                    await WaitForFriendsInitializationAsync();
                }

                if (!_friendsInitialized)
                {
                    await InitializeFriendsAsync();
                }

                if (!_friendsInitialized)
                {
                    ShowFriendRequestResult("친구 정보를 불러오는 중입니다.");
                    return;
                }

                Relationship relationship = await FriendsService.Instance.AddFriendByNameAsync(friendCode);
                ShowFriendRequestResult(relationship.Type == RelationshipType.Friend ? "이미 친구입니다." : "친구요청을 보냈습니다.");
                await RefreshFriendsAsync();
            }
            catch (Exception exception) when (IsFriendsRecoverableException(exception))
            {
                Debug.LogWarning($"UGS Friends request failed: {exception.Message}");
                ShowFriendRequestResult("친구요청을 보내지 못했습니다.");
            }
            finally
            {
                _isFriendRequestSubmitting = false;
                _profileFriendAddButton.SetEnabled(true);
            }
        }

        private void ApplyPlayerNameWithTag(string playerName)
        {
            if (!string.IsNullOrWhiteSpace(playerName))
            {
                _profileNameLabel.text = playerName;
            }
        }

        private void ShowFriendRequestResult(string message)
        {
            PopupManager.ShowMessage("친구 추가", message, "확인");
        }

        private async Awaitable WaitForFriendsInitializationAsync()
        {
            while (_isFriendsInitializing)
            {
                await Awaitable.NextFrameAsync();
            }
        }
    }
}
