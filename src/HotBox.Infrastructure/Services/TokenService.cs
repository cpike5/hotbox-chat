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
    private readonly HotBoxDbContext _dbContext;
    private readonly UserManager<AppUser> _userManager;
    private readonly JwtOptions _jwtOptions;
    private readonly ILogger<TokenService> _logger;

    public TokenService(
        HotBoxDbContext dbContext,
        UserManager<AppUser> userManager,
        IOptions<JwtOptions> jwtOptions,
        ILogger<TokenService> logger)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _jwtOptions = jwtOptions.Value;
        _logger = logger;
    }

    public async Task<string> GenerateAccessTokenAsync(AppUser user, CancellationToken ct = default)
    {
        var roles = await _userManager.GetRolesAsync(user);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new("display_name", user.DisplayName),
            new("is_agent", user.IsAgent.ToString().ToLowerInvariant())
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

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public async Task<RefreshToken> GenerateRefreshTokenAsync(Guid userId, CancellationToken ct = default)
    {
        var tokenBytes = RandomNumberGenerator.GetBytes(64);
        var tokenString = Convert.ToBase64String(tokenBytes);

        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            TokenHash = HashToken(tokenString),
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.Add(_jwtOptions.RefreshTokenExpiration)
        };

        _dbContext.RefreshTokens.Add(refreshToken);
        await _dbContext.SaveChangesAsync(ct);

        // Store the raw token temporarily for the caller to return to the client
        // We use ReplacedByToken field as a transient carrier (not persisted as the hash)
        refreshToken.ReplacedByToken = tokenString;

        _logger.LogDebug("Refresh token generated for user {UserId}", userId);

        return refreshToken;
    }

    public async Task<RefreshToken?> ValidateRefreshTokenAsync(string token, CancellationToken ct = default)
    {
        var tokenHash = HashToken(token);

        var refreshToken = await _dbContext.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash, ct);

        if (refreshToken is null)
        {
            _logger.LogWarning("Refresh token not found");
            return null;
        }

        if (!refreshToken.IsActive)
        {
            _logger.LogWarning("Refresh token is not active for user {UserId}", refreshToken.UserId);
            return null;
        }

        return refreshToken;
    }

    public async Task<RefreshToken> RotateRefreshTokenAsync(RefreshToken existingToken, CancellationToken ct = default)
    {
        // Revoke the existing token
        existingToken.RevokedAt = DateTime.UtcNow;

        // Generate a new token
        var newToken = await GenerateRefreshTokenAsync(existingToken.UserId, ct);

        // Link old token to new one
        existingToken.ReplacedByToken = newToken.TokenHash;

        await _dbContext.SaveChangesAsync(ct);

        _logger.LogDebug("Refresh token rotated for user {UserId}", existingToken.UserId);

        return newToken;
    }

    public async Task RevokeRefreshTokenAsync(string token, CancellationToken ct = default)
    {
        var tokenHash = HashToken(token);

        var refreshToken = await _dbContext.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash, ct);

        if (refreshToken is not null)
        {
            refreshToken.RevokedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(ct);

            _logger.LogDebug("Refresh token revoked for user {UserId}", refreshToken.UserId);
        }
    }

    public async Task RevokeAllUserRefreshTokensAsync(Guid userId, CancellationToken ct = default)
    {
        await _dbContext.RefreshTokens
            .Where(rt => rt.UserId == userId && rt.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(rt => rt.RevokedAt, DateTime.UtcNow), ct);

        _logger.LogInformation("All refresh tokens revoked for user {UserId}", userId);
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(bytes);
    }
}
