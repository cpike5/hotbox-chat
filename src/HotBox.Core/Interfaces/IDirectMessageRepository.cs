using HotBox.Core.Entities;
using HotBox.Core.Models;

namespace HotBox.Core.Interfaces;

public interface IDirectMessageRepository
{
    Task<IReadOnlyList<DirectMessage>> GetConversationAsync(Guid userId, Guid otherUserId, DateTime? before, int limit = 50, CancellationToken ct = default);

    Task<IReadOnlyList<ConversationSummary>> GetConversationsAsync(Guid userId, CancellationToken ct = default);

    Task<DirectMessage> CreateAsync(DirectMessage message, CancellationToken ct = default);
}
