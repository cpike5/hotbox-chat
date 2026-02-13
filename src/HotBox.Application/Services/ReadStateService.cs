using HotBox.Core.Entities;
using HotBox.Core.Enums;
using HotBox.Core.Interfaces;
using HotBox.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HotBox.Application.Services;

public class ReadStateService : IReadStateService
{
    private readonly HotBoxDbContext _dbContext;
    private readonly ILogger<ReadStateService> _logger;

    public ReadStateService(
        HotBoxDbContext dbContext,
        ILogger<ReadStateService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task MarkAsReadAsync(Guid userId, Guid channelId, CancellationToken ct = default)
    {
        // Find the latest message in the channel
        var latestMessage = await _dbContext.Messages
            .Where(m => m.ChannelId == channelId)
            .OrderByDescending(m => m.CreatedAtUtc)
            .Select(m => new { m.Id, m.CreatedAtUtc })
            .FirstOrDefaultAsync(ct);

        if (latestMessage == null)
        {
            // No messages in channel — nothing to mark as read
            _logger.LogDebug(
                "No messages found in channel {ChannelId} for user {UserId} to mark as read",
                channelId, userId);
            return;
        }

        // Upsert UserChannelRead
        var existingRead = await _dbContext.UserChannelReads
            .FirstOrDefaultAsync(r => r.UserId == userId && r.ChannelId == channelId, ct);

        if (existingRead != null)
        {
            existingRead.LastReadMessageId = latestMessage.Id;
            existingRead.LastReadAtUtc = DateTime.UtcNow;

            _logger.LogDebug(
                "Updated read state for user {UserId} in channel {ChannelId} to message {MessageId}",
                userId, channelId, latestMessage.Id);
        }
        else
        {
            var newRead = new UserChannelRead
            {
                UserId = userId,
                ChannelId = channelId,
                LastReadMessageId = latestMessage.Id,
                LastReadAtUtc = DateTime.UtcNow
            };

            _dbContext.UserChannelReads.Add(newRead);

            _logger.LogDebug(
                "Created read state for user {UserId} in channel {ChannelId} at message {MessageId}",
                userId, channelId, latestMessage.Id);
        }

        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task<int> GetUnreadCountAsync(Guid userId, Guid channelId, CancellationToken ct = default)
    {
        var readState = await _dbContext.UserChannelReads
            .FirstOrDefaultAsync(r => r.UserId == userId && r.ChannelId == channelId, ct);

        if (readState == null || readState.LastReadMessageId == null)
        {
            // No read state — return total message count
            var totalCount = await _dbContext.Messages
                .CountAsync(m => m.ChannelId == channelId, ct);

            _logger.LogDebug(
                "No read state for user {UserId} in channel {ChannelId}, returning total count: {Count}",
                userId, channelId, totalCount);

            return totalCount;
        }

        // Count messages created after the last read message
        var lastReadTimestamp = await _dbContext.Messages
            .Where(m => m.Id == readState.LastReadMessageId)
            .Select(m => m.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

        if (lastReadTimestamp == default)
        {
            // Last read message no longer exists — return total count
            var totalCount = await _dbContext.Messages
                .CountAsync(m => m.ChannelId == channelId, ct);

            _logger.LogWarning(
                "Last read message {MessageId} not found for user {UserId} in channel {ChannelId}, returning total count: {Count}",
                readState.LastReadMessageId, userId, channelId, totalCount);

            return totalCount;
        }

        var unreadCount = await _dbContext.Messages
            .CountAsync(m => m.ChannelId == channelId && m.CreatedAtUtc > lastReadTimestamp, ct);

        _logger.LogDebug(
            "User {UserId} has {UnreadCount} unread messages in channel {ChannelId}",
            userId, unreadCount, channelId);

        return unreadCount;
    }

    public async Task<Dictionary<Guid, int>> GetAllUnreadCountsAsync(Guid userId, CancellationToken ct = default)
    {
        // Query 1: Total message counts per text channel (DB-side GROUP BY)
        var totalCounts = await _dbContext.Messages
            .Where(m => m.Channel.Type == ChannelType.Text)
            .GroupBy(m => m.ChannelId)
            .Select(g => new { ChannelId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        if (totalCounts.Count == 0)
        {
            return new Dictionary<Guid, int>();
        }

        // Query 2: Read states with last-read message timestamps (join avoids N+1)
        var readStates = await _dbContext.UserChannelReads
            .Where(r => r.UserId == userId && r.LastReadMessageId != null)
            .Join(
                _dbContext.Messages,
                r => r.LastReadMessageId,
                m => m.Id,
                (r, m) => new { r.ChannelId, LastReadTime = m.CreatedAtUtc })
            .ToListAsync(ct);

        var readStateDict = readStates.ToDictionary(r => r.ChannelId, r => r.LastReadTime);

        // Query 3: For channels with read state, count messages after the read position.
        // We iterate channels (typically 5-15, not users/messages) so this is bounded.
        var result = new Dictionary<Guid, int>();

        foreach (var tc in totalCounts)
        {
            if (!readStateDict.TryGetValue(tc.ChannelId, out var lastReadTime))
            {
                // No read state — all messages are unread
                result[tc.ChannelId] = tc.Count;
            }
            else
            {
                // DB-side COUNT of messages after the read position
                var unreadCount = await _dbContext.Messages
                    .CountAsync(m => m.ChannelId == tc.ChannelId && m.CreatedAtUtc > lastReadTime, ct);

                result[tc.ChannelId] = unreadCount;
            }
        }

        _logger.LogDebug(
            "Retrieved unread counts for {ChannelCount} channels for user {UserId}",
            totalCounts.Count, userId);

        return result;
    }

    public async Task<Dictionary<Guid, int>> GetDmUnreadCountsAsync(Guid userId, CancellationToken ct = default)
    {
        // Find all unread DMs where this user is the recipient
        var unreadDms = await _dbContext.DirectMessages
            .Where(dm => dm.RecipientId == userId && dm.ReadAtUtc == null)
            .GroupBy(dm => dm.SenderId)
            .Select(g => new
            {
                SenderId = g.Key,
                UnreadCount = g.Count()
            })
            .ToListAsync(ct);

        var result = unreadDms.ToDictionary(
            dm => dm.SenderId,
            dm => dm.UnreadCount);

        _logger.LogDebug(
            "Retrieved DM unread counts for {ConversationCount} conversations for user {UserId}",
            result.Count, userId);

        return result;
    }

    public async Task MarkDmAsReadAsync(Guid userId, Guid otherUserId, CancellationToken ct = default)
    {
        var unreadMessages = await _dbContext.DirectMessages
            .Where(dm => dm.RecipientId == userId
                         && dm.SenderId == otherUserId
                         && dm.ReadAtUtc == null)
            .ToListAsync(ct);

        if (unreadMessages.Count == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        foreach (var message in unreadMessages)
        {
            message.ReadAtUtc = now;
        }

        await _dbContext.SaveChangesAsync(ct);

        _logger.LogDebug(
            "Marked {Count} DMs as read for user {UserId} from sender {SenderId}",
            unreadMessages.Count, userId, otherUserId);
    }
}
