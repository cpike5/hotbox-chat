using HotBox.Core.Enums;

namespace HotBox.Core.Interfaces;

public interface IPresenceService
{
    /// <summary>
    /// Registers a connection and marks the user as online.
    /// </summary>
    Task SetOnlineAsync(Guid userId, string connectionId, string displayName, bool isAgent = false);

    /// <summary>
    /// Marks the user as idle (triggered by inactivity timeout).
    /// </summary>
    Task SetIdleAsync(Guid userId);

    /// <summary>
    /// Marks the user as offline, removing all connections.
    /// </summary>
    Task SetOfflineAsync(Guid userId);

    /// <summary>
    /// Marks the user as Do Not Disturb.
    /// </summary>
    Task SetDoNotDisturbAsync(Guid userId);

    /// <summary>
    /// Gets the current status of a user.
    /// </summary>
    UserStatus GetStatus(Guid userId);

    /// <summary>
    /// Gets all users currently not offline (Online, Idle, or DoNotDisturb).
    /// </summary>
    IReadOnlyList<(Guid UserId, string DisplayName, UserStatus Status, bool IsAgent)> GetAllOnlineUsers();

    /// <summary>
    /// Removes a specific connection. If this was the user's last connection,
    /// starts the 30-second grace period before marking offline.
    /// Returns true if the user has no remaining connections (grace period started).
    /// </summary>
    bool RemoveConnection(Guid userId, string connectionId);

    /// <summary>
    /// Records a heartbeat from the client, resetting the idle timer.
    /// </summary>
    void RecordHeartbeat(Guid userId);

    /// <summary>
    /// Marks an agent as active based on API activity and keeps it online
    /// until agent inactivity timeout expires.
    /// </summary>
    Task TouchAgentActivityAsync(Guid userId, string displayName);
}
