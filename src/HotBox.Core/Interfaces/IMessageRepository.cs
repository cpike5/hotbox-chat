using HotBox.Core.Entities;

namespace HotBox.Core.Interfaces;

public interface IMessageRepository
{
    Task<Message?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<Message>> GetByChannelAsync(Guid channelId, DateTime? before, int limit = 50, CancellationToken ct = default);

    Task<Message> CreateAsync(Message message, CancellationToken ct = default);
}
