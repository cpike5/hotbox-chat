using HotBox.Core.Entities;

namespace HotBox.Core.Interfaces;

public interface IDemoUserService
{
    /// <summary>
    /// Creates a new demo user with the given username and display name.
    /// Returns null if capacity is reached or IP is cooling down.
    /// </summary>
    Task<AppUser?> CreateDemoUserAsync(string username, string displayName, string ipAddress, CancellationToken ct = default);

    /// <summary>
    /// Gets the count of currently active demo users.
    /// </summary>
    Task<int> GetActiveDemoUserCountAsync(CancellationToken ct = default);

    /// <summary>
    /// Checks whether the given IP address is within the cooldown window.
    /// </summary>
    Task<bool> IsIpCoolingDownAsync(string ipAddress, CancellationToken ct = default);

    /// <summary>
    /// Records activity for a demo user, resetting their inactivity timer.
    /// </summary>
    Task RecordActivityAsync(Guid userId);

    /// <summary>
    /// Returns the IDs of demo users whose sessions have expired due to inactivity.
    /// </summary>
    Task<List<Guid>> GetExpiredDemoUserIdsAsync(CancellationToken ct = default);

    /// <summary>
    /// Permanently removes a demo user and all associated data (messages, tokens, etc.).
    /// </summary>
    Task PurgeDemoUserAsync(Guid userId, CancellationToken ct = default);
}
