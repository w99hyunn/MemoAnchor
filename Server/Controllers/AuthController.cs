using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ASP.NET_core_MemoAnchor_Server.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly AuthProviderOptions options;
    private readonly OAuthStateStore stateStore;
    private readonly IHttpClientFactory httpClientFactory;

    public AuthController(
        IOptions<AuthProviderOptions> options,
        OAuthStateStore stateStore,
        IHttpClientFactory httpClientFactory)
    {
        this.options = options.Value;
        this.stateStore = stateStore;
        this.httpClientFactory = httpClientFactory;
    }

    [HttpGet("start/{provider}")]
    public IActionResult Start(string provider, [FromQuery] string sessionId = "")
    {
        OAuthProviderOptions providerOptions = GetProviderOptions(provider);
        string state = CreateUrlToken();
        string nonce = CreateUrlToken();
        string codeVerifier = CreateUrlToken();
        string codeChallenge = Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier)));

        stateStore.SaveState(state, new OAuthState(provider, codeVerifier, nonce, sessionId));

        string authorizationEndpoint = provider == "kakao"
            ? "https://kauth.kakao.com/oauth/authorize"
            : "https://accounts.google.com/o/oauth2/v2/auth";

        string scope = provider == "kakao"
            ? "openid profile_nickname"
            : "openid profile email";

        Dictionary<string, string?> query = new()
        {
            ["response_type"] = "code",
            ["client_id"] = providerOptions.ClientId,
            ["redirect_uri"] = providerOptions.RedirectUri,
            ["scope"] = scope,
            ["state"] = state,
            ["nonce"] = nonce,
            ["code_challenge"] = codeChallenge,
            ["code_challenge_method"] = "S256"
        };

        return Redirect(Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString(authorizationEndpoint, query));
    }

    [HttpGet("callback/{provider}")]
    public async Task<IActionResult> Callback(string provider, [FromQuery] string code, [FromQuery] string state, CancellationToken cancellationToken)
    {
        if (!stateStore.TryConsumeState(state, out OAuthState oauthState) || oauthState.Provider != provider)
        {
            return BadRequest("Invalid OAuth state.");
        }

        OAuthProviderOptions providerOptions = GetProviderOptions(provider);
        OAuthTokenResponse token = await RequestTokenAsync(provider, providerOptions, code, oauthState.CodeVerifier, cancellationToken);
        OAuthUserInfoResponse userInfo = ReadUserInfoFromIdToken(token.IdToken);

        string resultId = CreateUrlToken();
        string displayName = provider == "kakao"
            ? userInfo.GetKakaoDisplayName()
            : userInfo.GetDisplayName();
        OAuthResult result = new(provider, token.IdToken, displayName, userInfo.Email ?? string.Empty);
        stateStore.SaveResult(resultId, result);
        if (!string.IsNullOrWhiteSpace(oauthState.SessionId))
        {
            stateStore.SaveSessionResult(oauthState.SessionId, result);
        }

        string deepLink = $"{options.DeepLinkScheme}://auth?result={Uri.EscapeDataString(resultId)}";
        string html = $"""
<!doctype html>
<html lang="ko">
<head><meta charset="utf-8"><title>MemoAnchor Login</title></head>
<body>
<p>로그인이 완료되었습니다. 앱으로 돌아갑니다.</p>
<script>location.href = "{deepLink}";</script>
<a href="{deepLink}">앱으로 돌아가기</a>
</body>
</html>
""";
        return Content(html, "text/html; charset=utf-8");
    }

    [HttpGet("result/{resultId}")]
    public IActionResult Result(string resultId)
    {
        if (!stateStore.TryConsumeResult(resultId, out OAuthResult result))
        {
            return NotFound();
        }

        return Ok(ToResponse(result));
    }

    [HttpGet("session/{sessionId}")]
    public IActionResult SessionResult(string sessionId)
    {
        if (!stateStore.TryConsumeSessionResult(sessionId, out OAuthResult result))
        {
            return Accepted();
        }

        return Ok(ToResponse(result));
    }

    private async Task<OAuthTokenResponse> RequestTokenAsync(
        string provider,
        OAuthProviderOptions providerOptions,
        string code,
        string codeVerifier,
        CancellationToken cancellationToken)
    {
        string tokenEndpoint = provider == "kakao"
            ? "https://kauth.kakao.com/oauth/token"
            : "https://oauth2.googleapis.com/token";

        Dictionary<string, string> body = new()
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = providerOptions.ClientId,
            ["redirect_uri"] = providerOptions.RedirectUri,
            ["code"] = code,
            ["code_verifier"] = codeVerifier
        };

        if (!string.IsNullOrWhiteSpace(providerOptions.ClientSecret))
        {
            body["client_secret"] = providerOptions.ClientSecret;
        }

        HttpClient httpClient = httpClientFactory.CreateClient();
        using HttpResponseMessage response = await httpClient.PostAsync(tokenEndpoint, new FormUrlEncodedContent(body), cancellationToken);
        response.EnsureSuccessStatusCode();

        OAuthTokenResponse? token = await response.Content.ReadFromJsonAsync<OAuthTokenResponse>(cancellationToken);
        return token ?? throw new InvalidOperationException("OAuth token response was empty.");
    }

    private OAuthProviderOptions GetProviderOptions(string provider)
    {
        return provider switch
        {
            "kakao" => options.Kakao,
            "google" => options.Google,
            _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unsupported provider.")
        };
    }

    private static OAuthUserInfoResponse ReadUserInfoFromIdToken(string idToken)
    {
        string[] tokenParts = idToken.Split('.');
        if (tokenParts.Length < 2)
        {
            return new OAuthUserInfoResponse();
        }

        string payloadJson = Encoding.UTF8.GetString(Base64UrlDecode(tokenParts[1]));
        using JsonDocument payload = JsonDocument.Parse(payloadJson);
        return new OAuthUserInfoResponse
        {
            Name = GetJsonString(payload.RootElement, "name"),
            Nickname = GetJsonString(payload.RootElement, "nickname"),
            Email = GetJsonString(payload.RootElement, "email")
        };
    }

    private static object ToResponse(OAuthResult result)
    {
        return new
        {
            provider = result.Provider,
            idToken = result.IdToken,
            name = result.Name,
            email = result.Email
        };
    }

    private static string CreateUrlToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Base64UrlEncode(bytes);
    }

    private static string Base64UrlEncode(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static byte[] Base64UrlDecode(string value)
    {
        string padded = value.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2:
                padded += "==";
                break;
            case 3:
                padded += "=";
                break;
        }

        return Convert.FromBase64String(padded);
    }

    private static string GetJsonString(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out JsonElement property))
        {
            return property.GetString() ?? string.Empty;
        }

        return string.Empty;
    }
}

public sealed class OAuthTokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("id_token")]
    public string IdToken { get; set; } = string.Empty;
}

public sealed class OAuthUserInfoResponse
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("nickname")]
    public string? Nickname { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    public string GetDisplayName()
    {
        if (!string.IsNullOrWhiteSpace(Name))
        {
            return Name;
        }

        return Nickname ?? string.Empty;
    }

    public string GetKakaoDisplayName()
    {
        if (!string.IsNullOrWhiteSpace(Nickname))
        {
            return Nickname;
        }

        return Name ?? string.Empty;
    }
}
