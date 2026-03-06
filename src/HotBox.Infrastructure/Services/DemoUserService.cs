using System.Collections.Concurrent;
using HotBox.Core.Entities;
using HotBox.Core.Enums;
using HotBox.Core.Interfaces;
using HotBox.Core.Options;
using HotBox.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HotBox.Infrastructure.Services;

public class DemoUserService : IDemoUserService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly DemoModeOptions _options;
    private readonly ILogger<DemoUserService> _logger;

    private readonly ConcurrentDictionary<string, DateTime> _ipCooldowns = new();

    public DemoUserService(
        IServiceProvider serviceProvider,
        IOptions<DemoModeOptions> options,
        ILogger<DemoUserService> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<AppUser?> CreateDemoUserAsync(
        string username,
        string displayName,
        string ipAddress,
        CancellationToken ct = default)
    {
        if (await IsIpCoolingDownAsync(ipAddress, ct))
        {
            _logger.LogWarning("Demo registration rejected: IP {IpAddress} is cooling down", ipAddress);
            return null;
        }

        var activeCount = await GetActiveDemoUserCountAsync(ct);
        if (activeCount >= _options.MaxConcurrentUsers)
        {
            _logger.LogWarning("Demo registration rejected: capacity reached ({ActiveCount}/{Max})",
                activeCount, _options.MaxConcurrentUsers);
            return null;
        }

        using var scope = _serviceProvider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        var randomSuffix = Guid.NewGuid().ToString("N")[..4];
        var sanitizedName = username.Replace(" ", "_").ToLowerInvariant();
        var userName = $"demo_{sanitizedName}_{randomSuffix}";

        var now = DateTime.UtcNow;
        var user = new AppUser
        {
            UserName = userName,
            Email = $"{userName}@demo.local",
            DisplayName = displayName,
            EmailConfirmed = true,
            IsDemo = true,
            Status = UserStatus.Offline,
            CreatedAtUtc = now,
            LastSeenUtc = now,
        };

        var createResult = await userManager.CreateAsync(user);
        if (!createResult.Succeeded)
        {
            var errors = string.Join("; ", createResult.Errors.Select(e => e.Description));
            _logger.LogError("Failed to create demo user {UserName}: {Errors}", userName, errors);
            return null;
        }

        var roleResult = await userManager.AddToRoleAsync(user, "Member");
        if (!roleResult.Succeeded)
        {
            _logger.LogError("Failed to assign Member role to demo user {UserId}: {Errors}",
                user.Id, string.Join("; ", roleResult.Errors.Select(e => e.Description)));
        }

        // Track IP cooldown
        _ipCooldowns[ipAddress] = DateTime.UtcNow;

        _logger.LogInformation("Created demo user {UserId} ({DisplayName}) from IP {IpAddress}",
            user.Id, displayName, ipAddress);

        return user;
    }

    public async Task<int> GetActiveDemoUserCountAsync(CancellationToken ct = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<HotBoxDbContext>();

        return await dbContext.Users.CountAsync(u => u.IsDemo, ct);
    }

    public Task<bool> IsIpCoolingDownAsync(string ipAddress, CancellationToken ct = default)
    {
        if (_ipCooldowns.TryGetValue(ipAddress, out var lastRegistration))
        {
            if (DateTime.UtcNow - lastRegistration < _options.IpCooldown)
            {
                return Task.FromResult(true);
            }

            // Expired entry, remove it
            _ipCooldowns.TryRemove(ipAddress, out _);
        }

        return Task.FromResult(false);
    }

    public async Task RecordActivityAsync(Guid userId)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<HotBoxDbContext>();

        await dbContext.Users
            .Where(u => u.Id == userId && u.IsDemo)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.LastSeenUtc, DateTime.UtcNow));
    }

    public async Task<List<Guid>> GetExpiredDemoUserIdsAsync(CancellationToken ct = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<HotBoxDbContext>();

        var cutoff = DateTime.UtcNow - _options.SessionTimeout;

        return await dbContext.Users
            .Where(u => u.IsDemo && u.LastSeenUtc < cutoff)
            .Select(u => u.Id)
            .ToListAsync(ct);
    }

    public async Task PurgeDemoUserAsync(Guid userId, CancellationToken ct = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<HotBoxDbContext>();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);

        try
        {
            // Delete notifications where user is sender or recipient
            await dbContext.Notifications
                .Where(n => n.RecipientId == userId || n.SenderId == userId)
                .ExecuteDeleteAsync(ct);

            // Delete user notification preferences
            await dbContext.UserNotificationPreferences
                .Where(p => p.UserId == userId)
                .ExecuteDeleteAsync(ct);

            // Delete user channel reads
            await dbContext.UserChannelReads
                .Where(r => r.UserId == userId)
                .ExecuteDeleteAsync(ct);

            // Delete channel messages authored by this user
            await dbContext.Messages
                .Where(m => m.AuthorId == userId)
                .ExecuteDeleteAsync(ct);

            // Delete direct messages sent or received by this user
            await dbContext.DirectMessages
                .Where(dm => dm.SenderId == userId || dm.RecipientId == userId)
                .ExecuteDeleteAsync(ct);

            // Delete refresh tokens
            await dbContext.RefreshTokens
                .Where(rt => rt.UserId == userId)
                .ExecuteDeleteAsync(ct);

            // Delete user role assignments (Identity tables)
            await dbContext.UserRoles
                .Where(ur => ur.UserId == userId)
                .ExecuteDeleteAsync(ct);

            // Delete user claims
            await dbContext.UserClaims
                .Where(uc => uc.UserId == userId)
                .ExecuteDeleteAsync(ct);

            // Delete user logins
            await dbContext.UserLogins
                .Where(ul => ul.UserId == userId)
                .ExecuteDeleteAsync(ct);

            // Delete user tokens
            await dbContext.UserTokens
                .Where(ut => ut.UserId == userId)
                .ExecuteDeleteAsync(ct);

            // Delete the user
            await dbContext.Users
                .Where(u => u.Id == userId)
                .ExecuteDeleteAsync(ct);

            await transaction.CommitAsync(ct);

            _logger.LogInformation("Purged demo user {UserId} and all associated data", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to purge demo user {UserId}", userId);
            throw;
        }
    }

    /// <summary>
    /// Removes expired entries from the IP cooldown dictionary to prevent memory leaks.
    /// Called periodically by the cleanup service.
    /// </summary>
    public void PruneExpiredIpCooldowns()
    {
        var now = DateTime.UtcNow;
        var expiredKeys = _ipCooldowns
            .Where(kvp => now - kvp.Value >= _options.IpCooldown)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            _ipCooldowns.TryRemove(key, out _);
        }

        if (expiredKeys.Count > 0)
        {
            _logger.LogDebug("Pruned {Count} expired IP cooldown entries", expiredKeys.Count);
        }
    }
}
