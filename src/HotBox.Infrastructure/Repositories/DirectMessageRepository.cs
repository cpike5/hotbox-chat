using HotBox.Core.Entities;
using HotBox.Core.Interfaces;
using HotBox.Core.Models;
using HotBox.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HotBox.Infrastructure.Repositories;

public class DirectMessageRepository : IDirectMessageRepository
{
    private readonly HotBoxDbContext _context;

    public DirectMessageRepository(HotBoxDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<DirectMessage>> GetConversationAsync(
        Guid userId,
        Guid otherUserId,
        DateTime? before,
        int limit = 50,
        CancellationToken ct = default)
    {
        var query = _context.DirectMessages
            .AsNoTracking()
            .Where(dm =>
                (dm.SenderId == userId && dm.RecipientId == otherUserId) ||
                (dm.SenderId == otherUserId && dm.RecipientId == userId));

        if (before.HasValue)
        {
            query = query.Where(dm => dm.CreatedAtUtc < before.Value);
        }

        var messages = await query
            .OrderByDescending(dm => dm.CreatedAtUtc)
            .Take(limit)
            .ToListAsync(ct);

        messages.Reverse();
        return messages;
    }

    public async Task<IReadOnlyList<ConversationSummary>> GetConversationsAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        return await _context.DirectMessages
            .AsNoTracking()
            .Where(dm => dm.SenderId == userId || dm.RecipientId == userId)
            .Select(dm => new
            {
                OtherUserId = dm.SenderId == userId ? dm.RecipientId : dm.SenderId,
                dm.CreatedAtUtc
            })
            .GroupBy(x => x.OtherUserId)
            .Select(g => new
            {
                UserId = g.Key,
                LastMessageAtUtc = g.Max(x => x.CreatedAtUtc)
            })
            .Join(
                _context.Users,
                dm => dm.UserId,
                u => u.Id,
                (dm, u) => new ConversationSummary(
                    dm.UserId,
                    u.DisplayName,
                    dm.LastMessageAtUtc))
            .OrderByDescending(c => c.LastMessageAtUtc)
            .ToListAsync(ct);
    }

    public async Task<DirectMessage> CreateAsync(DirectMessage message, CancellationToken ct = default)
    {
        _context.DirectMessages.Add(message);
        await _context.SaveChangesAsync(ct);
        return message;
    }
}
