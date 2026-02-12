using System.Security.Claims;
using HotBox.Application.Models;
using HotBox.Core.Entities;
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
    private readonly UserManager<AppUser> _userManager;
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(
        IMessageService messageService,
        IDirectMessageService directMessageService,
        UserManager<AppUser> userManager,
        ILogger<ChatHub> logger)
    {
        _messageService = messageService;
        _directMessageService = directMessageService;
        _userManager = userManager;
        _logger = logger;
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

        var response = new MessageResponse
        {
            Id = message.Id,
            Content = message.Content,
            ChannelId = message.ChannelId,
            AuthorId = message.AuthorId,
            AuthorDisplayName = message.Author?.DisplayName ?? "Unknown",
            CreatedAtUtc = message.CreatedAtUtc,
        };

        await Clients.Group(channelId.ToString())
            .SendAsync("ReceiveMessage", response);
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
