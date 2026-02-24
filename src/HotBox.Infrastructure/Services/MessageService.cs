using HotBox.Core.Entities;
using HotBox.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace HotBox.Infrastructure.Services;

public class MessageService : IMessageService
{
    private readonly IMessageRepository _messageRepository;
    private readonly IChannelRepository _channelRepository;
    private readonly ILogger<MessageService> _logger;

    public MessageService(
        IMessageRepository messageRepository,
        IChannelRepository channelRepository,
        ILogger<MessageService> logger)
    {
        _messageRepository = messageRepository;
        _channelRepository = channelRepository;
        _logger = logger;
    }

    public async Task<Message> SendAsync(
        Guid channelId,
        Guid authorId,
        string content,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Message content cannot be empty.", nameof(content));

        var channel = await _channelRepository.GetByIdAsync(channelId, ct)
            ?? throw new KeyNotFoundException($"Channel {channelId} not found.");

        var message = new Message
        {
            Id = Guid.NewGuid(),
            Content = content,
            ChannelId = channelId,
            AuthorId = authorId,
            CreatedAtUtc = DateTime.UtcNow,
        };

        var created = await _messageRepository.CreateAsync(message, ct);

        _logger.LogInformation(
            "Message {MessageId} sent to channel {ChannelId} by user {AuthorId}",
            created.Id, channelId, authorId);

        return created;
    }

    public async Task<IReadOnlyList<Message>> GetByChannelAsync(
        Guid channelId,
        DateTime? before = null,
        int limit = 50,
        CancellationToken ct = default)
    {
        var channel = await _channelRepository.GetByIdAsync(channelId, ct)
            ?? throw new KeyNotFoundException($"Channel {channelId} not found.");

        return await _messageRepository.GetByChannelAsync(channelId, before, limit, ct);
    }

    public async Task<Message?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _messageRepository.GetByIdAsync(id, ct);
    }

    public async Task<IReadOnlyList<Message>> GetAroundAsync(
        Guid channelId,
        Guid messageId,
        int context = 25,
        CancellationToken ct = default)
    {
        var channel = await _channelRepository.GetByIdAsync(channelId, ct)
            ?? throw new KeyNotFoundException($"Channel {channelId} not found.");

        return await _messageRepository.GetAroundAsync(channelId, messageId, context, ct);
    }
}
