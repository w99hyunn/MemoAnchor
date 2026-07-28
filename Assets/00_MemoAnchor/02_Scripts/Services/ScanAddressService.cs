using System;
using System.Collections.Generic;
using Unity.Services.Authentication;
using UnityEngine;
using UnityEngine.Networking;

namespace MemoAnchor
{
    [Serializable]
    public class ScanAddressItem
    {
        public string id;
        public string zonecode;
        public string address;
        public string roadAddress;
        public string jibunAddress;
        public string buildingName;
        public string bname;
        public string createdAt;
    }

    [Serializable]
    public class ScanAddressListResponse
    {
        public List<ScanAddressItem> addresses = new();
    }

    [Serializable]
    public class ScanAddressSaveRequest
    {
        public string zonecode;
        public string address;
        public string roadAddress;
        public string jibunAddress;
        public string buildingName;
        public string bname;
    }

    public readonly struct ScanAddressSaveResult
    {
        public readonly bool IsSuccess;
        public readonly ScanAddressListResponse AddressList;

        public ScanAddressSaveResult(bool isSuccess, ScanAddressListResponse addressList)
        {
            IsSuccess = isSuccess;
            AddressList = addressList;
        }
    }

    public sealed class ScanAddressService
    {
        private const string SCAN_ADDRESSES_API_PATH = "/api/scan/addresses";

        private ScanAddressListResponse _lastResponse = new();
        private string _cachedPlayerId = string.Empty;
        private bool _isLoading;
        private bool _isSaving;

        public async Awaitable<ScanAddressListResponse> LoadAddressesAsync()
        {
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                ResetCache();
                return _lastResponse;
            }

            string playerId = AuthenticationService.Instance.PlayerId;
            ChangeCacheOwner(playerId);
            if (_isLoading)
            {
                return _lastResponse;
            }

            _isLoading = true;

            try
            {
                using UnityWebRequest request = ServicesManager.CreateAuthorizedGetRequest(SCAN_ADDRESSES_API_PATH);
                await ServicesManager.SendRequestAsync(request);

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"Scan address load failed: {request.error}");
                    return _lastResponse;
                }

                if (!IsCurrentPlayer(playerId))
                {
                    SynchronizeCacheOwner();
                    return _lastResponse;
                }

                _lastResponse = JsonUtility.FromJson<ScanAddressListResponse>(request.downloadHandler.text);
                return _lastResponse;
            }
            finally
            {
                _isLoading = false;
            }
        }

        public async Awaitable<ScanAddressSaveResult> SaveAddressAsync(ScanAddressSaveRequest payload)
        {
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                ResetCache();
                return new ScanAddressSaveResult(false, _lastResponse);
            }

            string playerId = AuthenticationService.Instance.PlayerId;
            ChangeCacheOwner(playerId);
            if (_isSaving)
            {
                return new ScanAddressSaveResult(false, _lastResponse);
            }

            _isSaving = true;

            try
            {
                string json = JsonUtility.ToJson(payload);
                using UnityWebRequest request = ServicesManager.CreateAuthorizedJsonPostRequest(SCAN_ADDRESSES_API_PATH, json);
                await ServicesManager.SendRequestAsync(request);

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"Scan address save failed: {request.error}");
                    return new ScanAddressSaveResult(false, _lastResponse);
                }

                if (!IsCurrentPlayer(playerId))
                {
                    SynchronizeCacheOwner();
                    return new ScanAddressSaveResult(false, _lastResponse);
                }

                _lastResponse = JsonUtility.FromJson<ScanAddressListResponse>(request.downloadHandler.text);
                return new ScanAddressSaveResult(true, _lastResponse);
            }
            finally
            {
                _isSaving = false;
            }
        }

        private void SynchronizeCacheOwner()
        {
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                ResetCache();
                return;
            }

            ChangeCacheOwner(AuthenticationService.Instance.PlayerId);
        }

        private void ChangeCacheOwner(string playerId)
        {
            if (string.Equals(_cachedPlayerId, playerId, StringComparison.Ordinal))
            {
                return;
            }

            _cachedPlayerId = playerId;
            _lastResponse = new ScanAddressListResponse();
        }

        private static bool IsCurrentPlayer(string playerId)
        {
            return AuthenticationService.Instance.IsSignedIn
                && string.Equals(AuthenticationService.Instance.PlayerId, playerId, StringComparison.Ordinal);
        }

        private void ResetCache()
        {
            _cachedPlayerId = string.Empty;
            _lastResponse = new ScanAddressListResponse();
        }
    }
}
