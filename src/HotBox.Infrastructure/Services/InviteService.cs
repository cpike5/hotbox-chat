using System.Security.Cryptography;
using HotBox.Core.Entities;
using HotBox.Core.Interfaces;
using HotBox.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HotBox.Infrastructure.Services;

public class InviteService : IInviteService
{
    private const int CodeByteLength = 6; // 6 bytes → 8 Base64URL chars
    private readonly HotBoxDbContext _context;
    private readonly ILogger<InviteService> _logger;

    public InviteService(
        HotBoxDbContext context,
        ILogger<InviteService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Invite> GenerateAsync(
        Guid createdByUserId,
        DateTime? expiresAtUtc = null,
        int? maxUses = null,
        CancellationToken ct = default)
    {
        var code = GenerateCode();

        var invite = new Invite
        {
            Id = Guid.NewGuid(),
            Code = code,
            CreatedByUserId = createdByUserId,
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = expiresAtUtc,
            MaxUses = maxUses,
            UseCount = 0,
            IsRevoked = false,
        };

        _context.Invites.Add(invite);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Generated invite code {InviteCode} by user {UserId} (expires: {ExpiresAtUtc}, maxUses: {MaxUses})",
            code,
            createdByUserId,
            expiresAtUtc?.ToString("o") ?? "never",
            maxUses?.ToString() ?? "unlimited");

        return invite;
    }

    public async Task<Invite?> ValidateAndConsumeAsync(string code, CancellationToken ct = default)
    {
        var invite = await _context.Invites
            .FirstOrDefaultAsync(i => i.Code == code, ct);

        if (invite is null)
        {
            _logger.LogWarning("Invite code {InviteCode} not found", code);
            return null;
        }

        if (invite.IsRevoked)
        {
            _logger.LogWarning("Invite code {InviteCode} has been revoked", code);
            return null;
        }

        if (invite.ExpiresAtUtc.HasValue && invite.ExpiresAtUtc.Value <= DateTime.UtcNow)
        {
            _logger.LogWarning("Invite code {InviteCode} has expired", code);
            return null;
        }

        if (invite.MaxUses.HasValue && invite.UseCount >= invite.MaxUses.Value)
        {
            _logger.LogWarning("Invite code {InviteCode} has reached max uses ({MaxUses})", code, invite.MaxUses.Value);
            return null;
        }

        invite.UseCount++;
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Invite code {InviteCode} consumed (use {UseCount}/{MaxUses})",
            code,
            invite.UseCount,
            invite.MaxUses?.ToString() ?? "unlimited");

        return invite;
    }

    public async Task<bool> RevokeAsync(string code, CancellationToken ct = default)
    {
        var invite = await _context.Invites
            .FirstOrDefaultAsync(i => i.Code == code, ct);

        if (invite is null)
        {
            _logger.LogWarning("Attempted to revoke non-existent invite code {InviteCode}", code);
            return false;
        }

        if (invite.IsRevoked)
        {
            _logger.LogDebug("Invite code {InviteCode} is already revoked", code);
            return true;
        }

        invite.IsRevoked = true;
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Revoked invite code {InviteCode}", code);
        return true;
    }

    public async Task<IReadOnlyList<Invite>> GetAllAsync(CancellationToken ct = default)
    {
        return await _context.Invites
            .AsNoTracking()
            .OrderByDescending(i => i.CreatedAtUtc)
            .ToListAsync(ct);
    }

    private static string GenerateCode()
    {
        var bytes = RandomNumberGenerator.GetBytes(CodeByteLength);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}
