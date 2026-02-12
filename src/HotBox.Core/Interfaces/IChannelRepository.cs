using HotBox.Core.Entities;
using HotBox.Core.Enums;

namespace HotBox.Core.Interfaces;

public interface IChannelRepository
{
    Task<Channel?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<Channel>> GetAllAsync(CancellationToken ct = default);

    Task<IReadOnlyList<Channel>> GetByTypeAsync(ChannelType type, CancellationToken ct = default);

    Task<Channel> CreateAsync(Channel channel, CancellationToken ct = default);

    Task UpdateAsync(Channel channel, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);

    Task<bool> ExistsByNameAsync(string name, Guid? excludeId = null, CancellationToken ct = default);

    Task<int> GetMaxSortOrderAsync(CancellationToken ct = default);
}
