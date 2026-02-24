using HotBox.Core.Entities;

namespace HotBox.Core.Interfaces;

public interface IMessageService
{
    Task<Message> SendAsync(Guid channelId, Guid authorId, string content, CancellationToken ct = default);

    Task<IReadOnlyList<Message>> GetByChannelAsync(Guid channelId, DateTime? before = null, int limit = 50, CancellationToken ct = default);

    Task<Message?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<Message>> GetAroundAsync(Guid channelId, Guid messageId, int context = 25, CancellationToken ct = default);
}
