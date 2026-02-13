using System.Collections.Concurrent;
using HotBox.Core.Enums;
using HotBox.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace HotBox.Infrastructure.Services;

public class PresenceService : IPresenceService, IDisposable
{
    private readonly ILogger<PresenceService> _logger;

    /// <summary>
    /// Tracks all connection IDs for a given user.
    /// All access must be guarded by <see cref="_connectionLock"/>.
    /// </summary>
    private readonly Dictionary<Guid, HashSet<string>> _userConnections = new();

    /// <summary>
    /// Tracks the current status of each user (only for users who are not Offline).
    /// </summary>
    private readonly ConcurrentDictionary<Guid, UserStatus> _userStatuses = new();

    /// <summary>
    /// Tracks display names for online users.
    /// </summary>
    private readonly ConcurrentDictionary<Guid, string> _userDisplayNames = new();

    /// <summary>
    /// Tracks whether each user is an agent (bot).
    /// </summary>
    private readonly ConcurrentDictionary<Guid, bool> _userIsAgent = new();

    /// <summary>
    /// Tracks the last heartbeat time for each user (for idle detection).
    /// </summary>
    private readonly ConcurrentDictionary<Guid, DateTime> _lastHeartbeat = new();

    /// <summary>
    /// Tracks grace period timers for users who have disconnected all connections.
    /// </summary>
    private readonly ConcurrentDictionary<Guid, Timer> _graceTimers = new();

    /// <summary>
    /// Tracks idle timeout timers per user.
    /// </summary>
    private readonly ConcurrentDictionary<Guid, Timer> _idleTimers = new();

    /// <summary>
    /// Lock object for connection set modifications (HashSet is not thread-safe).
    /// </summary>
    private readonly object _connectionLock = new();

    private static readonly TimeSpan GracePeriod = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Event raised when a user's status changes. The ChatHub subscribes to this
    /// to broadcast status updates.
    /// </summary>
    public event Action<Guid, string, UserStatus, bool>? OnUserStatusChanged;

    private bool _disposed;

    public PresenceService(ILogger<PresenceService> logger)
    {
        _logger = logger;
    }

    public Task SetOnlineAsync(Guid userId, string connectionId, string displayName, bool isAgent = false)
    {
        // Cancel any pending grace timer for this user
        CancelGraceTimer(userId);

        lock (_connectionLock)
        {
            if (!_userConnections.TryGetValue(userId, out var connections))
            {
                connections = new HashSet<string>();
                _userConnections[userId] = connections;
            }

            connections.Add(connectionId);
        }

        _userDisplayNames[userId] = displayName;
        _userIsAgent[userId] = isAgent;
        _lastHeartbeat[userId] = DateTime.UtcNow;

        var previousStatus = GetStatus(userId);
        _userStatuses[userId] = UserStatus.Online;

        ResetIdleTimer(userId);

        if (previousStatus != UserStatus.Online)
        {
            _logger.LogInformation("User {UserId} ({DisplayName}) is now online", userId, displayName);
            OnUserStatusChanged?.Invoke(userId, displayName, UserStatus.Online, isAgent);
        }

        return Task.CompletedTask;
    }

    public Task SetIdleAsync(Guid userId)
    {
        if (!_userStatuses.ContainsKey(userId))
            return Task.CompletedTask;

        var currentStatus = GetStatus(userId);
        if (currentStatus == UserStatus.DoNotDisturb || currentStatus == UserStatus.Offline)
            return Task.CompletedTask;

        _userStatuses[userId] = UserStatus.Idle;
        var displayName = GetDisplayName(userId);
        var isAgent = GetIsAgent(userId);

        _logger.LogInformation("User {UserId} ({DisplayName}) is now idle", userId, displayName);
        OnUserStatusChanged?.Invoke(userId, displayName, UserStatus.Idle, isAgent);

        return Task.CompletedTask;
    }

    public Task SetOfflineAsync(Guid userId)
    {
        CancelGraceTimer(userId);
        CancelIdleTimer(userId);

        lock (_connectionLock)
        {
            _userConnections.Remove(userId);
        }

        var displayName = GetDisplayName(userId);
        var isAgent = GetIsAgent(userId);

        _userStatuses.TryRemove(userId, out _);
        _userDisplayNames.TryRemove(userId, out _);
        _userIsAgent.TryRemove(userId, out _);
        _lastHeartbeat.TryRemove(userId, out _);

        _logger.LogInformation("User {UserId} ({DisplayName}) is now offline", userId, displayName);
        OnUserStatusChanged?.Invoke(userId, displayName, UserStatus.Offline, isAgent);

        return Task.CompletedTask;
    }

    public Task SetDoNotDisturbAsync(Guid userId)
    {
        if (!_userStatuses.ContainsKey(userId))
            return Task.CompletedTask;

        _userStatuses[userId] = UserStatus.DoNotDisturb;
        CancelIdleTimer(userId);

        var displayName = GetDisplayName(userId);
        var isAgent = GetIsAgent(userId);

        _logger.LogInformation("User {UserId} ({DisplayName}) set status to DoNotDisturb", userId, displayName);
        OnUserStatusChanged?.Invoke(userId, displayName, UserStatus.DoNotDisturb, isAgent);

        return Task.CompletedTask;
    }

    public UserStatus GetStatus(Guid userId)
    {
        return _userStatuses.TryGetValue(userId, out var status) ? status : UserStatus.Offline;
    }

