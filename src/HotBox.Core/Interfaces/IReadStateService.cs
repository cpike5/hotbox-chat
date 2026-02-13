namespace HotBox.Core.Interfaces;

/// <summary>
/// Manages read state tracking for channels and direct messages.
/// Tracks when users have read messages and provides unread counts.
/// </summary>
public interface IReadStateService
{
    /// <summary>
    /// Marks a channel as read for a user up to the latest message.
    /// Creates or updates the UserChannelRead record.
    /// </summary>
    /// <param name="userId">The user marking the channel as read</param>
    /// <param name="channelId">The channel being marked as read</param>
    /// <param name="ct">Cancellation token</param>
    Task MarkAsReadAsync(Guid userId, Guid channelId, CancellationToken ct = default);

    /// <summary>
    /// Gets the unread message count for a specific channel.
    /// Returns total message count if user has never read the channel.
    /// </summary>
    /// <param name="userId">The user to check unread count for</param>
    /// <param name="channelId">The channel to check</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Number of unread messages</returns>
    Task<int> GetUnreadCountAsync(Guid userId, Guid channelId, CancellationToken ct = default);

    /// <summary>
    /// Gets unread counts for all text channels the user has access to.
    /// Returns a dictionary mapping channel ID to unread count.
    /// Efficient single-query implementation.
    /// </summary>
    /// <param name="userId">The user to get unread counts for</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Dictionary of channel ID to unread count</returns>
    Task<Dictionary<Guid, int>> GetAllUnreadCountsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Gets unread counts for all DM conversations where the user is the recipient.
    /// Returns a dictionary mapping the other user's ID to unread count.
    /// </summary>
    Task<Dictionary<Guid, int>> GetDmUnreadCountsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Marks all unread DMs from a specific sender as read for this user.
    /// Updates DirectMessage.ReadAtUtc for all unread messages in the conversation.
    /// </summary>
    Task MarkDmAsReadAsync(Guid userId, Guid otherUserId, CancellationToken ct = default);
}
