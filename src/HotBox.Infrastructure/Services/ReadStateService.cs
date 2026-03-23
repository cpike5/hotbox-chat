using HotBox.Core.Entities;
using HotBox.Core.Enums;
using HotBox.Core.Interfaces;
using HotBox.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HotBox.Infrastructure.Services;

public class ReadStateService : IReadStateService
{
    private readonly HotBoxDbContext _dbContext;
    private readonly ILogger<ReadStateService> _logger;

    public ReadStateService(HotBoxDbContext dbContext, ILogger<ReadStateService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task MarkAsReadAsync(Guid userId, Guid channelId, CancellationToken ct = default)
    {
        var latestMessage = await _dbContext.Messages
            .Where(m => m.ChannelId == channelId)
            .OrderByDescending(m => m.CreatedAt)
            .Select(m => new { m.Id })
            .FirstOrDefaultAsync(ct);

        var readState = await _dbContext.UserChannelReads
            .FirstOrDefaultAsync(ucr => ucr.UserId == userId && ucr.ChannelId == channelId, ct);

        if (readState is not null)
        {
            readState.LastReadMessageId = latestMessage?.Id;
            readState.LastReadAt = DateTime.UtcNow;
        }
        else
        {
            _dbContext.UserChannelReads.Add(new UserChannelRead
            {
                UserId = userId,
                ChannelId = channelId,
                LastReadMessageId = latestMessage?.Id,
                LastReadAt = DateTime.UtcNow
            });
        }

        await _dbContext.SaveChangesAsync(ct);

        _logger.LogDebug("User {UserId} marked channel {ChannelId} as read", userId, channelId);
    }

    public async Task<int> GetUnreadCountAsync(Guid userId, Guid channelId, CancellationToken ct = default)
    {
        var readState = await _dbContext.UserChannelReads
            .AsNoTracking()
            .FirstOrDefaultAsync(ucr => ucr.UserId == userId && ucr.ChannelId == channelId, ct);

        if (readState?.LastReadMessageId is null)
        {
            // User has never read this channel - all messages are unread
            return await _dbContext.Messages
                .CountAsync(m => m.ChannelId == channelId, ct);
        }

        var lastReadMessage = await _dbContext.Messages
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == readState.LastReadMessageId, ct);

        if (lastReadMessage is null)
        {
            return 0;
        }

        return await _dbContext.Messages
            .CountAsync(m => m.ChannelId == channelId && m.CreatedAt > lastReadMessage.CreatedAt, ct);
    }

    public async Task<Dictionary<Guid, int>> GetAllUnreadCountsAsync(Guid userId, CancellationToken ct = default)
    {
        // Get all text channels
        var textChannels = await _dbContext.Channels
            .Where(c => c.Type == ChannelType.Text)
            .Select(c => c.Id)
            .ToListAsync(ct);

        // Get all read states for this user
        var readStates = await _dbContext.UserChannelReads
            .Where(ucr => ucr.UserId == userId)
            .Include(ucr => ucr.LastReadMessage)
            .AsNoTracking()
            .ToDictionaryAsync(ucr => ucr.ChannelId, ct);

        var result = new Dictionary<Guid, int>();

        foreach (var channelId in textChannels)
        {
            int unreadCount;

            if (readStates.TryGetValue(channelId, out var readState) && readState.LastReadMessage is not null)
            {
                unreadCount = await _dbContext.Messages
                    .CountAsync(m => m.ChannelId == channelId && m.CreatedAt > readState.LastReadMessage.CreatedAt, ct);
            }
            else
            {
                unreadCount = await _dbContext.Messages
                    .CountAsync(m => m.ChannelId == channelId, ct);
            }

            if (unreadCount > 0)
            {
                result[channelId] = unreadCount;
            }
        }

        return result;
    }

    public async Task<Dictionary<Guid, int>> GetDmUnreadCountsAsync(Guid userId, CancellationToken ct = default)
    {
        return await _dbContext.DirectMessages
            .Where(dm => dm.RecipientId == userId && dm.ReadAt == null)
            .GroupBy(dm => dm.SenderId)
            .Select(g => new { SenderId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.SenderId, x => x.Count, ct);
    }

    public async Task MarkDmAsReadAsync(Guid userId, Guid otherUserId, CancellationToken ct = default)
    {
        await _dbContext.DirectMessages
            .Where(dm => dm.RecipientId == userId && dm.SenderId == otherUserId && dm.ReadAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(dm => dm.ReadAt, DateTime.UtcNow), ct);

        _logger.LogDebug("User {UserId} marked DMs from {OtherUserId} as read", userId, otherUserId);
    }
}
