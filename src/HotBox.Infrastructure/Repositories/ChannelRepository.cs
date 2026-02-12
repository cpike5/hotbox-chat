using HotBox.Core.Entities;
using HotBox.Core.Enums;
using HotBox.Core.Interfaces;
using HotBox.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HotBox.Infrastructure.Repositories;

public class ChannelRepository : IChannelRepository
{
    private readonly HotBoxDbContext _context;

    public ChannelRepository(HotBoxDbContext context)
    {
        _context = context;
    }

    public async Task<Channel?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Channels
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, ct);
    }

    public async Task<IReadOnlyList<Channel>> GetAllAsync(CancellationToken ct = default)
    {
        return await _context.Channels
            .AsNoTracking()
            .OrderBy(c => c.SortOrder)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Channel>> GetByTypeAsync(ChannelType type, CancellationToken ct = default)
    {
        return await _context.Channels
            .AsNoTracking()
            .Where(c => c.Type == type)
            .OrderBy(c => c.SortOrder)
            .ToListAsync(ct);
    }

    public async Task<Channel> CreateAsync(Channel channel, CancellationToken ct = default)
    {
        _context.Channels.Add(channel);
        await _context.SaveChangesAsync(ct);
        return channel;
    }

    public async Task UpdateAsync(Channel channel, CancellationToken ct = default)
    {
        _context.Channels.Update(channel);
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var channel = await _context.Channels.FindAsync([id], ct);
        if (channel != null)
        {
            _context.Channels.Remove(channel);
            await _context.SaveChangesAsync(ct);
        }
    }

    public async Task<bool> ExistsByNameAsync(string name, Guid? excludeId = null, CancellationToken ct = default)
    {
        var query = _context.Channels.AsQueryable();

        if (excludeId.HasValue)
        {
            query = query.Where(c => c.Id != excludeId.Value);
        }

        return await query.AnyAsync(c => c.Name.ToLower() == name.ToLower(), ct);
    }

    public async Task<int> GetMaxSortOrderAsync(CancellationToken ct = default)
    {
        if (!await _context.Channels.AnyAsync(ct))
        {
            return -1;
        }

        return await _context.Channels.MaxAsync(c => c.SortOrder, ct);
    }
}
