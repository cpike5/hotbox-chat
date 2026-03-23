using System.Collections.Concurrent;
using HotBox.Core.Enums;
using HotBox.Core.Interfaces;
using HotBox.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HotBox.Infrastructure.Services;

public class PresenceService : IPresenceService
{
    private readonly ConcurrentDictionary<Guid, UserPresenceState> _users = new();
    private readonly PresenceOptions _options;
    private readonly ILogger<PresenceService> _logger;

    /// <summary>
    /// Fired when a user's status changes. Parameters: userId, displayName, newStatus, isAgent.
    /// </summary>
    public event Action<Guid, string, UserStatus, bool>? OnUserStatusChanged;

    public PresenceService(IOptions<PresenceOptions> options, ILogger<PresenceService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public Task SetOnlineAsync(Guid userId, string connectionId, string displayName, bool isAgent = false)
    {
        var state = _users.GetOrAdd(userId, _ => new UserPresenceState
        {
            DisplayName = displayName,
            IsAgent = isAgent
        });

        state.DisplayName = displayName;
        state.IsAgent = isAgent;
        state.Connections.Add(connectionId);
        state.LastHeartbeat = DateTime.UtcNow;

        // Cancel any pending grace period timer
        state.GracePeriodCts?.Cancel();
        state.GracePeriodCts?.Dispose();
        state.GracePeriodCts = null;

        var previousStatus = state.Status;
        state.Status = UserStatus.Online;

        if (previousStatus != UserStatus.Online)
        {
            _logger.LogInformation("User {UserId} ({DisplayName}) is now online", userId, displayName);
            OnUserStatusChanged?.Invoke(userId, displayName, UserStatus.Online, isAgent);
        }

        return Task.CompletedTask;
    }

    public Task SetIdleAsync(Guid userId)
    {
        if (_users.TryGetValue(userId, out var state) && state.Status == UserStatus.Online)
        {
            state.Status = UserStatus.Idle;
            _logger.LogDebug("User {UserId} ({DisplayName}) is now idle", userId, state.DisplayName);
            OnUserStatusChanged?.Invoke(userId, state.DisplayName, UserStatus.Idle, state.IsAgent);
        }

        return Task.CompletedTask;
    }

    public Task SetOfflineAsync(Guid userId)
    {
        if (_users.TryRemove(userId, out var state))
        {
            state.GracePeriodCts?.Cancel();
            state.GracePeriodCts?.Dispose();
            state.Connections.Clear();

            _logger.LogInformation("User {UserId} ({DisplayName}) is now offline", userId, state.DisplayName);
            OnUserStatusChanged?.Invoke(userId, state.DisplayName, UserStatus.Offline, state.IsAgent);
        }

        return Task.CompletedTask;
    }

    public Task SetDoNotDisturbAsync(Guid userId)
    {
        if (_users.TryGetValue(userId, out var state))
        {
            state.Status = UserStatus.DoNotDisturb;
            _logger.LogDebug("User {UserId} ({DisplayName}) is now do-not-disturb", userId, state.DisplayName);
            OnUserStatusChanged?.Invoke(userId, state.DisplayName, UserStatus.DoNotDisturb, state.IsAgent);
        }

        return Task.CompletedTask;
    }

    public UserStatus GetStatus(Guid userId)
    {
        return _users.TryGetValue(userId, out var state) ? state.Status : UserStatus.Offline;
    }

    public IReadOnlyList<(Guid UserId, string DisplayName, UserStatus Status, bool IsAgent)> GetAllOnlineUsers()
    {
        return _users
            .Where(kvp => kvp.Value.Status != UserStatus.Offline)
            .Select(kvp => (kvp.Key, kvp.Value.DisplayName, kvp.Value.Status, kvp.Value.IsAgent))
            .ToList();
    }

    public bool RemoveConnection(Guid userId, string connectionId)
    {
        if (!_users.TryGetValue(userId, out var state))
        {
            return false;
        }

        state.Connections.Remove(connectionId);

        if (state.Connections.Count == 0)
        {
            // Start grace period
            state.GracePeriodCts?.Cancel();
            state.GracePeriodCts?.Dispose();
            state.GracePeriodCts = new CancellationTokenSource();
            var cts = state.GracePeriodCts;

            _ = StartGracePeriodAsync(userId, cts.Token);

            return true;
        }

        return false;
    }

    public void RecordHeartbeat(Guid userId)
    {
        if (_users.TryGetValue(userId, out var state))
        {
            state.LastHeartbeat = DateTime.UtcNow;

            // If user was idle, set them back to online
            if (state.Status == UserStatus.Idle)
            {
                state.Status = UserStatus.Online;
                OnUserStatusChanged?.Invoke(userId, state.DisplayName, UserStatus.Online, state.IsAgent);
            }
        }
    }

    public Task TouchAgentActivityAsync(Guid userId, string displayName)
    {
        var state = _users.GetOrAdd(userId, _ => new UserPresenceState
        {
            DisplayName = displayName,
            IsAgent = true
        });

        state.DisplayName = displayName;
        state.IsAgent = true;
        state.LastHeartbeat = DateTime.UtcNow;

        // Cancel any pending grace period or agent inactivity timer
        state.GracePeriodCts?.Cancel();
        state.GracePeriodCts?.Dispose();
        state.AgentInactivityCts?.Cancel();
        state.AgentInactivityCts?.Dispose();

        var previousStatus = state.Status;
        state.Status = UserStatus.Online;

        if (previousStatus != UserStatus.Online)
        {
            _logger.LogInformation("Agent {UserId} ({DisplayName}) is now online via API activity", userId, displayName);
            OnUserStatusChanged?.Invoke(userId, displayName, UserStatus.Online, true);
        }

        // Start agent inactivity timer
        state.AgentInactivityCts = new CancellationTokenSource();
        var cts = state.AgentInactivityCts;
        _ = StartAgentInactivityTimerAsync(userId, cts.Token);

        return Task.CompletedTask;
    }

    private async Task StartGracePeriodAsync(Guid userId, CancellationToken ct)
    {
        try
        {
            await Task.Delay(_options.GracePeriod, ct);
            await SetOfflineAsync(userId);
        }
        catch (TaskCanceledException)
        {
            // Grace period was cancelled (user reconnected)
        }
    }

    private async Task StartAgentInactivityTimerAsync(Guid userId, CancellationToken ct)
    {
        try
        {
            await Task.Delay(_options.AgentInactivityTimeout, ct);
            await SetOfflineAsync(userId);
        }
        catch (TaskCanceledException)
        {
            // Timer was cancelled (agent had new activity)
        }
    }

    private sealed class UserPresenceState
    {
        public string DisplayName { get; set; } = string.Empty;
        public UserStatus Status { get; set; } = UserStatus.Online;
        public bool IsAgent { get; set; }
        public HashSet<string> Connections { get; } = [];
        public DateTime LastHeartbeat { get; set; }
        public CancellationTokenSource? GracePeriodCts { get; set; }
        public CancellationTokenSource? AgentInactivityCts { get; set; }
    }
}
