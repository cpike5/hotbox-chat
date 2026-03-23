using HotBox.Core.Entities;
using HotBox.Core.Interfaces;
using HotBox.Core.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace HotBox.Infrastructure.Services;

public class DirectMessageService : IDirectMessageService
{
    private readonly IDirectMessageRepository _directMessageRepository;
    private readonly UserManager<AppUser> _userManager;
    private readonly ILogger<DirectMessageService> _logger;

    public DirectMessageService(
        IDirectMessageRepository directMessageRepository,
        UserManager<AppUser> userManager,
        ILogger<DirectMessageService> logger)
    {
        _directMessageRepository = directMessageRepository;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<DirectMessage> SendAsync(
        Guid senderId,
        Guid recipientId,
        string content,
        CancellationToken ct = default)
    {
        var recipient = await _userManager.FindByIdAsync(recipientId.ToString())
            ?? throw new InvalidOperationException($"Recipient {recipientId} not found.");

        var message = new DirectMessage
        {
            Id = Guid.NewGuid(),
            Content = content,
            SenderId = senderId,
            RecipientId = recipientId,
            CreatedAt = DateTime.UtcNow
        };

        var created = await _directMessageRepository.CreateAsync(message, ct);

        _logger.LogDebug("Direct message {MessageId} sent from {SenderId} to {RecipientId}",
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
        return await _directMessageRepository.GetConversationAsync(userId, otherUserId, before, limit, ct);
    }

    public async Task<IReadOnlyList<ConversationSummary>> GetConversationsAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        return await _directMessageRepository.GetConversationsAsync(userId, ct);
    }
}
