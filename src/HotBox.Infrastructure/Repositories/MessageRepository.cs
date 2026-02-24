using HotBox.Core.Entities;
using HotBox.Core.Interfaces;
using HotBox.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HotBox.Infrastructure.Repositories;

public class MessageRepository : IMessageRepository
{
    private readonly HotBoxDbContext _context;

    public MessageRepository(HotBoxDbContext context)
    {
        _context = context;
    }

    public async Task<Message?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Messages
            .AsNoTracking()
            .Include(m => m.Author)
            .FirstOrDefaultAsync(m => m.Id == id, ct);
    }

    public async Task<IReadOnlyList<Message>> GetByChannelAsync(
        Guid channelId,
        DateTime? before,
        int limit = 50,
        CancellationToken ct = default)
    {
        var query = _context.Messages
            .AsNoTracking()
            .Include(m => m.Author)
            .Where(m => m.ChannelId == channelId);

        if (before.HasValue)
        {
            query = query.Where(m => m.CreatedAtUtc < before.Value);
        }

        var messages = await query
            .OrderByDescending(m => m.CreatedAtUtc)
            .Take(limit)
            .ToListAsync(ct);

        messages.Reverse();
        return messages;
    }

    public async Task<IReadOnlyList<Message>> GetAroundAsync(
        Guid channelId,
        Guid messageId,
        int context = 25,
        CancellationToken ct = default)
    {
        var target = await _context.Messages
            .AsNoTracking()
            .Where(m => m.Id == messageId && m.ChannelId == channelId)
            .Select(m => new { m.CreatedAtUtc })
            .FirstOrDefaultAsync(ct);

        if (target is null)
            return Array.Empty<Message>();

        var before = await _context.Messages
            .AsNoTracking()
            .Include(m => m.Author)
            .Where(m => m.ChannelId == channelId && m.CreatedAtUtc < target.CreatedAtUtc)
            .OrderByDescending(m => m.CreatedAtUtc)
            .Take(context)
            .ToListAsync(ct);

        var targetAndAfter = await _context.Messages
            .AsNoTracking()
            .Include(m => m.Author)
            .Where(m => m.ChannelId == channelId && m.CreatedAtUtc >= target.CreatedAtUtc)
            .OrderBy(m => m.CreatedAtUtc)
            .Take(context + 1)
            .ToListAsync(ct);

        before.Reverse();
        before.AddRange(targetAndAfter);
        return before;
    }

    public async Task<Message> CreateAsync(Message message, CancellationToken ct = default)
    {
        _context.Messages.Add(message);
        await _context.SaveChangesAsync(ct);

        return await _context.Messages
            .AsNoTracking()
            .Include(m => m.Author)
            .FirstAsync(m => m.Id == message.Id, ct);
    }
}
