using System.Security.Claims;
using HotBox.Application.Models;
using HotBox.Core.Entities;
using HotBox.Core.Enums;
using HotBox.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;

namespace HotBox.Application.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly IMessageService _messageService;
    private readonly IDirectMessageService _directMessageService;
    private readonly IPresenceService _presenceService;
    private readonly IReadStateService _readStateService;
    private readonly IChannelService _channelService;
    private readonly UserManager<AppUser> _userManager;
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(
        IMessageService messageService,
        IDirectMessageService directMessageService,
        IPresenceService presenceService,
        IReadStateService readStateService,
        IChannelService channelService,
        UserManager<AppUser> userManager,
        ILogger<ChatHub> logger)
    {
        _messageService = messageService;
        _directMessageService = directMessageService;
        _presenceService = presenceService;
        _readStateService = readStateService;
        _channelService = channelService;
        _userManager = userManager;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            _logger.LogWarning("Connected user {UserId} not found in database", userId);
            return;
        }

        await _presenceService.SetOnlineAsync(userId, Context.ConnectionId, user.DisplayName, user.IsAgent);

        // Send online users list to the newly connected client
        var onlineUsers = _presenceService.GetAllOnlineUsers()
            .Select(u => new OnlineUserInfo(u.UserId, u.DisplayName, u.Status, u.IsAgent))
            .ToList();
        await Clients.Caller.SendAsync("OnlineUsers", onlineUsers);

        // Auto-join all text channel groups
        var channels = await _channelService.GetByTypeAsync(ChannelType.Text);
        foreach (var channel in channels)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, channel.Id.ToString());
        }

        // Also join voice channel groups so users receive voice events
        var voiceChannels = await _channelService.GetByTypeAsync(ChannelType.Voice);
        foreach (var channel in voiceChannels)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, channel.Id.ToString());
        }

        _logger.LogInformation("User {UserId} ({DisplayName}) connected", userId, user.DisplayName);

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();
        var noConnectionsLeft = _presenceService.RemoveConnection(userId, Context.ConnectionId);

        if (noConnectionsLeft)
        {
            _logger.LogDebug("User {UserId} last connection removed, grace period started", userId);
        }

        if (exception is not null)
        {
            _logger.LogWarning(exception, "User {UserId} disconnected with error", userId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task SendMessage(Guid channelId, string content)
    {
        var userId = GetUserId();
        var message = await _messageService.SendAsync(channelId, userId, content);

        var response = new MessageResponse(
            message.Id,
            message.Content,
            message.ChannelId,
            message.UserId,
            message.User.DisplayName,
            message.User.AvatarUrl,
            message.User.IsAgent,
            message.CreatedAt,
            message.EditedAt);

        await Clients.Group(channelId.ToString()).SendAsync("ReceiveMessage", response);
    }

    public Task EditMessage(Guid messageId, string newContent)
    {
        // Not yet implemented
        _logger.LogDebug("EditMessage called for {MessageId} but not yet implemented", messageId);
        return Task.CompletedTask;
    }

    public Task DeleteMessage(Guid messageId)
    {
        // Not yet implemented
        _logger.LogDebug("DeleteMessage called for {MessageId} but not yet implemented", messageId);
        return Task.CompletedTask;
    }

    public async Task JoinChannel(Guid channelId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, channelId.ToString());
        _logger.LogDebug("User {UserId} joined channel group {ChannelId}", GetUserId(), channelId);
    }

    public async Task LeaveChannel(Guid channelId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, channelId.ToString());
        _logger.LogDebug("User {UserId} left channel group {ChannelId}", GetUserId(), channelId);
    }

    public async Task StartTyping(Guid channelId)
    {
        var userId = GetUserId();
        var user = await _userManager.FindByIdAsync(userId.ToString());
        await Clients.OthersInGroup(channelId.ToString())
            .SendAsync("UserTyping", channelId, userId, user?.DisplayName ?? "Unknown");
    }

    public async Task StopTyping(Guid channelId)
    {
        var userId = GetUserId();
        await Clients.OthersInGroup(channelId.ToString())
            .SendAsync("UserStoppedTyping", channelId, userId);
    }

    public async Task UpdateStatus(UserStatus status)
    {
        var userId = GetUserId();
        switch (status)
        {
            case UserStatus.Online:
                var user = await _userManager.FindByIdAsync(userId.ToString());
                await _presenceService.SetOnlineAsync(userId, Context.ConnectionId, user?.DisplayName ?? "Unknown");
                break;
            case UserStatus.Idle:
                await _presenceService.SetIdleAsync(userId);
                break;
            case UserStatus.DoNotDisturb:
                await _presenceService.SetDoNotDisturbAsync(userId);
                break;
            case UserStatus.Offline:
                await _presenceService.SetOfflineAsync(userId);
                break;
        }
    }

    public async Task SendDirectMessage(Guid recipientId, string content)
    {
        var userId = GetUserId();
        var dm = await _directMessageService.SendAsync(userId, recipientId, content);

        var response = new DirectMessageResponse(
            dm.Id,
            dm.Content,
            dm.SenderId,
            dm.Sender.DisplayName,
            dm.Sender.AvatarUrl,
            dm.RecipientId,
            dm.Recipient.DisplayName,
            dm.CreatedAt,
            dm.EditedAt,
            dm.ReadAt);

        // Send to both sender and recipient
        await Clients.User(userId.ToString()).SendAsync("ReceiveDirectMessage", response);
        await Clients.User(recipientId.ToString()).SendAsync("ReceiveDirectMessage", response);
    }

    public async Task MarkChannelRead(Guid channelId)
    {
        var userId = GetUserId();
        await _readStateService.MarkAsReadAsync(userId, channelId);
    }

    public void Heartbeat()
    {
        var userId = GetUserId();
        _presenceService.RecordHeartbeat(userId);
    }

    private Guid GetUserId()
    {
        var userIdStr = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
                        ?? throw new HubException("User ID not found in claims.");
        return Guid.Parse(userIdStr);
    }
}
