using System.Collections.Concurrent;
using System.Security.Claims;
using HotBox.Core.Entities;
using HotBox.Core.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HotBox.Application.Hubs;

[Authorize]
public class VoiceSignalingHub : Hub
{
    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, VoiceUser>> _voiceChannels = new();

    private readonly UserManager<AppUser> _userManager;
    private readonly IOptions<IceServerOptions> _iceServerOptions;
    private readonly ILogger<VoiceSignalingHub> _logger;

    public VoiceSignalingHub(
        UserManager<AppUser> userManager,
        IOptions<IceServerOptions> iceServerOptions,
        ILogger<VoiceSignalingHub> logger)
    {
        _userManager = userManager;
        _iceServerOptions = iceServerOptions;
        _logger = logger;
    }

    public async Task JoinVoiceChannel(Guid channelId)
    {
        var userId = GetUserId();
        var displayName = await GetDisplayNameAsync(userId);
        var channelKey = channelId.ToString();

        var voiceUser = new VoiceUser
        {
            UserId = userId,
            DisplayName = displayName,
            ConnectionId = Context.ConnectionId,
            IsMuted = false,
            IsDeafened = false,
        };

        var channelUsers = _voiceChannels.GetOrAdd(channelKey, _ => new ConcurrentDictionary<string, VoiceUser>());
        channelUsers[Context.ConnectionId] = voiceUser;

        await Groups.AddToGroupAsync(Context.ConnectionId, channelKey);

        _logger.LogInformation(
            "User {UserId} joined voice channel {ChannelId}",
            userId, channelId);

        // Send the current user list to the joining user (without ConnectionId)
        var currentUsers = channelUsers.Values
            .Select(u => new VoiceUserDto(u.UserId, u.DisplayName, u.IsMuted, u.IsDeafened))
            .ToArray();

        await Clients.Caller.SendAsync("VoiceChannelUsers", channelId, currentUsers);

        // Notify other peers that a new user joined (without ConnectionId)
        await Clients.OthersInGroup(channelKey)
            .SendAsync("UserJoinedVoice", channelId,
                new VoiceUserDto(voiceUser.UserId, voiceUser.DisplayName, voiceUser.IsMuted, voiceUser.IsDeafened));
    }

    public async Task LeaveVoiceChannel(Guid channelId)
    {
        var userId = GetUserId();
        var channelKey = channelId.ToString();

        RemoveUserFromChannel(channelKey, Context.ConnectionId);

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, channelKey);

        _logger.LogInformation(
            "User {UserId} left voice channel {ChannelId}",
            userId, channelId);

