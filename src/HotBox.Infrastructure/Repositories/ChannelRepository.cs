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
}
