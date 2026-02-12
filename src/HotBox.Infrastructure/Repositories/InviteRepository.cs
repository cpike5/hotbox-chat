using HotBox.Core.Entities;
using HotBox.Core.Interfaces;
using HotBox.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HotBox.Infrastructure.Repositories;

public class InviteRepository : IInviteRepository
{
    private readonly HotBoxDbContext _context;

    public InviteRepository(HotBoxDbContext context)
    {
        _context = context;
    }

    public async Task<Invite?> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        return await _context.Invites
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Code == code, ct);
    }

    public async Task<IReadOnlyList<Invite>> GetAllAsync(CancellationToken ct = default)
    {
        return await _context.Invites
            .AsNoTracking()
            .OrderByDescending(i => i.CreatedAtUtc)
            .ToListAsync(ct);
    }

    public async Task<Invite> CreateAsync(Invite invite, CancellationToken ct = default)
    {
        _context.Invites.Add(invite);
        await _context.SaveChangesAsync(ct);
        return invite;
    }

    public async Task RevokeAsync(Guid id, CancellationToken ct = default)
    {
        var invite = await _context.Invites.FindAsync([id], ct);
        if (invite != null)
        {
            invite.IsRevoked = true;
            await _context.SaveChangesAsync(ct);
        }
    }
}
