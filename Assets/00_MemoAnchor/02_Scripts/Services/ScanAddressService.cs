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
        private bool _isLoading;
        private bool _isSaving;

        public async Awaitable<ScanAddressListResponse> LoadAddressesAsync()
        {
            if (_isLoading || !AuthenticationService.Instance.IsSignedIn)
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
            if (_isSaving || !AuthenticationService.Instance.IsSignedIn)
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

                _lastResponse = JsonUtility.FromJson<ScanAddressListResponse>(request.downloadHandler.text);
                return new ScanAddressSaveResult(true, _lastResponse);
            }
            finally
            {
                _isSaving = false;
            }
        }
    }
}
