using HotBox.Core.Entities;
using HotBox.Core.Enums;

namespace HotBox.Core.Interfaces;

public interface IChannelService
{
    Task<Channel> CreateAsync(Guid userId, string name, string? description, ChannelType type, CancellationToken ct = default);

    Task UpdateAsync(Guid userId, Guid channelId, string name, string? description, CancellationToken ct = default);

    Task DeleteAsync(Guid userId, Guid channelId, CancellationToken ct = default);

    Task<IReadOnlyList<Channel>> GetAllAsync(CancellationToken ct = default);

    Task<Channel?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<Channel>> GetByTypeAsync(ChannelType type, CancellationToken ct = default);
}
