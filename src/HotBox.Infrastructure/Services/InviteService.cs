using System.Security.Cryptography;
using HotBox.Core.Entities;
using HotBox.Core.Interfaces;
using HotBox.Infrastructure.Data;
using Microsoft.Extensions.Logging;

namespace HotBox.Infrastructure.Services;

public class InviteService : IInviteService
{
    private readonly IInviteRepository _inviteRepository;
    private readonly HotBoxDbContext _dbContext;
    private readonly ILogger<InviteService> _logger;

    public InviteService(
        IInviteRepository inviteRepository,
        HotBoxDbContext dbContext,
        ILogger<InviteService> logger)
    {
        _inviteRepository = inviteRepository;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<Invite> GenerateAsync(
        Guid createdByUserId,
        DateTime? expiresAt = null,
        int? maxUses = null,
        CancellationToken ct = default)
    {
        var invite = new Invite
        {
            Id = Guid.NewGuid(),
            Code = GenerateCode(),
            CreatedById = createdByUserId,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt,
            MaxUses = maxUses,
            UseCount = 0,
            IsRevoked = false
        };

        var created = await _inviteRepository.CreateAsync(invite, ct);

        _logger.LogInformation("Invite {InviteCode} created by user {UserId}", invite.Code, createdByUserId);

        return created;
    }

    public async Task<Invite?> ValidateAndConsumeAsync(string code, CancellationToken ct = default)
    {
        var invite = await _inviteRepository.GetByCodeAsync(code, ct);

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

        if (invite.ExpiresAt.HasValue && invite.ExpiresAt.Value < DateTime.UtcNow)
        {
            _logger.LogWarning("Invite code {InviteCode} has expired", code);
            return null;
        }

        if (invite.MaxUses.HasValue && invite.UseCount >= invite.MaxUses.Value)
        {
            _logger.LogWarning("Invite code {InviteCode} has reached maximum uses", code);
            return null;
        }

        invite.UseCount++;
        await _dbContext.SaveChangesAsync(ct);

        return invite;
    }

    public async Task<bool> RevokeAsync(string code, CancellationToken ct = default)
    {
        var invite = await _inviteRepository.GetByCodeAsync(code, ct);
        if (invite is null)
        {
            return false;
        }

        await _inviteRepository.RevokeAsync(invite.Id, ct);

        _logger.LogInformation("Invite {InviteCode} revoked", code);

        return true;
    }

    public async Task<IReadOnlyList<Invite>> GetAllAsync(CancellationToken ct = default)
    {
        return await _inviteRepository.GetAllAsync(ct);
    }

    private static string GenerateCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789";
        return string.Create(8, chars, static (span, chars) =>
        {
            Span<byte> randomBytes = stackalloc byte[8];
            RandomNumberGenerator.Fill(randomBytes);
            for (var i = 0; i < span.Length; i++)
            {
                span[i] = chars[randomBytes[i] % chars.Length];
            }
        });
    }
}
