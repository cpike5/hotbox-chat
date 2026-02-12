using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using HotBox.Core.Entities;
using HotBox.Core.Interfaces;
using HotBox.Core.Options;
using HotBox.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace HotBox.Infrastructure.Services;

public class TokenService : ITokenService
{
    private readonly HotBoxDbContext _context;
    private readonly UserManager<AppUser> _userManager;
    private readonly JwtOptions _jwtOptions;
    private readonly ILogger<TokenService> _logger;

    public TokenService(
        HotBoxDbContext context,
        UserManager<AppUser> userManager,
        IOptions<JwtOptions> jwtOptions,
        ILogger<TokenService> logger)
    {
        _context = context;
        _userManager = userManager;
        _jwtOptions = jwtOptions.Value;
        _logger = logger;
    }

    public async Task<string> GenerateAccessTokenAsync(AppUser user, CancellationToken ct = default)
    {
        var roles = await _userManager.GetRolesAsync(user);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("display_name", user.DisplayName),
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            expires: DateTime.UtcNow.Add(_jwtOptions.AccessTokenExpiration),
            signingCredentials: credentials);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        _logger.LogDebug("Generated access token for user {UserId}", user.Id);

        return tokenString;
    }

    public async Task<RefreshToken> GenerateRefreshTokenAsync(Guid userId, CancellationToken ct = default)
    {
        var tokenValue = GenerateSecureToken();

        var refreshToken = new RefreshToken
        {
            Token = tokenValue,
            UserId = userId,
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.Add(_jwtOptions.RefreshTokenExpiration),
        };

        _context.RefreshTokens.Add(refreshToken);
        await _context.SaveChangesAsync(ct);

        _logger.LogDebug("Generated refresh token for user {UserId}", userId);

        return refreshToken;
    }

    public async Task<RefreshToken?> ValidateRefreshTokenAsync(string token, CancellationToken ct = default)
    {
        var refreshToken = await _context.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.Token == token, ct);

        if (refreshToken is null)
        {
            _logger.LogWarning("Refresh token not found during validation");
            return null;
        }

        if (!refreshToken.IsActive)
        {
            _logger.LogWarning("Inactive refresh token used for user {UserId}", refreshToken.UserId);
            return null;
        }

        return refreshToken;
    }

    public async Task<RefreshToken> RotateRefreshTokenAsync(RefreshToken existingToken, CancellationToken ct = default)
    {
        await using var transaction = await _context.Database
            .BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, ct);

        try
        {
            // Re-fetch the token within the transaction to prevent race conditions
            var tokenToRevoke = await _context.RefreshTokens
                .FirstOrDefaultAsync(rt => rt.Id == existingToken.Id && rt.RevokedAtUtc == null, ct);

            if (tokenToRevoke is null)
            {
                throw new InvalidOperationException("Refresh token has already been revoked.");
            }

            var newTokenValue = GenerateSecureToken();

            // Revoke the existing token
            tokenToRevoke.RevokedAtUtc = DateTime.UtcNow;
            tokenToRevoke.ReplacedByToken = newTokenValue;

            // Create the replacement token
            var newToken = new RefreshToken
            {
                Token = newTokenValue,
                UserId = tokenToRevoke.UserId,
                CreatedAtUtc = DateTime.UtcNow,
                ExpiresAtUtc = DateTime.UtcNow.Add(_jwtOptions.RefreshTokenExpiration),
            };

            _context.RefreshTokens.Add(newToken);
            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            _logger.LogDebug("Rotated refresh token for user {UserId}", tokenToRevoke.UserId);

            return newToken;
        }
        catch (Exception) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Token rotation failed due to concurrent access for user {UserId}", existingToken.UserId);
            throw new InvalidOperationException("Refresh token has already been revoked.");
        }
    }

    public async Task RevokeRefreshTokenAsync(string token, CancellationToken ct = default)
    {
        var refreshToken = await _context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == token, ct);

        if (refreshToken is null)
        {
            _logger.LogWarning("Attempted to revoke a non-existent refresh token");
            return;
        }

        if (refreshToken.IsRevoked)
        {
            _logger.LogDebug("Refresh token already revoked for user {UserId}", refreshToken.UserId);
            return;
        }

        refreshToken.RevokedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Revoked refresh token for user {UserId}", refreshToken.UserId);
    }

    public async Task RevokeAllUserRefreshTokensAsync(Guid userId, CancellationToken ct = default)
    {
        var revokedCount = await _context.RefreshTokens
            .Where(rt => rt.UserId == userId && rt.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(rt => rt.RevokedAtUtc, DateTime.UtcNow), ct);

        _logger.LogInformation(
            "Revoked {TokenCount} refresh tokens for user {UserId}",
            revokedCount,
            userId);
    }

    private static string GenerateSecureToken()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }
}
