using HotBox.Core.Entities;

namespace HotBox.Core.Interfaces;

public interface ITokenService
{
    Task<string> GenerateAccessTokenAsync(AppUser user, CancellationToken ct = default);

    Task<RefreshToken> GenerateRefreshTokenAsync(Guid userId, CancellationToken ct = default);

    Task<RefreshToken?> ValidateRefreshTokenAsync(string token, CancellationToken ct = default);

    Task<RefreshToken> RotateRefreshTokenAsync(RefreshToken existingToken, CancellationToken ct = default);

    Task RevokeRefreshTokenAsync(string token, CancellationToken ct = default);

    Task RevokeAllUserRefreshTokensAsync(Guid userId, CancellationToken ct = default);
}
