using HotBox.Core.Entities;
using HotBox.Core.Interfaces;
using HotBox.Core.Models;
using HotBox.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HotBox.Infrastructure.Repositories;

public class DirectMessageRepository : IDirectMessageRepository
{
    private readonly HotBoxDbContext _dbContext;

    public DirectMessageRepository(HotBoxDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<DirectMessage>> GetConversationAsync(
        Guid userId,
        Guid otherUserId,
        DateTime? before,
        int limit = 50,
        CancellationToken ct = default)
    {
        var query = _dbContext.DirectMessages
            .Where(dm =>
                (dm.SenderId == userId && dm.RecipientId == otherUserId) ||
                (dm.SenderId == otherUserId && dm.RecipientId == userId));

        if (before.HasValue)
        {
            query = query.Where(dm => dm.CreatedAt < before.Value);
        }

        return await query
            .OrderByDescending(dm => dm.CreatedAt)
            .Take(limit)
            .Include(dm => dm.Sender)
            .Include(dm => dm.Recipient)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ConversationSummary>> GetConversationsAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        // Get the latest message for each conversation partner
        var sentConversations = _dbContext.DirectMessages
            .Where(dm => dm.SenderId == userId)
            .Select(dm => new { OtherUserId = dm.RecipientId, dm.CreatedAt, dm.Content, dm.Recipient.DisplayName });

        var receivedConversations = _dbContext.DirectMessages
            .Where(dm => dm.RecipientId == userId)
            .Select(dm => new { OtherUserId = dm.SenderId, dm.CreatedAt, dm.Content, dm.Sender.DisplayName });

        var allMessages = sentConversations.Union(receivedConversations);

        var conversations = await allMessages
            .GroupBy(m => m.OtherUserId)
            .Select(g => new
            {
                UserId = g.Key,
                LastMessage = g.OrderByDescending(m => m.CreatedAt).First()
            })
            .OrderByDescending(c => c.LastMessage.CreatedAt)
            .ToListAsync(ct);

        return conversations
            .Select(c => new ConversationSummary(
                c.UserId,
                c.LastMessage.DisplayName,
                c.LastMessage.CreatedAt,
                c.LastMessage.Content))
            .ToList();
    }

    public async Task<DirectMessage> CreateAsync(DirectMessage message, CancellationToken ct = default)
    {
        _dbContext.DirectMessages.Add(message);
        await _dbContext.SaveChangesAsync(ct);

        // Reload with navigation properties
        await _dbContext.Entry(message).Reference(dm => dm.Sender).LoadAsync(ct);
        await _dbContext.Entry(message).Reference(dm => dm.Recipient).LoadAsync(ct);

        return message;
    }
}
