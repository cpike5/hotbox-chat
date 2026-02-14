using System.Text.Json;
using System.Text.RegularExpressions;
using HotBox.Application.Hubs;
using HotBox.Application.Models;
using HotBox.Core.Entities;
using HotBox.Core.Enums;
using HotBox.Core.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HotBox.Application.Services;

public partial class NotificationService : INotificationService
{
    private readonly INotificationRepository _notificationRepository;
    private readonly IPresenceService _presenceService;
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly UserManager<AppUser> _userManager;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        INotificationRepository notificationRepository,
        IPresenceService presenceService,
        IHubContext<ChatHub> hubContext,
        UserManager<AppUser> userManager,
        ILogger<NotificationService> logger)
    {
        _notificationRepository = notificationRepository;
        _presenceService = presenceService;
        _hubContext = hubContext;
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
        if (senderId == recipientId)
            return;

        var payloadJson = JsonSerializer.Serialize(new
        {
            senderDisplayName,
            messagePreview,
            sourceName
        });

        var notification = new Notification
        {
            Type = type,
            RecipientId = recipientId,
            SenderId = senderId,
            PayloadJson = payloadJson,
            SourceId = sourceId,
            SourceType = sourceType,
            CreatedAtUtc = DateTime.UtcNow
        };

        await _notificationRepository.CreateAsync(notification, ct);

        _logger.LogInformation(
            "Notification {NotificationId} created: {Type} for user {RecipientId} from {SenderId}",
            notification.Id, type, recipientId, senderId);

        // Check DND
        var recipientStatus = _presenceService.GetStatus(recipientId);
        if (recipientStatus == UserStatus.DoNotDisturb)
        {
            _logger.LogDebug("Skipping delivery for {RecipientId} — DoNotDisturb", recipientId);
            return;
        }

        // Check mute
        if (await _notificationRepository.IsSourceMutedAsync(recipientId, sourceType, sourceId, ct))
        {
            _logger.LogDebug("Skipping delivery for {RecipientId} — source muted", recipientId);
            return;
        }

        // Deliver via SignalR
        var response = new NotificationResponse
        {
            Id = notification.Id,
            Type = type,
            SenderId = senderId,
            SenderDisplayName = senderDisplayName,
            MessagePreview = messagePreview,
            SourceId = sourceId,
            SourceType = sourceType,
            SourceName = sourceName,
            CreatedAtUtc = notification.CreatedAtUtc,
            ReadAtUtc = null
        };

        await _hubContext.Clients
            .User(recipientId.ToString())
            .SendAsync("ReceiveNotification", response, ct);
    }

    public async Task ProcessMentionNotificationsAsync(
        Guid senderId,
        string senderDisplayName,
        Guid channelId,
        string channelName,
        string messageContent,
        CancellationToken ct = default)
    {
        var mentionedUsernames = ExtractMentions(messageContent);
        if (mentionedUsernames.Count == 0)
            return;

        var preview = messageContent.Length > 100
            ? messageContent[..100]
            : messageContent;

        var mentionedUsers = await _userManager.Users
            .Where(u => u.UserName != null && mentionedUsernames.Contains(u.UserName))
            .ToListAsync(ct);

        foreach (var user in mentionedUsers)
        {
            await CreateAsync(
                NotificationType.Mention,
                senderId,
                user.Id,
                senderDisplayName,
                preview,
                channelId,
                NotificationSourceType.Channel,
                channelName,
                ct);
        }
    }

    private static List<string> ExtractMentions(string content)
    {
        var matches = MentionRegex().Matches(content);
        var usernames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in matches)
            usernames.Add(match.Groups[1].Value);
        return usernames.ToList();
    }

    [GeneratedRegex(@"@(\w+)", RegexOptions.Compiled)]
    private static partial Regex MentionRegex();
}
