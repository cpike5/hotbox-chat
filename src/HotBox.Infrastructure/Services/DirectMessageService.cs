using HotBox.Core.Entities;
using HotBox.Core.Interfaces;
using HotBox.Core.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace HotBox.Infrastructure.Services;

public class DirectMessageService : IDirectMessageService
{
    private readonly IDirectMessageRepository _repository;
    private readonly UserManager<AppUser> _userManager;
    private readonly ILogger<DirectMessageService> _logger;

    public DirectMessageService(
        IDirectMessageRepository repository,
        UserManager<AppUser> userManager,
        ILogger<DirectMessageService> logger)
    {
        _repository = repository;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<DirectMessage> SendAsync(
        Guid senderId,
        Guid recipientId,
        string content,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Message content cannot be empty.", nameof(content));

        if (senderId == recipientId)
            throw new ArgumentException("Cannot send a direct message to yourself.");

        var sender = await _userManager.FindByIdAsync(senderId.ToString())
            ?? throw new KeyNotFoundException($"Sender {senderId} not found.");

        var recipient = await _userManager.FindByIdAsync(recipientId.ToString())
            ?? throw new KeyNotFoundException($"Recipient {recipientId} not found.");

        var message = new DirectMessage
        {
            Id = Guid.NewGuid(),
            Content = content,
            SenderId = senderId,
            RecipientId = recipientId,
            CreatedAtUtc = DateTime.UtcNow,
        };

        var created = await _repository.CreateAsync(message, ct);

        _logger.LogInformation(
            "Direct message {MessageId} sent from user {SenderId} to user {RecipientId}",
            created.Id, senderId, recipientId);

        return created;
    }

    public async Task<IReadOnlyList<DirectMessage>> GetConversationAsync(
        Guid userId,
        Guid otherUserId,
        DateTime? before = null,
        int limit = 50,
        CancellationToken ct = default)
    {
        return await _repository.GetConversationAsync(userId, otherUserId, before, limit, ct);
    }

    public async Task<IReadOnlyList<ConversationSummary>> GetConversationsAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        return await _repository.GetConversationsAsync(userId, ct);
    }
}
