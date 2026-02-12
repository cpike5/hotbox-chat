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
