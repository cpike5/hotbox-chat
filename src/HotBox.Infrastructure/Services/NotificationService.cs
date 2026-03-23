using System.Text.Json;
using HotBox.Core.Entities;
using HotBox.Core.Enums;
using HotBox.Core.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace HotBox.Infrastructure.Services;

public class NotificationService : INotificationService
{
    private readonly INotificationRepository _notificationRepository;
    private readonly UserManager<AppUser> _userManager;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        INotificationRepository notificationRepository,
        UserManager<AppUser> userManager,
        ILogger<NotificationService> logger)
    {
        _notificationRepository = notificationRepository;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task CreateAsync(
        NotificationType type,
        Guid senderId,
        Guid recipientId,
        string senderDisplayName,
        string messagePreview,
        Guid sourceId,
        NotificationSourceType sourceType,
        string sourceName,
        CancellationToken ct = default)
    {
        // Don't notify yourself
        if (senderId == recipientId)
        {
            return;
        }

        // Check if source is muted
        if (await _notificationRepository.IsSourceMutedAsync(recipientId, sourceType, sourceId, ct))
        {
            return;
        }

        var payload = JsonSerializer.Serialize(new
        {
            senderDisplayName,
            messagePreview = messagePreview.Length > 100
                ? string.Concat(messagePreview.AsSpan(0, 100), "...")
                : messagePreview,
            sourceName
        });

        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            Type = type,
            RecipientId = recipientId,
            SenderId = senderId,
            PayloadJson = payload,
            SourceId = sourceId,
            SourceType = sourceType,
            CreatedAt = DateTime.UtcNow
        };

        await _notificationRepository.CreateAsync(notification, ct);

        _logger.LogDebug("Notification created: {Type} from {SenderId} to {RecipientId}",
            type, senderId, recipientId);
    }

    public async Task ProcessMentionNotificationsAsync(
        Guid senderId,
        string senderDisplayName,
        Guid channelId,
        string channelName,
        string messageContent,
        CancellationToken ct = default)
    {
        // Extract @mentions from message content
        var mentions = ExtractMentions(messageContent);

        foreach (var mentionedName in mentions)
        {
            // Look up user by display name (case-insensitive)
            var users = _userManager.Users;
            var mentionedUser = users.FirstOrDefault(u =>
                u.DisplayName.ToLower() == mentionedName.ToLower());

            if (mentionedUser is null)
            {
                continue;
            }

            await CreateAsync(
                NotificationType.Mention,
                senderId,
                mentionedUser.Id,
                senderDisplayName,
                messageContent,
                channelId,
                NotificationSourceType.Channel,
                channelName,
                ct);
        }
    }

    private static List<string> ExtractMentions(string content)
    {
        var mentions = new List<string>();
        var span = content.AsSpan();
        var index = 0;

        while (index < span.Length)
        {
            var atIndex = span[index..].IndexOf('@');
            if (atIndex < 0) break;

            atIndex += index;
            var start = atIndex + 1;
            var end = start;

            // Read until whitespace or end
            while (end < span.Length && !char.IsWhiteSpace(span[end]))
            {
                end++;
            }

            if (end > start)
            {
                mentions.Add(span[start..end].ToString());
            }

            index = end;
        }

        return mentions;
    }
}
