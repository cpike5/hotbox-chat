using System.Text.RegularExpressions;
using HotBox.Application.Hubs;
using HotBox.Application.Models;
using HotBox.Core.Entities;
using HotBox.Core.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HotBox.Application.Services;

public partial class NotificationService : INotificationService
{
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly UserManager<AppUser> _userManager;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        IHubContext<ChatHub> hubContext,
        UserManager<AppUser> userManager,
        ILogger<NotificationService> logger)
    {
        _hubContext = hubContext;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task ProcessMessageNotificationsAsync(
        Guid senderId,
        string senderDisplayName,
        Guid channelId,
        string channelName,
        string messageContent)
    {
        var mentionedUsernames = ExtractMentions(messageContent);
        if (mentionedUsernames.Count == 0)
            return;

        var messagePreview = messageContent.Length > 100
            ? messageContent[..100]
            : messageContent;

        var payload = new NotificationPayload
        {
            SenderDisplayName = senderDisplayName,
            ChannelName = channelName,
            ChannelId = channelId,
            MessagePreview = messagePreview,
        };

        // Batch lookup: query all mentioned usernames in a single database round-trip
        var mentionedUsers = await _userManager.Users
            .Where(u => u.UserName != null && mentionedUsernames.Contains(u.UserName))
            .ToListAsync();

        foreach (var user in mentionedUsers)
        {
            // Don't notify the sender about their own mentions
            if (user.Id == senderId)
                continue;

            await _hubContext.Clients
                .User(user.Id.ToString())
                .SendAsync("ReceiveNotification", payload);

            _logger.LogInformation(
                "Notification sent to user {UserId} for mention in channel {ChannelId} by {SenderId}",
                user.Id, channelId, senderId);
        }
    }

    /// <summary>
    /// Extracts @username mentions from message content.
    /// Matches @followed by word characters (letters, digits, underscores).
    /// </summary>
    private static List<string> ExtractMentions(string content)
    {
        var matches = MentionRegex().Matches(content);
        var usernames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in matches)
        {
            usernames.Add(match.Groups[1].Value);
        }

        return usernames.ToList();
    }

    [GeneratedRegex(@"@(\w+)", RegexOptions.Compiled)]
    private static partial Regex MentionRegex();
}
