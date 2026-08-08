using System;
using System.Collections.Generic;
using System.IO;
using Unity.Services.Authentication;
using UnityEngine;
using UnityEngine.Networking;

namespace MemoAnchor
{
    [Serializable]
    public class MemoChecklistEntry
    {
        public string text;
        public bool done;
    }

    [Serializable]
    public class MemoVoiceEntry
    {
        public string name;
        public string url;
    }

    [Serializable]
    public class MemoItem
    {
        public string id;
        public string mapId;
        public string mapName;
        public string address;
        public string locationName;
        public string kind;
        public string urgency;
        public string title;
        public string body;
        public string authorPlayerId;
        public string authorName;
        public string assigneePlayerId;
        public string assigneeName;
        public string workStatus;
        public string dueText;
        public string createdAt;
        public string deletedAt;
        public bool isRead;
        public bool hasSpatialAnchor;
        public string reconstructionScanId;
        public float positionX;
        public float positionY;
        public float positionZ;
        public float rotationX;
        public float rotationY;
        public float rotationZ;
        public float rotationW = 1f;
        public List<MemoChecklistEntry> checklistItems = new();
        public List<MemoVoiceEntry> voiceItems = new();
        public List<string> imageUrls = new();
    }

    [Serializable]
    public class MemoListResponse
    {
        public List<MemoItem> memos = new();
    }

    [Serializable]
    public class MemoCreateRequest
    {
        public string mapId;
        public string locationName;
        public string kind;
        public string urgency;
        public string title;
        public string body;
        public string assigneePlayerId;
        public string assigneeName;
        public string dueText;
        public bool hasSpatialAnchor;
        public string reconstructionScanId;
        public float positionX;
        public float positionY;
        public float positionZ;
        public float rotationX;
        public float rotationY;
        public float rotationZ;
        public float rotationW = 1f;
        public List<MemoChecklistEntry> checklistItems = new();
        public List<MemoVoiceEntry> voiceItems = new();
        public List<string> imageUrls = new();
    }

    public readonly struct MemoCreateResult
    {
        public readonly bool IsSuccess;
        public readonly MemoItem Memo;
        public readonly MemoListResponse MemoList;

        public MemoCreateResult(bool isSuccess, MemoItem memo, MemoListResponse memoList)
        {
            IsSuccess = isSuccess;
            Memo = memo;
            MemoList = memoList;
        }
    }

    public readonly struct MemoMediaUploadResult
    {
        public readonly bool IsSuccess;
        public readonly List<string> Urls;

        public MemoMediaUploadResult(bool isSuccess, List<string> urls)
        {
            IsSuccess = isSuccess;
            Urls = urls;
        }
    }

    public sealed class MemoService
    {
        private const string MEMOS_API_PATH = "/api/memos";

        private MemoListResponse _lastResponse = new();
        private MemoListResponse _lastTrashResponse = new();
        private bool _isLoading;
        private bool _isCreating;
        private bool _isMutating;
        private bool _isUploadingMedia;
        private bool _isUploadingVoice;

        public bool LastLoadSucceeded { get; private set; }

        public async Awaitable<MemoListResponse> LoadMemosAsync()
        {
            if (_isLoading || !AuthenticationService.Instance.IsSignedIn)
            {
                return _lastResponse;
            }

            _isLoading = true;
            LastLoadSucceeded = false;

            try
            {
                using UnityWebRequest request = ServicesManager.CreateAuthorizedGetRequest(MEMOS_API_PATH);
                await ServicesManager.SendRequestAsync(request);

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"Memo load failed: {request.error}");
                    return _lastResponse;
                }

