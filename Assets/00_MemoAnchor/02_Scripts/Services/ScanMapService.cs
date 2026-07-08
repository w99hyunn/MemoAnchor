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
        public string createdAt;
        public string scanCreatedAt;
    }

    [Serializable]
    public class ScanMapListResponse
    {
        public List<ScanMapItem> maps = new();
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
    }
}
