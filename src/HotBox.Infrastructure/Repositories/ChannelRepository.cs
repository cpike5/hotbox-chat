using HotBox.Core.Entities;
using HotBox.Core.Enums;
using HotBox.Core.Interfaces;
using HotBox.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HotBox.Infrastructure.Repositories;

public class ChannelRepository : IChannelRepository
{
    private readonly HotBoxDbContext _dbContext;

    public ChannelRepository(HotBoxDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Channel?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _dbContext.Channels
            .Include(c => c.CreatedBy)
            .FirstOrDefaultAsync(c => c.Id == id, ct);
    }

    public async Task<IReadOnlyList<Channel>> GetAllAsync(CancellationToken ct = default)
    {
        return await _dbContext.Channels
            .Include(c => c.CreatedBy)
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Channel>> GetByTypeAsync(ChannelType type, CancellationToken ct = default)
    {
        return await _dbContext.Channels
            .Where(c => c.Type == type)
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task<Channel> CreateAsync(Channel channel, CancellationToken ct = default)
    {
        _dbContext.Channels.Add(channel);
        await _dbContext.SaveChangesAsync(ct);
        return channel;
    }

    public async Task UpdateAsync(Channel channel, CancellationToken ct = default)
    {
        _dbContext.Channels.Update(channel);
        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var channel = await _dbContext.Channels.FindAsync([id], ct);
        if (channel is not null)
        {
            _dbContext.Channels.Remove(channel);
            await _dbContext.SaveChangesAsync(ct);
        }
    }

    public async Task<bool> ExistsByNameAsync(string name, Guid? excludeId = null, CancellationToken ct = default)
    {
        var query = _dbContext.Channels.Where(c => c.Name == name);
        if (excludeId.HasValue)
        {
            query = query.Where(c => c.Id != excludeId.Value);
        }

        return await query.AnyAsync(ct);
    }

    public async Task<int> GetMaxSortOrderAsync(CancellationToken ct = default)
    {
        if (!await _dbContext.Channels.AnyAsync(ct))
        {
            return -1;
        }

        return await _dbContext.Channels.MaxAsync(c => c.SortOrder, ct);
    }

    public async Task ReorderAsync(List<Guid> channelIds, CancellationToken ct = default)
    {
        for (var i = 0; i < channelIds.Count; i++)
        {
            var channelId = channelIds[i];
            var channel = await _dbContext.Channels.FindAsync([channelId], ct);
            if (channel is not null)
            {
                channel.SortOrder = i;
            }
        }

        await _dbContext.SaveChangesAsync(ct);
    }
}
