using System;
using System.Collections.Generic;
using Unity.Services.Authentication;
using UnityEngine;
using UnityEngine.Networking;

namespace MemoAnchor
{
    [Serializable]
    public class ScanMapMemberItem
    {
        public string playerId;
        public string role;
        public string name;
        public string companyName;
    }

    [Serializable]
    public class ScanMapItem
    {
        public string id;
        public string addressId;
        public string address;
        public string roadAddress;
        public string spaceName;
        public string currentUserRole;
        public List<ScanMapMemberItem> members = new();
        public string inviteCode;
        public string inviteCodeExpiresAt;
        public string createdAt;
        public string scanCreatedAt;
    }

    [Serializable]
    public class ScanMapListResponse
    {
        public List<ScanMapItem> maps = new();
    }

    [Serializable]
    public class MapInviteResponse
    {
        public string code;
        public string expiresAt;
    }

    [Serializable]
    public class InviteMapMembersRequest
    {
        public List<InviteMapMemberRequestItem> members = new();
    }

    [Serializable]
    public class InviteMapMemberRequestItem
    {
        public string playerId;
        public string name;
        public string companyName;
    }

    [Serializable]
    public class MapFriendProfilesRequest
    {
        public List<string> playerIds = new();
    }

    [Serializable]
    public class MapFriendProfileItem
    {
        public string playerId;
        public string name;
        public string companyName;
    }

    [Serializable]
    public class MapFriendProfilesResponse
    {
        public List<MapFriendProfileItem> profiles = new();
    }

    [Serializable]
    public class ScanMapCreateRequest
    {
        public string addressId;
        public string address;
        public string roadAddress;
        public string spaceName;
        public string repairerPlayerId;
        public string managerPlayerId;
    }

    public readonly struct ScanMapCreateResult
    {
        public readonly bool IsSuccess;
        public readonly ScanMapListResponse MapList;

        public ScanMapCreateResult(bool isSuccess, ScanMapListResponse mapList)
        {
            IsSuccess = isSuccess;
            MapList = mapList;
        }
    }

    public sealed class ScanMapService
    {
        private const string SCAN_MAPS_API_PATH = "/api/scan/maps";

        private ScanMapListResponse _lastResponse = new();
        private bool _isLoading;
        private bool _isCreating;
        private bool _isManagingMembers;
        private bool _isLoadingFriendProfiles;

        public async Awaitable<ScanMapListResponse> LoadMapsAsync()
        {
            if (_isLoading || !AuthenticationService.Instance.IsSignedIn)
            {
                return _lastResponse;
            }

            _isLoading = true;

            try
            {
                using UnityWebRequest request = ServicesManager.CreateAuthorizedGetRequest(SCAN_MAPS_API_PATH);
                await ServicesManager.SendRequestAsync(request);

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"Scan map load failed: {request.error}");
                    return _lastResponse;
                }

