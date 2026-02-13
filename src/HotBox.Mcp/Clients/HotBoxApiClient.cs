using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace HotBox.Mcp.Clients;

/// <summary>
/// HTTP client for communicating with the HotBox API.
/// </summary>
public class HotBoxApiClient
{
    private readonly HttpClient _httpClient;

    public HotBoxApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// Creates a new agent account (requires API key auth).
    /// </summary>
    public async Task<JsonElement> CreateAgentAccountAsync(string email, string displayName, CancellationToken cancellationToken = default)
    {
        var payload = new { email, displayName };
        var response = await _httpClient.PostAsJsonAsync("/api/admin/agents", payload, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
    }

    /// <summary>
    /// Lists all agent accounts created by this API key (requires API key auth).
    /// </summary>
    public async Task<JsonElement> ListAgentAccountsAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync("/api/admin/agents", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
    }

    /// <summary>
    /// Sends a message to a text channel (requires bearer token auth).
    /// </summary>
    public async Task<JsonElement> SendMessageAsync(Guid channelId, string content, string bearerToken, CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/channels/{channelId}/messages");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        request.Content = JsonContent.Create(new { content });

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
    }

    /// <summary>
    /// Sends a direct message to a user (requires bearer token auth).
    /// </summary>
    public async Task<JsonElement> SendDirectMessageAsync(Guid userId, string content, string bearerToken, CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/dm/{userId}/messages");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        request.Content = JsonContent.Create(new { content });

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
    }

    /// <summary>
    /// Reads messages from a text channel (requires bearer token auth).
    /// </summary>
    public async Task<JsonElement> ReadMessagesAsync(Guid channelId, int limit, string bearerToken, CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/channels/{channelId}/messages?limit={limit}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
    }

    /// <summary>
    /// Reads direct messages with a user (requires bearer token auth).
    /// </summary>
    public async Task<JsonElement> ReadDirectMessagesAsync(Guid userId, int limit, string bearerToken, CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/dm/{userId}?limit={limit}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
    }
}
