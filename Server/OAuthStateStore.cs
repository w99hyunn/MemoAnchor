using Microsoft.Extensions.Caching.Memory;

public sealed class OAuthStateStore
{
    private static readonly TimeSpan StateLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan ResultLifetime = TimeSpan.FromMinutes(2);
    private readonly IMemoryCache memoryCache;

    public OAuthStateStore(IMemoryCache memoryCache)
    {
        this.memoryCache = memoryCache;
    }

    public void SaveState(string state, OAuthState value)
    {
        memoryCache.Set(GetStateKey(state), value, StateLifetime);
    }

    public bool TryConsumeState(string state, out OAuthState value)
    {
        if (memoryCache.TryGetValue(GetStateKey(state), out value!))
        {
            memoryCache.Remove(GetStateKey(state));
            return true;
        }

        value = default!;
        return false;
    }

    public void SaveResult(string resultId, OAuthResult value)
    {
        memoryCache.Set(GetResultKey(resultId), value, ResultLifetime);
    }

    public void SaveSessionResult(string sessionId, OAuthResult value)
    {
        memoryCache.Set(GetSessionResultKey(sessionId), value, ResultLifetime);
    }

    public bool TryConsumeResult(string resultId, out OAuthResult value)
    {
        if (memoryCache.TryGetValue(GetResultKey(resultId), out value!))
        {
            memoryCache.Remove(GetResultKey(resultId));
            return true;
        }

        value = default!;
        return false;
    }

    public bool TryConsumeSessionResult(string sessionId, out OAuthResult value)
    {
        if (memoryCache.TryGetValue(GetSessionResultKey(sessionId), out value!))
        {
            memoryCache.Remove(GetSessionResultKey(sessionId));
            return true;
        }

        value = default!;
        return false;
    }

    private static string GetStateKey(string state)
    {
        return $"oauth-state:{state}";
    }

    private static string GetResultKey(string resultId)
    {
        return $"oauth-result:{resultId}";
    }

    private static string GetSessionResultKey(string sessionId)
    {
        return $"oauth-session-result:{sessionId}";
    }
}

public sealed record OAuthState(string Provider, string CodeVerifier, string Nonce, string SessionId);

public sealed record OAuthResult(string Provider, string IdToken, string Name, string Email);
