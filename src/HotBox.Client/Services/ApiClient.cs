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

    public async Task<List<ProviderInfo>> GetProvidersAsync(CancellationToken ct = default)
    {
        return await GetAsync<List<ProviderInfo>>("api/auth/providers", ct) ?? new List<ProviderInfo>();
    }

    public async Task<RegistrationModeResponse?> GetRegistrationModeAsync(CancellationToken ct = default)
    {
        return await GetAsync<RegistrationModeResponse>("api/auth/registration-mode", ct);
    }

    public async Task<AuthResponse?> RefreshTokenAsync(CancellationToken ct = default)
    {
        try
        {
            // The refresh token is in an HttpOnly cookie — no Authorization header needed
            var response = await _http.PostAsync("api/auth/refresh", null, ct);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<AuthResponse>(cancellationToken: ct);
        }
        catch
        {
            return null;
        }
    }

    public async Task LogoutAsync(CancellationToken ct = default)
    {
        try
        {
            SetAuthHeader();
            await _http.PostAsync("api/auth/logout", null, ct);
        }
        catch
        {
            // Best-effort logout — server cookie will eventually expire
        }
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

    // ── Direct Messages ────────────────────────────────────────────────

    public async Task<List<ConversationSummaryResponse>> GetConversationsAsync(CancellationToken ct = default)
    {
        return await GetAsync<List<ConversationSummaryResponse>>("api/dm", ct) ?? new List<ConversationSummaryResponse>();
    }

    public async Task<List<DirectMessageResponse>> GetDirectMessagesAsync(
        Guid userId,
        DateTime? before = null,
        int limit = 50,
        CancellationToken ct = default)
    {
        var url = $"api/dm/{userId}?limit={limit}";
        if (before.HasValue)
        {
            url += $"&before={before.Value:O}";
        }

        return await GetAsync<List<DirectMessageResponse>>(url, ct) ?? new List<DirectMessageResponse>();
    }

    public async Task<DirectMessageResponse?> SendDirectMessageAsync(
        Guid userId,
        string content,
        CancellationToken ct = default)
    {
        var request = new { Content = content };
        return await PostAsync<DirectMessageResponse>($"api/dm/{userId}", request, ct);
    }

    // ── Admin ─────────────────────────────────────────────────────────

    public async Task<AdminSettingsResponse?> GetAdminSettingsAsync(CancellationToken ct = default)
    {
        return await GetAsync<AdminSettingsResponse>("api/admin/settings", ct);
    }

    public async Task<AdminSettingsResponse?> UpdateAdminSettingsAsync(
        string serverName,
        RegistrationMode registrationMode,
        CancellationToken ct = default)
    {
        var request = new { ServerName = serverName, RegistrationMode = registrationMode };
        return await PutReturningAsync<AdminSettingsResponse>("api/admin/settings", request, ct);
    }

    public async Task<List<AdminUserResponse>> GetAdminUsersAsync(CancellationToken ct = default)
    {
        return await GetAsync<List<AdminUserResponse>>("api/admin/users", ct) ?? new List<AdminUserResponse>();
    }

    public async Task<AdminUserResponse?> CreateAdminUserAsync(
        string email,
        string password,
        string displayName,
        string role,
        CancellationToken ct = default)
    {
        var request = new { Email = email, Password = password, DisplayName = displayName, Role = role };
        return await PostAsync<AdminUserResponse>("api/admin/users", request, ct);
    }

    public async Task<bool> ChangeUserRoleAsync(Guid userId, string role, CancellationToken ct = default)
    {
        var request = new { Role = role };
        return await PutAsync($"api/admin/users/{userId}/role", request, ct);
    }

    public async Task<bool> DeleteUserAsync(Guid userId, CancellationToken ct = default)
    {
        return await DeleteAsync($"api/admin/users/{userId}", ct);
    }

    public async Task<List<AdminInviteResponse>> GetAdminInvitesAsync(CancellationToken ct = default)
    {
        return await GetAsync<List<AdminInviteResponse>>("api/admin/invites", ct) ?? new List<AdminInviteResponse>();
    }

    public async Task<AdminInviteResponse?> GenerateInviteAsync(
        DateTime? expiresAtUtc,
        int? maxUses,
        CancellationToken ct = default)
    {
        var request = new { ExpiresAtUtc = expiresAtUtc, MaxUses = maxUses };
        return await PostAsync<AdminInviteResponse>("api/admin/invites", request, ct);
    }

    public async Task<bool> RevokeInviteAsync(string code, CancellationToken ct = default)
    {
        return await DeleteAsync($"api/admin/invites/{code}", ct);
    }

    // --- API Key Management ---

    public async Task<List<AdminApiKeyResponse>> GetAdminApiKeysAsync(CancellationToken ct = default)
    {
        return await GetAsync<List<AdminApiKeyResponse>>("api/admin/apikeys", ct) ?? new List<AdminApiKeyResponse>();
    }

    public async Task<CreateApiKeyResponse?> CreateAdminApiKeyAsync(string name, CancellationToken ct = default)
    {
        var request = new { Name = name };
        return await PostAsync<CreateApiKeyResponse>("api/admin/apikeys", request, ct);
    }

    public async Task<bool> RevokeAdminApiKeyAsync(Guid id, string? reason, CancellationToken ct = default)
    {
        var request = new { Reason = reason };
        return await PutAsync($"api/admin/apikeys/{id}/revoke", request, ct);
    }

    public async Task<bool> ReorderChannelsAsync(List<Guid> channelIds, CancellationToken ct = default)
    {
        var request = new { ChannelIds = channelIds };
        return await PutAsync("api/admin/channels/reorder", request, ct);
    }

    // ── Users ──────────────────────────────────────────────────────────────

    public async Task<List<UserSearchResult>> SearchUsersAsync(string? query = null, CancellationToken ct = default)
    {
        var url = "api/users/search";
        if (!string.IsNullOrWhiteSpace(query))
            url += $"?q={Uri.EscapeDataString(query)}";
        return await GetAsync<List<UserSearchResult>>(url, ct) ?? new List<UserSearchResult>();
    }

    public async Task<UserProfileResponse?> GetMyProfileAsync(CancellationToken ct = default)
    {
        return await GetAsync<UserProfileResponse>("api/users/me", ct);
    }

    public async Task<UserProfileResponse?> GetUserProfileAsync(Guid userId, CancellationToken ct = default)
    {
        return await GetAsync<UserProfileResponse>($"api/users/{userId}", ct);
    }

    public async Task<UserProfileResponse?> UpdateMyProfileAsync(
        UpdateProfileRequest request,
        CancellationToken ct = default)
    {
        return await PutReturningAsync<UserProfileResponse>("api/users/me", request, ct);
    }

    // ── Search ────────────────────────────────────────────────────────────

    public async Task<SearchResultModel?> SearchMessagesAsync(
        string query,
        Guid? channelId = null,
        string? cursor = null,
        int limit = 20,
        CancellationToken ct = default)
    {
        var url = $"api/search/messages?q={Uri.EscapeDataString(query)}&limit={limit}";
        if (channelId.HasValue) url += $"&channelId={channelId.Value}";
        if (cursor is not null) url += $"&cursor={Uri.EscapeDataString(cursor)}";
        return await GetAsync<SearchResultModel>(url, ct);
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

    private async Task<bool> HandleUnauthorizedAsync()
    {
        _logger.LogWarning("Received 401, attempting token refresh");
        var refreshResponse = await RefreshTokenAsync();
        if (refreshResponse is not null)
        {
            var userInfo = JwtParser.ParseUserInfoFromToken(refreshResponse.AccessToken);
            _authState.SetAuthenticated(refreshResponse.AccessToken, userInfo);
            return true;
        }

        _logger.LogWarning("Token refresh failed, logging out");
        _authState.SetLoggedOut();
        return false;
    }

    private async Task<T?> GetAsync<T>(string url, CancellationToken ct) where T : class
    {
        try
        {
            SetAuthHeader();
            var response = await _http.GetAsync(url, ct);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                await HandleUnauthorizedAsync();
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
                await HandleUnauthorizedAsync();
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

    private async Task<TResponse?> PutReturningAsync<TResponse>(string url, object payload, CancellationToken ct)
        where TResponse : class
    {
        try
        {
            SetAuthHeader();
            var response = await _http.PutAsJsonAsync(url, payload, ct);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                await HandleUnauthorizedAsync();
                return default;
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "PUT {Url} returned {StatusCode}",
                    url,
                    (int)response.StatusCode);
                return default;
            }

            return await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken: ct);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed for PUT {Url}", url);
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
                await HandleUnauthorizedAsync();
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
                await HandleUnauthorizedAsync();
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
