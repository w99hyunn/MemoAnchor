using System.Text;
using Unity.Services.Authentication;
using UnityEngine;
using UnityEngine.Networking;

namespace MemoAnchor
{
    public class ServicesManager : MonoBehaviour
    {
        private const string DEFAULT_SERVER_BASE_URL = "https://memoanchorserver.bindgames.kr";
        private const string DEFAULT_LOCAL_SERVER_BASE_URL = "http://localhost:5108";
        private const string AUTHORIZATION_HEADER = "Authorization";
        private const string CONTENT_TYPE_HEADER = "Content-Type";
        private const string JSON_CONTENT_TYPE = "application/json";
        private const string BEARER_PREFIX = "Bearer ";

        [SerializeField] private bool isLocalMode;
        [SerializeField] private string serverBaseUrl = DEFAULT_SERVER_BASE_URL;
        [SerializeField] private string localServerBaseUrl = DEFAULT_LOCAL_SERVER_BASE_URL;

        public static ServicesManager Instance { get; private set; }

        private static string _serverBaseUrl = DEFAULT_SERVER_BASE_URL;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            string activeServerBaseUrl = isLocalMode ? localServerBaseUrl : serverBaseUrl;
            _serverBaseUrl = activeServerBaseUrl.TrimEnd('/');
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public static string BuildServerUrl(string path)
        {
            string normalizedPath = path.StartsWith("/") ? path : $"/{path}";
            return $"{_serverBaseUrl}{normalizedPath}";
        }

        public static UnityWebRequest CreateGetRequest(string path)
        {
            return UnityWebRequest.Get(BuildServerUrl(path));
        }

        public static UnityWebRequest CreateAuthorizedGetRequest(string path)
        {
            UnityWebRequest request = CreateGetRequest(path);
            Authorize(request);
            return request;
        }

        public static UnityWebRequest CreateJsonPostRequest(string path, string json)
        {
            return CreateJsonRequest(path, json, UnityWebRequest.kHttpVerbPOST);
        }

        public static UnityWebRequest CreateJsonRequest(string path, string json, string method)
        {
            UnityWebRequest request = new(BuildServerUrl(path), method);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader(CONTENT_TYPE_HEADER, JSON_CONTENT_TYPE);
            return request;
        }

        public static UnityWebRequest CreateAuthorizedJsonRequest(string path, string json, string method)
        {
            UnityWebRequest request = CreateJsonRequest(path, json, method);
            Authorize(request);
            return request;
        }

        public static UnityWebRequest CreateAuthorizedJsonPostRequest(string path, string json)
        {
            UnityWebRequest request = CreateJsonPostRequest(path, json);
            Authorize(request);
            return request;
        }

        public static UnityWebRequest CreateAuthorizedRequest(string path, string method)
        {
            UnityWebRequest request = new(BuildServerUrl(path), method);
            request.downloadHandler = new DownloadHandlerBuffer();
            Authorize(request);
            return request;
        }

        public static UnityWebRequest CreateAuthorizedFileUploadRequest(string path, string filePath, string contentType)
        {
            UnityWebRequest request = new(BuildServerUrl(path), UnityWebRequest.kHttpVerbPOST);
            request.uploadHandler = new UploadHandlerFile(filePath);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader(CONTENT_TYPE_HEADER, contentType);
            Authorize(request);
            return request;
        }

        public static void Authorize(UnityWebRequest request)
        {
            request.SetRequestHeader(AUTHORIZATION_HEADER, $"{BEARER_PREFIX}{AuthenticationService.Instance.AccessToken}");
        }

        public static async Awaitable SendRequestAsync(UnityWebRequest request)
        {
            UnityWebRequestAsyncOperation operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                await Awaitable.NextFrameAsync();
            }
        }
    }
}
