using System;
using System.Collections.Generic;
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
        public string authorName;
        public string assigneeName;
        public string dueText;
        public string createdAt;
        public List<MemoChecklistEntry> checklistItems = new();
        public List<string> voiceItems = new();
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

    public sealed class MemoService
    {
        private const string MEMOS_API_PATH = "/api/memos";

        private MemoListResponse _lastResponse = new();
        private bool _isLoading;
        private bool _isCreating;

        public async Awaitable<MemoListResponse> LoadMemosAsync()
        {
            if (_isLoading || !AuthenticationService.Instance.IsSignedIn)
            {
                return _lastResponse;
            }

            _isLoading = true;

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

        [Serializable]
        private sealed class MemoCreateResponse
        {
            public MemoItem memo;
            public List<MemoItem> memos = new();
        }
    }
}