        await Clients.OthersInGroup(channelKey)
            .SendAsync("UserLeftVoice", channelId, userId);
    }

    public async Task SendOffer(Guid targetUserId, string sdp)
    {
        var userId = GetUserId();
        var target = FindUserByUserId(targetUserId);

        if (target is null)
        {
            _logger.LogWarning(
                "User {UserId} tried to send SDP offer to unknown user {TargetUserId}",
                userId, targetUserId);
            return;
        }

        _logger.LogDebug(
            "User {UserId} sending SDP offer to user {TargetUserId}",
            userId, targetUserId);

        await Clients.Client(target.ConnectionId)
            .SendAsync("ReceiveOffer", userId, sdp);
    }

    public async Task SendAnswer(Guid targetUserId, string sdp)
    {
        var userId = GetUserId();
        var target = FindUserByUserId(targetUserId);

        if (target is null)
        {
            _logger.LogWarning(
                "User {UserId} tried to send SDP answer to unknown user {TargetUserId}",
                userId, targetUserId);
            return;
        }

        _logger.LogDebug(
            "User {UserId} sending SDP answer to user {TargetUserId}",
            userId, targetUserId);

        await Clients.Client(target.ConnectionId)
            .SendAsync("ReceiveAnswer", userId, sdp);
    }

    public async Task SendIceCandidate(Guid targetUserId, string candidate)
    {
        var userId = GetUserId();
        var target = FindUserByUserId(targetUserId);

        if (target is null)
        {
            _logger.LogWarning(
                "User {UserId} tried to send ICE candidate to unknown user {TargetUserId}",
                userId, targetUserId);
            return;
        }

        _logger.LogDebug(
            "User {UserId} sending ICE candidate to user {TargetUserId}",
            userId, targetUserId);

        await Clients.Client(target.ConnectionId)
            .SendAsync("ReceiveIceCandidate", userId, candidate);
    }

    public async Task ToggleMute(Guid channelId, bool isMuted)
    {
        var userId = GetUserId();
        var channelKey = channelId.ToString();

        if (_voiceChannels.TryGetValue(channelKey, out var channelUsers)
            && channelUsers.TryGetValue(Context.ConnectionId, out var voiceUser))
        {
            voiceUser.IsMuted = isMuted;
        }

        _logger.LogDebug(
            "User {UserId} toggled mute to {IsMuted} in channel {ChannelId}",
            userId, isMuted, channelId);

        await Clients.OthersInGroup(channelKey)
            .SendAsync("UserMuteChanged", channelId, userId, isMuted);
    }

    public async Task ToggleDeafen(Guid channelId, bool isDeafened)
    {
        var userId = GetUserId();
        var channelKey = channelId.ToString();

        if (_voiceChannels.TryGetValue(channelKey, out var channelUsers)
            && channelUsers.TryGetValue(Context.ConnectionId, out var voiceUser))
        {
            voiceUser.IsDeafened = isDeafened;
        }

        _logger.LogDebug(
            "User {UserId} toggled deafen to {IsDeafened} in channel {ChannelId}",
            userId, isDeafened, channelId);

        await Clients.OthersInGroup(channelKey)
            .SendAsync("UserDeafenChanged", channelId, userId, isDeafened);
    }

    public object[] GetIceServers()
    {
        var options = _iceServerOptions.Value;
        var servers = new List<object>();

        // Add STUN servers
        if (options.StunUrls.Length > 0)
        {
            servers.Add(new { urls = options.StunUrls });
        }

        // Add TURN server if configured
        if (!string.IsNullOrEmpty(options.TurnUrl))
        {
            servers.Add(new
            {
                urls = new[] { options.TurnUrl },
                username = options.TurnUsername,
                credential = options.TurnCredential,
            });
        }

        return servers.ToArray();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var connectionId = Context.ConnectionId;
        Guid? removedUserId = null;
        var channelsLeft = new List<string>();

        foreach (var (channelKey, channelUsers) in _voiceChannels)
        {
            if (channelUsers.TryRemove(connectionId, out var voiceUser))
            {
                removedUserId = voiceUser.UserId;
                channelsLeft.Add(channelKey);

                // Clean up empty channels
                if (channelUsers.IsEmpty)
                {
                    _voiceChannels.TryRemove(channelKey, out _);
                }
            }
        }

        if (removedUserId.HasValue)
        {
            _logger.LogInformation(
                "User {UserId} disconnected from voice channels {ChannelIds}",
                removedUserId.Value, string.Join(", ", channelsLeft));

            foreach (var channelKey in channelsLeft)
            {
                await Clients.Group(channelKey)
                    .SendAsync("UserLeftVoice", Guid.Parse(channelKey), removedUserId.Value);
            }
        }

        await base.OnDisconnectedAsync(exception);
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

    /// <summary>
    /// Finds a VoiceUser across all channels by their UserId.
    /// Returns null if the user is not in any voice channel.
    /// </summary>
    private static VoiceUser? FindUserByUserId(Guid targetUserId)
    {
        foreach (var channelUsers in _voiceChannels.Values)
        {
            foreach (var voiceUser in channelUsers.Values)
            {
                if (voiceUser.UserId == targetUserId)
                {
                    return voiceUser;
                }
            }
        }

        return null;
    }

    private static void RemoveUserFromChannel(string channelKey, string connectionId)
    {
        if (_voiceChannels.TryGetValue(channelKey, out var channelUsers))
        {
            channelUsers.TryRemove(connectionId, out _);

            // Clean up empty channels
            if (channelUsers.IsEmpty)
            {
                _voiceChannels.TryRemove(channelKey, out _);
            }
        }
    }

    /// <summary>
    /// Internal tracking class with ConnectionId for server-side routing.
    /// Never sent to clients directly.
    /// </summary>
    private class VoiceUser
    {
        public Guid UserId { get; init; }
        public string DisplayName { get; init; } = string.Empty;
        public string ConnectionId { get; init; } = string.Empty;
        public bool IsMuted { get; set; }
        public bool IsDeafened { get; set; }
    }

    /// <summary>
    /// DTO sent to clients. Excludes ConnectionId for security.
    /// </summary>
    private record VoiceUserDto(Guid UserId, string DisplayName, bool IsMuted, bool IsDeafened);
}
