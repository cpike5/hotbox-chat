using HotBox.Core.Entities;
using HotBox.Core.Enums;
using HotBox.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace HotBox.Infrastructure.Services;

public class ChannelService : IChannelService
{
    private readonly IChannelRepository _channelRepository;
    private readonly ILogger<ChannelService> _logger;

    public ChannelService(IChannelRepository channelRepository, ILogger<ChannelService> logger)
    {
        _channelRepository = channelRepository;
        _logger = logger;
    }

    public async Task<Channel> CreateAsync(
        Guid userId,
        string name,
        string? description,
        ChannelType type,
        CancellationToken ct = default)
    {
        if (await _channelRepository.ExistsByNameAsync(name, ct: ct))
        {
            throw new InvalidOperationException($"A channel with the name '{name}' already exists.");
        }

        var maxSortOrder = await _channelRepository.GetMaxSortOrderAsync(ct);

        var channel = new Channel
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            Type = type,
            SortOrder = maxSortOrder + 1,
            CreatedAt = DateTime.UtcNow,
            CreatedById = userId
        };

        var created = await _channelRepository.CreateAsync(channel, ct);

        _logger.LogInformation("Channel {ChannelName} created by user {UserId}", name, userId);

        return created;
    }

    public async Task UpdateAsync(
        Guid userId,
        Guid channelId,
        string name,
        string? description,
        CancellationToken ct = default)
    {
        var channel = await _channelRepository.GetByIdAsync(channelId, ct)
            ?? throw new InvalidOperationException($"Channel {channelId} not found.");

        if (await _channelRepository.ExistsByNameAsync(name, excludeId: channelId, ct: ct))
        {
            throw new InvalidOperationException($"A channel with the name '{name}' already exists.");
        }

        channel.Name = name;
        channel.Description = description;

        await _channelRepository.UpdateAsync(channel, ct);

        _logger.LogInformation("Channel {ChannelId} updated by user {UserId}", channelId, userId);
    }

    public async Task DeleteAsync(Guid userId, Guid channelId, CancellationToken ct = default)
    {
        var channel = await _channelRepository.GetByIdAsync(channelId, ct)
            ?? throw new InvalidOperationException($"Channel {channelId} not found.");

        await _channelRepository.DeleteAsync(channelId, ct);

        _logger.LogInformation("Channel {ChannelId} ({ChannelName}) deleted by user {UserId}",
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
}
