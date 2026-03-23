using System.Collections.Concurrent;
using System.Security.Claims;
using HotBox.Core.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;

namespace HotBox.Application.Hubs;

[Authorize]
public class VoiceSignalingHub : Hub
{
    // channelId -> set of (userId, displayName)
    private static readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, string>> ChannelParticipants = new();

    private readonly UserManager<AppUser> _userManager;
    private readonly ILogger<VoiceSignalingHub> _logger;

    public VoiceSignalingHub(UserManager<AppUser> userManager, ILogger<VoiceSignalingHub> logger)
    {
        _userManager = userManager;
        _logger = logger;
    }

    public async Task JoinVoiceChannel(Guid channelId)
    {
        var userId = GetUserId();
        var user = await _userManager.FindByIdAsync(userId.ToString());
        var displayName = user?.DisplayName ?? "Unknown";

        var participants = ChannelParticipants.GetOrAdd(channelId, _ => new ConcurrentDictionary<Guid, string>());
        participants[userId] = displayName;

        await Groups.AddToGroupAsync(Context.ConnectionId, $"voice:{channelId}");

        // Notify others in the voice channel
        await Clients.OthersInGroup($"voice:{channelId}")
            .SendAsync("UserJoinedVoice", channelId, userId, displayName);

        // Send current participants to the joiner
        var currentParticipants = participants
            .Select(kvp => new { UserId = kvp.Key, DisplayName = kvp.Value })
            .ToList();
        await Clients.Caller.SendAsync("VoiceChannelParticipants", channelId, currentParticipants);

        // Also notify the channel group (text side) so UI can update voice indicators
        await Clients.Group(channelId.ToString())
            .SendAsync("UserJoinedVoice", channelId, userId, displayName);

        _logger.LogInformation("User {UserId} ({DisplayName}) joined voice channel {ChannelId}", userId, displayName, channelId);
    }

    public async Task LeaveVoiceChannel(Guid channelId)
    {
        var userId = GetUserId();

        if (ChannelParticipants.TryGetValue(channelId, out var participants))
        {
            participants.TryRemove(userId, out var displayName);

            if (participants.IsEmpty)
            {
                ChannelParticipants.TryRemove(channelId, out _);
            }

            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"voice:{channelId}");

            await Clients.OthersInGroup($"voice:{channelId}")
                .SendAsync("UserLeftVoice", channelId, userId);

            await Clients.Group(channelId.ToString())
                .SendAsync("UserLeftVoice", channelId, userId);

            _logger.LogInformation("User {UserId} left voice channel {ChannelId}", userId, channelId);
        }
    }

    public async Task SendOffer(Guid targetUserId, string sdp)
    {
        var userId = GetUserId();
        await Clients.User(targetUserId.ToString())
            .SendAsync("ReceiveOffer", userId, sdp);
    }

    public async Task SendAnswer(Guid targetUserId, string sdp)
    {
        var userId = GetUserId();
        await Clients.User(targetUserId.ToString())
            .SendAsync("ReceiveAnswer", userId, sdp);
    }

    public async Task SendIceCandidate(Guid targetUserId, string candidate)
    {
        var userId = GetUserId();
        await Clients.User(targetUserId.ToString())
            .SendAsync("ReceiveIceCandidate", userId, candidate);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();

        // Remove user from all voice channels they were in
        foreach (var kvp in ChannelParticipants)
        {
            var channelId = kvp.Key;
            var participants = kvp.Value;

            if (participants.TryRemove(userId, out _))
            {
                if (participants.IsEmpty)
                {
                    ChannelParticipants.TryRemove(channelId, out _);
                }

                await Clients.Group($"voice:{channelId}")
                    .SendAsync("UserLeftVoice", channelId, userId);

                await Clients.Group(channelId.ToString())
                    .SendAsync("UserLeftVoice", channelId, userId);

                _logger.LogInformation("User {UserId} removed from voice channel {ChannelId} on disconnect", userId, channelId);
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    private Guid GetUserId()
    {
        var userIdStr = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
                        ?? throw new HubException("User ID not found in claims.");
        return Guid.Parse(userIdStr);
    }
}