                _lastResponse = JsonUtility.FromJson<MemoListResponse>(request.downloadHandler.text);
                LastLoadSucceeded = true;
                return _lastResponse;
            }
            finally
            {
                _isLoading = false;
            }
        }

        public async Awaitable<MemoCreateResult> CreateMemoAsync(MemoCreateRequest payload)
        {
            if (_isCreating || !AuthenticationService.Instance.IsSignedIn)
            {
                return new MemoCreateResult(false, null, _lastResponse);
            }

            _isCreating = true;

            try
            {
                string json = JsonUtility.ToJson(payload);
                using UnityWebRequest request = ServicesManager.CreateAuthorizedJsonPostRequest(MEMOS_API_PATH, json);
                await ServicesManager.SendRequestAsync(request);

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"Memo create failed: {request.error}");
                    return new MemoCreateResult(false, null, _lastResponse);
                }

                MemoCreateResponse response = JsonUtility.FromJson<MemoCreateResponse>(request.downloadHandler.text);
                if (response.memos != null)
                {
                    _lastResponse = new MemoListResponse { memos = response.memos };
                }

                return new MemoCreateResult(true, response.memo, _lastResponse);
            }
            finally
            {
                _isCreating = false;
            }
        }

        public async Awaitable<MemoListResponse> LoadTrashedMemosAsync()
        {
            if (_isLoading || !AuthenticationService.Instance.IsSignedIn)
            {
                return _lastTrashResponse;
            }

            _isLoading = true;

            try
            {
                using UnityWebRequest request = ServicesManager.CreateAuthorizedGetRequest($"{MEMOS_API_PATH}/trash");
                await ServicesManager.SendRequestAsync(request);

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"Memo trash load failed: {request.error}");
                    return _lastTrashResponse;
                }

                _lastTrashResponse = JsonUtility.FromJson<MemoListResponse>(request.downloadHandler.text);
                return _lastTrashResponse;
            }
            finally
            {
                _isLoading = false;
            }
        }

        public async Awaitable<MemoCreateResult> UpdateMemoAsync(string memoId, MemoCreateRequest payload)
        {
            if (_isMutating || !AuthenticationService.Instance.IsSignedIn)
            {
                return new MemoCreateResult(false, null, _lastResponse);
            }

            _isMutating = true;

            try
            {
                string json = JsonUtility.ToJson(payload);
                string path = $"{MEMOS_API_PATH}/{UnityWebRequest.EscapeURL(memoId)}";
                using UnityWebRequest request = ServicesManager.CreateAuthorizedJsonRequest(path, json, "PUT");
                await ServicesManager.SendRequestAsync(request);

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"Memo update failed: {request.error}");
                    return new MemoCreateResult(false, null, _lastResponse);
                }

                MemoCreateResponse response = JsonUtility.FromJson<MemoCreateResponse>(request.downloadHandler.text);
                if (response.memos != null)
                {
                    _lastResponse = new MemoListResponse { memos = response.memos };
                }

                return new MemoCreateResult(true, response.memo, _lastResponse);
            }
            finally
            {
                _isMutating = false;
            }
        }

        public async Awaitable<MemoMediaUploadResult> UploadMemoMediaAsync(List<string> filePaths)
        {
            if (filePaths.Count == 0)
            {
                return new MemoMediaUploadResult(true, new List<string>());
            }

            if (_isUploadingMedia || !AuthenticationService.Instance.IsSignedIn)
            {
                return new MemoMediaUploadResult(false, new List<string>());
            }

            _isUploadingMedia = true;
            var urls = new List<string>(filePaths.Count);

            try
            {
                foreach (string filePath in filePaths)
                {
                    string extension = Path.GetExtension(filePath).ToLowerInvariant();
                    string path = $"{MEMOS_API_PATH}/media?extension={UnityWebRequest.EscapeURL(extension)}";
                    using UnityWebRequest request = ServicesManager.CreateAuthorizedFileUploadRequest(path, filePath, GetMediaContentType(extension));
                    await ServicesManager.SendRequestAsync(request);

                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        Debug.LogWarning($"Memo media upload failed: {request.error}");
                        return new MemoMediaUploadResult(false, urls);
                    }

                    MemoMediaUploadResponse response = JsonUtility.FromJson<MemoMediaUploadResponse>(request.downloadHandler.text);
                    if (string.IsNullOrWhiteSpace(response.url))
                    {
                        return new MemoMediaUploadResult(false, urls);
                    }

                    urls.Add(response.url);
                }

                return new MemoMediaUploadResult(true, urls);
            }
            finally
            {
                _isUploadingMedia = false;
            }
        }

        public async Awaitable<MemoMediaUploadResult> UploadMemoVoiceAsync(List<string> filePaths)
        {
            if (filePaths.Count == 0)
            {
                return new MemoMediaUploadResult(true, new List<string>());
            }

            if (_isUploadingVoice || !AuthenticationService.Instance.IsSignedIn)
            {
                return new MemoMediaUploadResult(false, new List<string>());
            }

            _isUploadingVoice = true;
            var urls = new List<string>(filePaths.Count);

            try
            {
                foreach (string filePath in filePaths)
                {
                    string path = $"{MEMOS_API_PATH}/voice";
                    using UnityWebRequest request = ServicesManager.CreateAuthorizedFileUploadRequest(path, filePath, "audio/wav");
                    await ServicesManager.SendRequestAsync(request);

                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        Debug.LogWarning($"Memo voice upload failed: {request.error}");
                        return new MemoMediaUploadResult(false, urls);
                    }

                    MemoMediaUploadResponse response = JsonUtility.FromJson<MemoMediaUploadResponse>(request.downloadHandler.text);
                    if (string.IsNullOrWhiteSpace(response.url))
                    {
                        return new MemoMediaUploadResult(false, urls);
                    }

                    urls.Add(response.url);
                }

                return new MemoMediaUploadResult(true, urls);
            }
            finally
            {
                _isUploadingVoice = false;
            }
        }

        public async Awaitable<MemoListResponse> MoveMemoToTrashAsync(string memoId)
        {
            return await MutateMemoAsync($"{MEMOS_API_PATH}/{UnityWebRequest.EscapeURL(memoId)}", UnityWebRequest.kHttpVerbDELETE, _lastResponse, response => _lastResponse = response);
        }

        public async Awaitable<MemoListResponse> RestoreMemoAsync(string memoId)
        {
            return await MutateMemoAsync($"{MEMOS_API_PATH}/{UnityWebRequest.EscapeURL(memoId)}/restore", UnityWebRequest.kHttpVerbPOST, _lastTrashResponse, response => _lastTrashResponse = response);
        }

        public async Awaitable<MemoListResponse> DeleteMemoPermanentlyAsync(string memoId)
        {
            return await MutateMemoAsync($"{MEMOS_API_PATH}/{UnityWebRequest.EscapeURL(memoId)}/permanent", UnityWebRequest.kHttpVerbDELETE, _lastTrashResponse, response => _lastTrashResponse = response);
        }

        public async Awaitable<MemoListResponse> SetMemoWorkStatusAsync(string memoId, string status)
        {
            string path = $"{MEMOS_API_PATH}/{UnityWebRequest.EscapeURL(memoId)}/work-status/{UnityWebRequest.EscapeURL(status)}";
            return await MutateMemoAsync(path, UnityWebRequest.kHttpVerbPOST, _lastResponse, response => _lastResponse = response);
        }

        public async Awaitable<bool> MarkMemoReadAsync(string memoId)
        {
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                return false;
            }

            string path = $"{MEMOS_API_PATH}/{UnityWebRequest.EscapeURL(memoId)}/read";
            using UnityWebRequest request = ServicesManager.CreateAuthorizedRequest(path, UnityWebRequest.kHttpVerbPOST);
            await ServicesManager.SendRequestAsync(request);
            if (request.result == UnityWebRequest.Result.Success)
            {
                return true;
            }

            Debug.LogWarning($"Memo read update failed: {request.error}");
            return false;
        }

        private async Awaitable<MemoListResponse> MutateMemoAsync(string path, string method, MemoListResponse fallback, Action<MemoListResponse> applyResponse)
        {
            if (_isMutating || !AuthenticationService.Instance.IsSignedIn)
            {
                return fallback;
            }

            _isMutating = true;

            try
            {
                using UnityWebRequest request = ServicesManager.CreateAuthorizedRequest(path, method);
                await ServicesManager.SendRequestAsync(request);

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"Memo mutation failed: {request.error}");
                    return fallback;
                }

                MemoListResponse response = JsonUtility.FromJson<MemoListResponse>(request.downloadHandler.text);
                applyResponse(response);
                return response;
            }
            finally
            {
                _isMutating = false;
            }
        }

        private static string GetMediaContentType(string extension)
        {
            switch (extension)
            {
                case ".jpg":
                case ".jpeg":
                    return "image/jpeg";
                case ".png":
                    return "image/png";
                case ".gif":
                    return "image/gif";
                case ".webp":
                    return "image/webp";
                case ".heic":
                case ".heif":
                    return "image/heic";
                case ".bmp":
                    return "image/bmp";
                case ".tif":
                case ".tiff":
                    return "image/tiff";
                case ".mov":
                    return "video/quicktime";
                case ".avi":
                    return "video/x-msvideo";
                case ".webm":
                    return "video/webm";
                case ".m4v":
                    return "video/x-m4v";
                case ".3gp":
                    return "video/3gpp";
                case ".mkv":
                    return "video/x-matroska";
                default:
                    return "video/mp4";
            }
        }

        [Serializable]
        private sealed class MemoCreateResponse
        {
            public MemoItem memo;
            public List<MemoItem> memos = new();
        }

        [Serializable]
        private sealed class MemoMediaUploadResponse
        {
            public string url;
        }
    }
}
