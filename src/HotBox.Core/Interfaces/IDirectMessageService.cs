using HotBox.Core.Entities;
using HotBox.Core.Models;

namespace HotBox.Core.Interfaces;

public interface IDirectMessageService
{
    Task<DirectMessage> SendAsync(Guid senderId, Guid recipientId, string content, CancellationToken ct = default);

    Task<IReadOnlyList<DirectMessage>> GetConversationAsync(Guid userId, Guid otherUserId, DateTime? before = null, int limit = 50, CancellationToken ct = default);

    Task<IReadOnlyList<ConversationSummary>> GetConversationsAsync(Guid userId, CancellationToken ct = default);
}
