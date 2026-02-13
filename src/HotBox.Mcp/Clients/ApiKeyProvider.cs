namespace HotBox.Mcp.Clients;

/// <summary>
/// Holds the API key for admin-only requests, keeping it off the default HttpClient headers
/// so bearer-token requests don't accidentally trigger API key authentication.
/// </summary>
public class ApiKeyProvider(string apiKey)
{
    public string ApiKey { get; } = apiKey;
}
