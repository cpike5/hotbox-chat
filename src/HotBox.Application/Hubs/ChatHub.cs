using System.Security.Claims;
using HotBox.Application.Models;
using HotBox.Core.Entities;
using HotBox.Core.Enums;
using HotBox.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace HotBox.Application.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly IMessageService _messageService;
    private readonly IDirectMessageService _directMessageService;
    private readonly IChannelService _channelService;
    private readonly INotificationService _notificationService;
    private readonly IPresenceService _presenceService;
    private readonly UserManager<AppUser> _userManager;
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(
        IMessageService messageService,
        IDirectMessageService directMessageService,
        IChannelService channelService,
        INotificationService notificationService,
        IPresenceService presenceService,
        UserManager<AppUser> userManager,
        ILogger<ChatHub> logger)
    {
        _messageService = messageService;
        _directMessageService = directMessageService;
        _channelService = channelService;
        _notificationService = notificationService;
        _presenceService = presenceService;
        _userManager = userManager;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        var user = await _userManager.FindByIdAsync(userId.ToString());
        var displayName = user?.DisplayName ?? "Unknown";
        var isAgent = user?.IsAgent ?? false;

        // Register connection and mark user online
        await _presenceService.SetOnlineAsync(userId, Context.ConnectionId, displayName, isAgent);

        // Broadcast status change to all other clients
        await Clients.Others.SendAsync(
            "UserStatusChanged", userId, displayName, UserStatus.Online, isAgent);

        // Send the full list of online users to the connecting client
        var onlineUsers = _presenceService.GetAllOnlineUsers()
            .Select(u => new OnlineUserInfo
            {
                UserId = u.UserId,
                DisplayName = u.DisplayName,
                Status = u.Status,
                IsAgent = u.IsAgent,
            })
            .ToList();

        await Clients.Caller.SendAsync("OnlineUsers", onlineUsers);

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();

        // Remove this specific connection
        var noConnectionsLeft = _presenceService.RemoveConnection(userId, Context.ConnectionId);

        if (noConnectionsLeft)
        {
            // Grace period timer is started internally by PresenceService.
            // When it fires (30s later), if no reconnect occurred,
            // PresenceService raises OnUserStatusChanged which we subscribe to below.
            // However, since the Hub is short-lived, we handle the broadcast
            // via the event on PresenceService that we wire up at startup.
            // For immediate feedback, we do nothing here — the grace timer handles it.
            _logger.LogDebug(
                "User {UserId} disconnected, grace period started (connection {ConnectionId})",
                userId, Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task JoinChannel(Guid channelId)
    {
        var userId = GetUserId();
        var displayName = await GetDisplayNameAsync(userId);

        await Groups.AddToGroupAsync(Context.ConnectionId, channelId.ToString());

        _logger.LogInformation(
            "User {UserId} joined channel {ChannelId}",
            userId, channelId);

        await Clients.Group(channelId.ToString())
            .SendAsync("UserJoinedChannel", channelId, userId, displayName);
    }

    public async Task LeaveChannel(Guid channelId)
    {
        var userId = GetUserId();

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, channelId.ToString());

        _logger.LogInformation(
            "User {UserId} left channel {ChannelId}",
            userId, channelId);

        await Clients.Group(channelId.ToString())
            .SendAsync("UserLeftChannel", channelId, userId);
    }

    public async Task SendMessage(Guid channelId, string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new HubException("Message content cannot be empty.");

        var userId = GetUserId();

        var message = await _messageService.SendAsync(channelId, userId, content);

        var authorDisplayName = message.Author?.DisplayName ?? "Unknown";

        var response = new MessageResponse
        {
            Id = message.Id,
            Content = message.Content,
            ChannelId = message.ChannelId,
            AuthorId = message.AuthorId,
            AuthorDisplayName = authorDisplayName,
            IsAgent = message.Author?.IsAgent ?? false,
            CreatedAtUtc = message.CreatedAtUtc,
        };

        await Clients.Group(channelId.ToString())
            .SendAsync("ReceiveMessage", response);

        // Process @mention notifications
        var channel = await _channelService.GetByIdAsync(channelId);
        var channelName = channel?.Name ?? "Unknown";

        await _notificationService.ProcessMessageNotificationsAsync(
            userId,
            authorDisplayName,
            channelId,
            channelName,
            content);

        // Notify all other connected clients that this channel has a new message.
        // The client increments its local unread count — avoids N+1 per-user DB queries.
        await Clients.Others.SendAsync("UnreadCountUpdated", channelId);
    }

    public async Task StartTyping(Guid channelId)
    {
        var userId = GetUserId();
        var displayName = await GetDisplayNameAsync(userId);

        await Clients.OthersInGroup(channelId.ToString())
            .SendAsync("UserTyping", channelId, userId, displayName);
    }

    public async Task StopTyping(Guid channelId)
    {
        var userId = GetUserId();

        await Clients.OthersInGroup(channelId.ToString())
            .SendAsync("UserStoppedTyping", channelId, userId);
    }

    public async Task SendDirectMessage(Guid recipientId, string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new HubException("Message content cannot be empty.");

        var senderId = GetUserId();

        var message = await _directMessageService.SendAsync(senderId, recipientId, content);

        var senderDisplayName = await GetDisplayNameAsync(senderId);

        var response = new DirectMessageResponse
        {
            Id = message.Id,
            Content = message.Content,
            SenderId = message.SenderId,
            SenderDisplayName = senderDisplayName,
            RecipientId = message.RecipientId,
            CreatedAtUtc = message.CreatedAtUtc,
            ReadAtUtc = message.ReadAtUtc,
        };

        await Clients.User(recipientId.ToString())
            .SendAsync("ReceiveDirectMessage", response);

        await Clients.User(senderId.ToString())
            .SendAsync("ReceiveDirectMessage", response);

        // Notify the recipient that they have a new unread DM from this sender.
        // The client increments its local count — avoids unnecessary DB query.
        await Clients.User(recipientId.ToString())
            .SendAsync("DmUnreadCountUpdated", senderId);
    }

    public async Task DirectMessageTyping(Guid recipientId)
    {
        var senderId = GetUserId();
        var displayName = await GetDisplayNameAsync(senderId);

        await Clients.User(recipientId.ToString())
            .SendAsync("DirectMessageTyping", senderId, displayName);
    }

    public async Task DirectMessageStoppedTyping(Guid recipientId)
    {
        var senderId = GetUserId();

        await Clients.User(recipientId.ToString())
            .SendAsync("DirectMessageStoppedTyping", senderId);
    }

    /// <summary>
    /// Client calls this periodically to signal activity, resetting the 5-minute idle timer.
    /// </summary>
    public Task Heartbeat()
    {
        var userId = GetUserId();
        _presenceService.RecordHeartbeat(userId);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Client calls this to manually set their status to DoNotDisturb.
    /// </summary>
    public async Task SetStatus(UserStatus status)
    {
        var userId = GetUserId();

        var user = await _userManager.FindByIdAsync(userId.ToString());
        var displayName = user?.DisplayName ?? "Unknown";
        var isAgent = user?.IsAgent ?? false;

        switch (status)
        {
            case UserStatus.Online:
                await _presenceService.SetOnlineAsync(userId, Context.ConnectionId, displayName, isAgent);
                await Clients.Others.SendAsync("UserStatusChanged", userId, displayName, UserStatus.Online, isAgent);
                break;
            case UserStatus.DoNotDisturb:
                await _presenceService.SetDoNotDisturbAsync(userId);
                await Clients.Others.SendAsync("UserStatusChanged", userId, displayName, UserStatus.DoNotDisturb, isAgent);
                break;
            case UserStatus.Idle:
                await _presenceService.SetIdleAsync(userId);
                await Clients.Others.SendAsync("UserStatusChanged", userId, displayName, UserStatus.Idle, isAgent);
                break;
            default:
                throw new HubException("Cannot manually set status to Offline. Disconnect instead.");
        }
    }

    private Guid GetUserId()
    {
        var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            throw new HubException("Unable to determine user identity.");
        }

        return userId;
    }

    private async Task<string> GetDisplayNameAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        return user?.DisplayName ?? "Unknown";
    }
}
