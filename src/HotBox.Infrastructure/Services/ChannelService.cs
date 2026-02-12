using HotBox.Core.Entities;
using HotBox.Core.Enums;
using HotBox.Core.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace HotBox.Infrastructure.Services;

public class ChannelService : IChannelService
{
    private readonly IChannelRepository _channelRepository;
    private readonly UserManager<AppUser> _userManager;
    private readonly ILogger<ChannelService> _logger;

    public ChannelService(
        IChannelRepository channelRepository,
        UserManager<AppUser> userManager,
        ILogger<ChannelService> logger)
    {
        _channelRepository = channelRepository;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<Channel> CreateAsync(
        Guid userId,
        string name,
        string? topic,
        ChannelType type,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Channel name cannot be empty.", nameof(name));

        await EnsureAdminOrModeratorAsync(userId);

        // Validate channel name uniqueness
        if (await _channelRepository.ExistsByNameAsync(name, ct: ct))
        {
            throw new InvalidOperationException($"A channel with the name '{name}' already exists.");
        }

        var maxSortOrder = await _channelRepository.GetMaxSortOrderAsync(ct);

        var channel = new Channel
        {
            Id = Guid.NewGuid(),
            Name = name,
            Topic = topic,
            Type = type,
            SortOrder = maxSortOrder + 1,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = userId,
        };

        var created = await _channelRepository.CreateAsync(channel, ct);

        _logger.LogInformation(
            "Channel {ChannelId} ({ChannelName}) created by user {UserId}",
            created.Id, created.Name, userId);

        return created;
    }

    public async Task UpdateAsync(
        Guid userId,
        Guid channelId,
        string name,
        string? topic,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Channel name cannot be empty.", nameof(name));

        await EnsureAdminOrModeratorAsync(userId);

        var channel = await _channelRepository.GetByIdAsync(channelId, ct)
            ?? throw new KeyNotFoundException($"Channel {channelId} not found.");

        // Validate channel name uniqueness (excluding the current channel)
        if (await _channelRepository.ExistsByNameAsync(name, excludeId: channelId, ct: ct))
        {
            throw new InvalidOperationException($"A channel with the name '{name}' already exists.");
        }

        channel.Name = name;
        channel.Topic = topic;

        await _channelRepository.UpdateAsync(channel, ct);

        _logger.LogInformation(
            "Channel {ChannelId} updated by user {UserId}",
            channelId, userId);
    }

    public async Task DeleteAsync(Guid userId, Guid channelId, CancellationToken ct = default)
    {
        await EnsureAdminAsync(userId);

        var channel = await _channelRepository.GetByIdAsync(channelId, ct)
            ?? throw new KeyNotFoundException($"Channel {channelId} not found.");

        await _channelRepository.DeleteAsync(channelId, ct);

        _logger.LogInformation(
            "Channel {ChannelId} ({ChannelName}) deleted by user {UserId}",
            channelId, channel.Name, userId);
    }

    public async Task<IReadOnlyList<Channel>> GetAllAsync(CancellationToken ct = default)
    {
        return await _channelRepository.GetAllAsync(ct);
    }

    public async Task<Channel?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _channelRepository.GetByIdAsync(id, ct);
    }

    public async Task<IReadOnlyList<Channel>> GetByTypeAsync(ChannelType type, CancellationToken ct = default)
    {
        return await _channelRepository.GetByTypeAsync(type, ct);
    }

    private async Task EnsureAdminOrModeratorAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString())
            ?? throw new KeyNotFoundException($"User {userId} not found.");

        var roles = await _userManager.GetRolesAsync(user);
        if (!roles.Contains("Admin") && !roles.Contains("Moderator"))
        {
            _logger.LogWarning(
                "User {UserId} attempted channel operation without required role",
                userId);
            throw new UnauthorizedAccessException("Only Admin or Moderator roles can perform this operation.");
        }
    }

    private async Task EnsureAdminAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString())
            ?? throw new KeyNotFoundException($"User {userId} not found.");

        var roles = await _userManager.GetRolesAsync(user);
        if (!roles.Contains("Admin"))
        {
            _logger.LogWarning(
                "User {UserId} attempted admin-only channel operation without Admin role",
                userId);
            throw new UnauthorizedAccessException("Only Admin role can perform this operation.");
        }
    }
}
