using HotBox.Core.Entities;
using HotBox.Core.Interfaces;
using HotBox.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HotBox.Infrastructure.Repositories;

public class MessageRepository : IMessageRepository
{
    private readonly HotBoxDbContext _dbContext;

    public MessageRepository(HotBoxDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Message?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _dbContext.Messages
            .Include(m => m.User)
            .Include(m => m.Channel)
            .FirstOrDefaultAsync(m => m.Id == id, ct);
    }

    public async Task<IReadOnlyList<Message>> GetByChannelAsync(
        Guid channelId,
        DateTime? before,
        int limit = 50,
        CancellationToken ct = default)
    {
        var query = _dbContext.Messages
            .Where(m => m.ChannelId == channelId);

        if (before.HasValue)
        {
            query = query.Where(m => m.CreatedAt < before.Value);
        }

        return await query
            .OrderByDescending(m => m.CreatedAt)
            .Take(limit)
            .Include(m => m.User)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Message>> GetAroundAsync(
        Guid channelId,
        Guid messageId,
        int context = 25,
        CancellationToken ct = default)
    {
        var targetMessage = await _dbContext.Messages
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == messageId && m.ChannelId == channelId, ct);

        if (targetMessage is null)
        {
            return [];
        }

        var before = await _dbContext.Messages
            .Where(m => m.ChannelId == channelId && m.CreatedAt < targetMessage.CreatedAt)
            .OrderByDescending(m => m.CreatedAt)
            .Take(context)
            .Include(m => m.User)
            .AsNoTracking()
            .ToListAsync(ct);

        var after = await _dbContext.Messages
            .Where(m => m.ChannelId == channelId && m.CreatedAt > targetMessage.CreatedAt)
            .OrderBy(m => m.CreatedAt)
            .Take(context)
            .Include(m => m.User)
            .AsNoTracking()
            .ToListAsync(ct);

        // Load the user for the target message
        var targetWithUser = await _dbContext.Messages
            .Include(m => m.User)
            .AsNoTracking()
            .FirstAsync(m => m.Id == messageId, ct);

        var result = new List<Message>(before.Count + 1 + after.Count);
        result.AddRange(before.OrderBy(m => m.CreatedAt));
        result.Add(targetWithUser);
        result.AddRange(after);

        return result;
    }

    public async Task<Message> CreateAsync(Message message, CancellationToken ct = default)
    {
        _dbContext.Messages.Add(message);
        await _dbContext.SaveChangesAsync(ct);

        // Reload with User navigation property
        await _dbContext.Entry(message).Reference(m => m.User).LoadAsync(ct);

        return message;
    }
}
