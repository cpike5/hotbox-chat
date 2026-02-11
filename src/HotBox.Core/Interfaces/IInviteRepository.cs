using HotBox.Core.Entities;

namespace HotBox.Core.Interfaces;

public interface IInviteRepository
{
    Task<Invite?> GetByCodeAsync(string code, CancellationToken ct = default);

    Task<IReadOnlyList<Invite>> GetAllAsync(CancellationToken ct = default);

    Task<Invite> CreateAsync(Invite invite, CancellationToken ct = default);

    Task RevokeAsync(Guid id, CancellationToken ct = default);
}
