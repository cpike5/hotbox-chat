using HotBox.Core.Entities;
using HotBox.Core.Interfaces;
using HotBox.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HotBox.Infrastructure.Repositories;

public class InviteRepository : IInviteRepository
{
    private readonly HotBoxDbContext _dbContext;

    public InviteRepository(HotBoxDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Invite?> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        return await _dbContext.Invites
            .Include(i => i.CreatedBy)
            .FirstOrDefaultAsync(i => i.Code == code, ct);
    }

    public async Task<IReadOnlyList<Invite>> GetAllAsync(CancellationToken ct = default)
    {
        return await _dbContext.Invites
            .Include(i => i.CreatedBy)
            .OrderByDescending(i => i.CreatedAt)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task<Invite> CreateAsync(Invite invite, CancellationToken ct = default)
    {
        _dbContext.Invites.Add(invite);
        await _dbContext.SaveChangesAsync(ct);
        return invite;
    }

    public async Task RevokeAsync(Guid id, CancellationToken ct = default)
    {
        var invite = await _dbContext.Invites.FindAsync([id], ct);
        if (invite is not null)
        {
            invite.IsRevoked = true;
            await _dbContext.SaveChangesAsync(ct);
        }
    }
}