                _lastResponse = JsonUtility.FromJson<ScanMapListResponse>(request.downloadHandler.text);
                return _lastResponse;
            }
            finally
            {
                _isLoading = false;
            }
        }

        public async Awaitable<ScanMapCreateResult> CreateMapAsync(ScanMapCreateRequest payload)
        {
            if (_isCreating || !AuthenticationService.Instance.IsSignedIn)
            {
                return new ScanMapCreateResult(false, _lastResponse);
            }

            _isCreating = true;

            try
            {
                string json = JsonUtility.ToJson(payload);
                using UnityWebRequest request = ServicesManager.CreateAuthorizedJsonPostRequest(SCAN_MAPS_API_PATH, json);
                await ServicesManager.SendRequestAsync(request);

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"Scan map create failed: {request.error}");
                    return new ScanMapCreateResult(false, _lastResponse);
                }

                _lastResponse = JsonUtility.FromJson<ScanMapListResponse>(request.downloadHandler.text);
                return new ScanMapCreateResult(true, _lastResponse);
            }
            finally
            {
                _isCreating = false;
            }
        }

        public async Awaitable<MapInviteResponse> IssueInviteAsync(string mapId)
        {
            if (_isManagingMembers || !AuthenticationService.Instance.IsSignedIn)
            {
                return null;
            }

            _isManagingMembers = true;
            try
            {
                string path = $"{SCAN_MAPS_API_PATH}/{UnityWebRequest.EscapeURL(mapId)}/invite";
                using UnityWebRequest request = ServicesManager.CreateAuthorizedJsonPostRequest(path, "{}");
                await ServicesManager.SendRequestAsync(request);
                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"Map invite issue failed: {request.error}");
                    return null;
                }
                return JsonUtility.FromJson<MapInviteResponse>(request.downloadHandler.text);
            }
            finally
            {
                _isManagingMembers = false;
            }
        }

        public async Awaitable<ScanMapListResponse> PromoteMemberAsync(string mapId, string memberPlayerId)
        {
            string path = $"{SCAN_MAPS_API_PATH}/{UnityWebRequest.EscapeURL(mapId)}/members/{UnityWebRequest.EscapeURL(memberPlayerId)}/promote";
            return await MutateMembersAsync(path, UnityWebRequest.kHttpVerbPOST);
        }

        public async Awaitable<ScanMapListResponse> InviteMembersAsync(string mapId, List<InviteMapMemberRequestItem> members)
        {
            if (_isManagingMembers || !AuthenticationService.Instance.IsSignedIn)
            {
                return null;
            }

            _isManagingMembers = true;
            try
            {
                string path = $"{SCAN_MAPS_API_PATH}/{UnityWebRequest.EscapeURL(mapId)}/members";
                string json = JsonUtility.ToJson(new InviteMapMembersRequest { members = members });
                using UnityWebRequest request = ServicesManager.CreateAuthorizedJsonPostRequest(path, json);
                await ServicesManager.SendRequestAsync(request);
                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"Map member invite failed: {request.error}");
                    return null;
                }
                _lastResponse = JsonUtility.FromJson<ScanMapListResponse>(request.downloadHandler.text);
                return _lastResponse;
            }
            finally
            {
                _isManagingMembers = false;
            }
        }

        public async Awaitable<MapFriendProfilesResponse> LoadFriendProfilesAsync(List<string> playerIds)
        {
            if (_isLoadingFriendProfiles || !AuthenticationService.Instance.IsSignedIn)
            {
                return null;
            }

            _isLoadingFriendProfiles = true;
            try
            {
                string json = JsonUtility.ToJson(new MapFriendProfilesRequest { playerIds = playerIds });
                using UnityWebRequest request = ServicesManager.CreateAuthorizedJsonPostRequest($"{SCAN_MAPS_API_PATH}/friend-profiles", json);
                await ServicesManager.SendRequestAsync(request);
                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"Map friend profile load failed: {request.error}\n{request.downloadHandler.text}");
                    return null;
                }
                return JsonUtility.FromJson<MapFriendProfilesResponse>(request.downloadHandler.text);
            }
            finally
            {
                _isLoadingFriendProfiles = false;
            }
        }

        public async Awaitable<ScanMapListResponse> RemoveMemberAsync(string mapId, string memberPlayerId)
        {
            string path = $"{SCAN_MAPS_API_PATH}/{UnityWebRequest.EscapeURL(mapId)}/members/{UnityWebRequest.EscapeURL(memberPlayerId)}";
            return await MutateMembersAsync(path, UnityWebRequest.kHttpVerbDELETE);
        }

        private async Awaitable<ScanMapListResponse> MutateMembersAsync(string path, string method)
        {
            if (_isManagingMembers || !AuthenticationService.Instance.IsSignedIn)
            {
                return null;
            }

            _isManagingMembers = true;
            try
            {
                using UnityWebRequest request = ServicesManager.CreateAuthorizedRequest(path, method);
                await ServicesManager.SendRequestAsync(request);
                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"Map member mutation failed: {request.error}");
                    return null;
                }
                _lastResponse = JsonUtility.FromJson<ScanMapListResponse>(request.downloadHandler.text);
                return _lastResponse;
            }
            finally
            {
                _isManagingMembers = false;
            }
        }
    }
}