    public IReadOnlyList<(Guid UserId, string DisplayName, UserStatus Status, bool IsAgent)> GetAllOnlineUsers()
    {
        return _userStatuses
            .Select(kvp => (
                UserId: kvp.Key,
                DisplayName: GetDisplayName(kvp.Key),
                Status: kvp.Value,
                IsAgent: GetIsAgent(kvp.Key)))
            .ToList()
            .AsReadOnly();
    }

    public bool RemoveConnection(Guid userId, string connectionId)
    {
        bool hasNoConnections;

        lock (_connectionLock)
        {
            if (!_userConnections.TryGetValue(userId, out var connections))
                return true;

            connections.Remove(connectionId);
            hasNoConnections = connections.Count == 0;
        }

        if (hasNoConnections)
        {
            StartGraceTimer(userId);
            return true;
        }

        return false;
    }

    public void RecordHeartbeat(Guid userId)
    {
        if (!_userStatuses.ContainsKey(userId))
            return;

        _lastHeartbeat[userId] = DateTime.UtcNow;

        // If user was idle, bring them back online
        if (_userStatuses.TryGetValue(userId, out var status) && status == UserStatus.Idle)
        {
            _userStatuses[userId] = UserStatus.Online;
            var displayName = GetDisplayName(userId);
            var isAgent = GetIsAgent(userId);
            OnUserStatusChanged?.Invoke(userId, displayName, UserStatus.Online, isAgent);
        }

        ResetIdleTimer(userId);
    }

    private string GetDisplayName(Guid userId)
    {
        return _userDisplayNames.TryGetValue(userId, out var name) ? name : "Unknown";
    }

    private bool GetIsAgent(Guid userId)
    {
        return _userIsAgent.TryGetValue(userId, out var isAgent) && isAgent;
    }

    private void StartGraceTimer(Guid userId)
    {
        CancelGraceTimer(userId);

        var timer = new Timer(
            _ => Task.Run(async () =>
            {
                try { await OnGracePeriodExpiredAsync(userId); }
                catch (Exception ex) { _logger.LogError(ex, "Grace period callback failed for {UserId}", userId); }
            }),
            null,
            GracePeriod,
            Timeout.InfiniteTimeSpan);

        _graceTimers[userId] = timer;

        _logger.LogDebug(
            "Started 30-second grace period for user {UserId}",
            userId);
    }

    private Task OnGracePeriodExpiredAsync(Guid userId)
    {
        // Verify user still has no connections and remove atomically within the lock
        // to prevent a race where a user reconnects between the check and SetOfflineAsync.
        bool stillDisconnected;
        lock (_connectionLock)
        {
            stillDisconnected = !_userConnections.TryGetValue(userId, out var connections)
                                || connections.Count == 0;

            if (stillDisconnected)
            {
                _userConnections.Remove(userId);
            }
        }

        if (stillDisconnected)
        {
            // Connection removal already handled above under the lock;
            // SetOfflineAsync will clean up remaining state and raise the event.
            CancelIdleTimer(userId);

            var displayName = GetDisplayName(userId);
            var isAgent = GetIsAgent(userId);

            _userStatuses.TryRemove(userId, out _);
            _userDisplayNames.TryRemove(userId, out _);
            _userIsAgent.TryRemove(userId, out _);
            _lastHeartbeat.TryRemove(userId, out _);

            _logger.LogInformation("User {UserId} ({DisplayName}) is now offline", userId, displayName);
            OnUserStatusChanged?.Invoke(userId, displayName, UserStatus.Offline, isAgent);
        }

        return Task.CompletedTask;
    }

    private void CancelGraceTimer(Guid userId)
    {
        if (_graceTimers.TryRemove(userId, out var timer))
        {
            timer.Dispose();
        }
    }

    private void ResetIdleTimer(Guid userId)
    {
        CancelIdleTimer(userId);

        // Don't set idle timer for DoNotDisturb users
        if (_userStatuses.TryGetValue(userId, out var status) && status == UserStatus.DoNotDisturb)
            return;

        var timer = new Timer(
            _ => Task.Run(async () =>
            {
                try { await OnIdleTimeoutExpiredAsync(userId); }
                catch (Exception ex) { _logger.LogError(ex, "Idle timeout callback failed for {UserId}", userId); }
            }),
            null,
            IdleTimeout,
            Timeout.InfiniteTimeSpan);

        _idleTimers[userId] = timer;
    }

    private Task OnIdleTimeoutExpiredAsync(Guid userId)
    {
        var lastBeat = _lastHeartbeat.TryGetValue(userId, out var ts) ? ts : DateTime.MinValue;
        var elapsed = DateTime.UtcNow - lastBeat;

        if (elapsed >= IdleTimeout)
        {
            return SetIdleAsync(userId);
        }

        // Heartbeat came in after timer was set; reschedule
        var remaining = IdleTimeout - elapsed;
        ResetIdleTimer(userId);
        return Task.CompletedTask;
    }

    private void CancelIdleTimer(Guid userId)
    {
        if (_idleTimers.TryRemove(userId, out var timer))
        {
            timer.Dispose();
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        foreach (var timer in _graceTimers.Values)
        {
            timer.Dispose();
        }
        _graceTimers.Clear();

        foreach (var timer in _idleTimers.Values)
        {
            timer.Dispose();
        }
        _idleTimers.Clear();
    }
}
