using System;
using System.Text;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace MemoAnchor
{
    public sealed class SplashAuthService
    {
        private readonly string serverBaseUrl;
        private readonly string kakaoProviderName;
        private readonly string googleProviderName;
        private AuthResultResponse pendingAuthResult;

        public SplashAuthService(string serverBaseUrl, string kakaoProviderName, string googleProviderName)
        {
            this.serverBaseUrl = serverBaseUrl.TrimEnd('/');
            this.kakaoProviderName = kakaoProviderName;
            this.googleProviderName = googleProviderName;
        }

        public string BeginProviderLogin(string provider)
        {
            string sessionId = Guid.NewGuid().ToString("N");
            Application.OpenURL($"{serverBaseUrl}/api/auth/start/{provider}?sessionId={sessionId}");
            return sessionId;
        }

        public async Awaitable<SplashAuthCompletion> TryCompleteCachedLoginAsync()
        {
            await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.SessionTokenExists)
            {
                return new SplashAuthCompletion(false, default);
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync(new SignInOptions
                {
                    CreateAccount = false
                });
            }

            ProfileStatusResponse profileStatus = await FetchProfileStatusAsync();
            if (!profileStatus.exists)
            {
                AuthenticationService.Instance.SignOut(true);
                AuthenticationService.Instance.ClearSessionToken();
                return new SplashAuthCompletion(false, default);
            }

            PlayerProfile profile = BuildPlayerProfile(profileStatus);
            PlayerSession.SetProfile(profile);
            return new SplashAuthCompletion(true, profile);
        }

        public async Awaitable<SplashAuthCompletion> CompleteLoginAsync(string resultId)
        {
            AuthResultResponse authResult = await FetchAuthResultAsync(resultId);
            return await CompleteLoginAsync(authResult);
        }

        public async Awaitable<SplashAuthCompletion> CompleteLoginSessionAsync(string sessionId)
        {
            const float TIMEOUT_SECONDS = 180f;
            float elapsed = 0f;

            while (elapsed < TIMEOUT_SECONDS)
            {
                AuthResultResponse authResult = await TryFetchSessionAuthResultAsync(sessionId);
                if (authResult != null)
                {
                    return await CompleteLoginAsync(authResult);
                }

                float waitElapsed = 0f;
                while (waitElapsed < 1f)
                {
                    waitElapsed += Time.deltaTime;
                    elapsed += Time.deltaTime;
                    await Awaitable.NextFrameAsync();
                }
            }

            throw new TimeoutException("Login callback timed out.");
        }

        private async Awaitable<SplashAuthCompletion> CompleteLoginAsync(AuthResultResponse authResult)
        {
            pendingAuthResult = authResult;
            await UnityServices.InitializeAsync();
            await AuthenticationService.Instance.SignInWithOpenIdConnectAsync(GetUnityProviderName(pendingAuthResult.provider), pendingAuthResult.idToken);

            ProfileStatusResponse profileStatus = await FetchProfileStatusAsync();
            PlayerProfile profile = BuildPlayerProfile(profileStatus);
            if (profileStatus.exists)
            {
                PlayerSession.SetProfile(profile);
            }

            return new SplashAuthCompletion(profileStatus.exists, profile);
        }

        public async Awaitable SaveSignupProfileAsync(string name, string email, string companyName)
        {
            PlayerProfileSaveRequest payload = new()
            {
                provider = pendingAuthResult.provider,
                name = name,
                email = email,
                companyName = companyName
            };

            string json = JsonUtility.ToJson(payload);
            using UnityWebRequest request = new($"{serverBaseUrl}/api/profile", UnityWebRequest.kHttpVerbPOST);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", $"Bearer {AuthenticationService.Instance.AccessToken}");
            await SendRequestAsync(request);

            if (request.result != UnityWebRequest.Result.Success)
            {
                throw new InvalidOperationException(request.error);
            }

            PlayerSession.SetProfile(new PlayerProfile(name, email, companyName));
        }

        public static string GetResultIdFromDeepLink(string url)
        {
            int queryStart = url.IndexOf('?');
            if (queryStart < 0)
            {
                return string.Empty;
            }

            string query = url[(queryStart + 1)..];
            string[] pairs = query.Split('&');
            foreach (string pair in pairs)
            {
                string[] parts = pair.Split('=');
                if (parts.Length == 2 && UnityWebRequest.UnEscapeURL(parts[0]) == "result")
                {
                    return UnityWebRequest.UnEscapeURL(parts[1]);
                }
            }

            return string.Empty;
        }

        private string GetUnityProviderName(string provider)
        {
            return provider == "kakao" ? kakaoProviderName : googleProviderName;
        }

        private PlayerProfile BuildPlayerProfile(ProfileStatusResponse profileStatus)
        {
            string fallbackName = pendingAuthResult != null ? pendingAuthResult.name : string.Empty;
            string fallbackEmail = pendingAuthResult != null ? pendingAuthResult.email : string.Empty;
            string name = string.IsNullOrWhiteSpace(profileStatus.name) ? fallbackName : profileStatus.name;
            string email = string.IsNullOrWhiteSpace(profileStatus.email) ? fallbackEmail : profileStatus.email;
            return new PlayerProfile(name, email, profileStatus.companyName);
        }

        private async Awaitable<AuthResultResponse> FetchAuthResultAsync(string resultId)
        {
            string url = $"{serverBaseUrl}/api/auth/result/{UnityWebRequest.EscapeURL(resultId)}";
            using UnityWebRequest request = UnityWebRequest.Get(url);
            await SendRequestAsync(request);

            if (request.result != UnityWebRequest.Result.Success)
            {
                throw new InvalidOperationException(request.error);
            }

            return JsonUtility.FromJson<AuthResultResponse>(request.downloadHandler.text);
        }

        private async Awaitable<AuthResultResponse> TryFetchSessionAuthResultAsync(string sessionId)
        {
            string url = $"{serverBaseUrl}/api/auth/session/{UnityWebRequest.EscapeURL(sessionId)}";
            using UnityWebRequest request = UnityWebRequest.Get(url);
            await SendRequestAsync(request);

            if (request.responseCode == 202)
            {
                return null;
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                throw new InvalidOperationException(request.error);
            }

            return JsonUtility.FromJson<AuthResultResponse>(request.downloadHandler.text);
        }

        private async Awaitable<ProfileStatusResponse> FetchProfileStatusAsync()
        {
            using UnityWebRequest request = UnityWebRequest.Get($"{serverBaseUrl}/api/profile/me");
            request.SetRequestHeader("Authorization", $"Bearer {AuthenticationService.Instance.AccessToken}");
            await SendRequestAsync(request);

            if (request.result != UnityWebRequest.Result.Success)
            {
                throw new InvalidOperationException(request.error);
            }

            return JsonUtility.FromJson<ProfileStatusResponse>(request.downloadHandler.text);
        }

        private static async Awaitable SendRequestAsync(UnityWebRequest request)
        {
            UnityWebRequestAsyncOperation operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                await Awaitable.NextFrameAsync();
            }
        }

        [Serializable]
        private sealed class AuthResultResponse
        {
            public string provider;
            public string idToken;
            public string name;
            public string email;
        }

        [Serializable]
        private sealed class ProfileStatusResponse
        {
            public bool exists;
            public string name;
            public string email;
            public string companyName;
        }

        [Serializable]
        private sealed class PlayerProfileSaveRequest
        {
            public string provider;
            public string name;
            public string email;
            public string companyName;
        }
    }
}
