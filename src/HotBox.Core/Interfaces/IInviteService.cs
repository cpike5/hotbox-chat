using HotBox.Core.Entities;

namespace HotBox.Core.Interfaces;

public interface IInviteService
{
    Task<Invite> GenerateAsync(Guid createdByUserId, DateTime? expiresAt = null, int? maxUses = null, CancellationToken ct = default);

    Task<Invite?> ValidateAndConsumeAsync(string code, CancellationToken ct = default);

    Task<bool> RevokeAsync(string code, CancellationToken ct = default);

    Task<IReadOnlyList<Invite>> GetAllAsync(CancellationToken ct = default);
}
