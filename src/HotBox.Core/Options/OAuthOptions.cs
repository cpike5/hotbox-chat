namespace HotBox.Core.Options;

public class OAuthOptions
{
    public const string SectionName = "OAuth";

    public OAuthProviderOptions Google { get; set; } = new();

    public OAuthProviderOptions Microsoft { get; set; } = new();
}

public class OAuthProviderOptions
{
    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    public bool Enabled { get; set; }
}
