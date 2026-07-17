public sealed class AuthProviderOptions
{
    public OAuthProviderOptions Kakao { get; set; } = new();
    public OAuthProviderOptions Google { get; set; } = new();
    public string DeepLinkScheme { get; set; } = "memoanchor";
}

public sealed class OAuthProviderOptions
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
}
