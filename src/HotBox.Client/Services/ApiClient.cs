using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using HotBox.Client.Models;
using HotBox.Client.State;
using HotBox.Core.Enums;
using Microsoft.Extensions.Logging;

namespace HotBox.Client.Services;

public class ApiClient
{
    private readonly HttpClient _http;
    private readonly AuthState _authState;
    private readonly ILogger<ApiClient> _logger;

    public ApiClient(HttpClient http, AuthState authState, ILogger<ApiClient> logger)
    {
        _http = http;
        _authState = authState;
        _logger = logger;
    }

    // ── Auth ────────────────────────────────────────────────────────────

    public async Task<AuthResponse?> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        var request = new LoginRequest { Email = email, Password = password };
        var response = await PostAsync<AuthResponse>("api/auth/login", request, ct);
        return response;
    }

    public async Task<AuthResponse?> RegisterAsync(
        string email,
        string password,
        string displayName,
        string? inviteCode = null,
        CancellationToken ct = default)
    {
        var request = new RegisterRequest
        {
            Email = email,
            Password = password,
            DisplayName = displayName,
            InviteCode = inviteCode,
        };
        var response = await PostAsync<AuthResponse>("api/auth/register", request, ct);
        return response;
    }

    // ── Channels ────────────────────────────────────────────────────────

    public async Task<List<ChannelResponse>> GetChannelsAsync(CancellationToken ct = default)
    {
        return await GetAsync<List<ChannelResponse>>("api/channels", ct) ?? new List<ChannelResponse>();
    }

    public async Task<ChannelResponse?> GetChannelAsync(Guid id, CancellationToken ct = default)
    {
        return await GetAsync<ChannelResponse>($"api/channels/{id}", ct);
    }

    public async Task<ChannelResponse?> CreateChannelAsync(
        string name,
        string? topic,
        ChannelType type,
        CancellationToken ct = default)
    {
        var request = new CreateChannelRequest { Name = name, Topic = topic, Type = type };
        return await PostAsync<ChannelResponse>("api/channels", request, ct);
    }

    public async Task<bool> UpdateChannelAsync(Guid id, string? name, string? topic, CancellationToken ct = default)
    {
        var request = new UpdateChannelRequest { Name = name, Topic = topic };
        return await PutAsync($"api/channels/{id}", request, ct);
    }

    public async Task<bool> DeleteChannelAsync(Guid id, CancellationToken ct = default)
    {
        return await DeleteAsync($"api/channels/{id}", ct);
    }

    // ── Messages ────────────────────────────────────────────────────────

    public async Task<List<MessageResponse>> GetMessagesAsync(
        Guid channelId,
        DateTime? before = null,
        int limit = 50,
        CancellationToken ct = default)
    {
        var url = $"api/channels/{channelId}/messages?limit={limit}";
        if (before.HasValue)
        {
            url += $"&before={before.Value:O}";
        }

        return await GetAsync<List<MessageResponse>>(url, ct) ?? new List<MessageResponse>();
    }

    public async Task<MessageResponse?> GetMessageAsync(Guid id, CancellationToken ct = default)
    {
        return await GetAsync<MessageResponse>($"api/messages/{id}", ct);
    }

    // ── HTTP Helpers ────────────────────────────────────────────────────

    private void SetAuthHeader()
    {
        var token = _authState.AccessToken;
        if (!string.IsNullOrEmpty(token))
        {
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        }
        else
        {
            _http.DefaultRequestHeaders.Authorization = null;
        }
    }

    private void HandleUnauthorized()
    {
        _logger.LogWarning("Received 401 Unauthorized response, clearing auth state");
        _authState.SetLoggedOut();
    }

    private async Task<T?> GetAsync<T>(string url, CancellationToken ct) where T : class
    {
        try
        {
            SetAuthHeader();
            var response = await _http.GetAsync(url, ct);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                HandleUnauthorized();
                return default;
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "GET {Url} returned {StatusCode}",
                    url,
                    (int)response.StatusCode);
                return default;
            }

            return await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed for GET {Url}", url);
            return default;
        }
        catch (TaskCanceledException)
        {
            // Request was cancelled — let it propagate silently
            return default;
        }
    }

    private async Task<TResponse?> PostAsync<TResponse>(string url, object payload, CancellationToken ct)
        where TResponse : class
    {
        try
        {
            SetAuthHeader();
            var response = await _http.PostAsJsonAsync(url, payload, ct);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                HandleUnauthorized();
                return default;
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "POST {Url} returned {StatusCode}",
                    url,
                    (int)response.StatusCode);
                return default;
            }

            return await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken: ct);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed for POST {Url}", url);
            return default;
        }
        catch (TaskCanceledException)
        {
            return default;
        }
    }

    private async Task<bool> PutAsync(string url, object payload, CancellationToken ct)
    {
        try
        {
            SetAuthHeader();
            var response = await _http.PutAsJsonAsync(url, payload, ct);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                HandleUnauthorized();
                return false;
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "PUT {Url} returned {StatusCode}",
                    url,
                    (int)response.StatusCode);
                return false;
            }

            return true;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed for PUT {Url}", url);
            return false;
        }
        catch (TaskCanceledException)
        {
            return false;
        }
    }

    private async Task<bool> DeleteAsync(string url, CancellationToken ct)
    {
        try
        {
            SetAuthHeader();
            var response = await _http.DeleteAsync(url, ct);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                HandleUnauthorized();
                return false;
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "DELETE {Url} returned {StatusCode}",
                    url,
                    (int)response.StatusCode);
                return false;
            }

            return true;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed for DELETE {Url}", url);
            return false;
        }
        catch (TaskCanceledException)
        {
            return false;
        }
    }
}
