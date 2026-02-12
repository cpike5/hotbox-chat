using HotBox.Client.Models;
using HotBox.Client.State;
using Microsoft.Extensions.Logging;

namespace HotBox.Client.Services;

/// <summary>
/// Orchestrates voice channel connections by coordinating between
/// <see cref="VoiceHubService"/> (signaling), <see cref="WebRtcService"/> (peer connections),
/// and <see cref="VoiceState"/> (UI state).
/// </summary>
public class VoiceConnectionManager : IAsyncDisposable
{
    private readonly VoiceHubService _voiceHub;
    private readonly WebRtcService _webRtc;
    private readonly VoiceState _voiceState;
    private readonly ILogger<VoiceConnectionManager> _logger;

    /// <summary>
    /// Maps connectionId to userId for peers in the current voice channel.
    /// The hub uses connectionId for signaling routing, but the UI needs userId for display.
    /// </summary>
    private readonly Dictionary<string, Guid> _connectionToUser = new();

    /// <summary>
    /// Maps userId to connectionId. Inverse of <see cref="_connectionToUser"/>.
    /// </summary>
    private readonly Dictionary<Guid, string> _userToConnection = new();

    private IceServerInfo[]? _currentIceServers;

    public VoiceConnectionManager(
        VoiceHubService voiceHub,
        WebRtcService webRtc,
        VoiceState voiceState,
        ILogger<VoiceConnectionManager> logger)
    {
        _voiceHub = voiceHub;
        _webRtc = webRtc;
        _voiceState = voiceState;
        _logger = logger;
    }

    /// <summary>
    /// Starts the voice hub connection and subscribes to all signaling and WebRTC events.
    /// Should be called once when the user authenticates.
    /// </summary>
    public async Task InitializeAsync(string accessToken)
    {
        // Start voice hub connection
        await _voiceHub.StartAsync(accessToken);

        // Subscribe to hub events
        _voiceHub.OnUserJoinedVoice += HandleUserJoined;
        _voiceHub.OnUserLeftVoice += HandleUserLeft;
        _voiceHub.OnReceiveOffer += HandleReceiveOffer;
        _voiceHub.OnReceiveAnswer += HandleReceiveAnswer;
        _voiceHub.OnReceiveIceCandidate += HandleReceiveIceCandidate;
        _voiceHub.OnUserMuteChanged += HandleUserMuteChanged;
        _voiceHub.OnUserDeafenChanged += HandleUserDeafenChanged;
        _voiceHub.OnVoiceChannelUsers += HandleVoiceChannelUsers;
        _voiceHub.OnConnectionChanged += HandleConnectionChanged;

        // Subscribe to WebRTC callbacks
        _webRtc.OnIceCandidateReady += HandleIceCandidateReady;
        _webRtc.OnPeerConnectionStateChanged += HandlePeerConnectionStateChanged;
    }

    /// <summary>
    /// Joins a voice channel: initializes local audio, fetches ICE servers,
    /// and sends the join request to the server.
    /// </summary>
    public async Task JoinChannelAsync(Guid channelId, string channelName)
    {
        _voiceState.SetConnectionStatus(VoiceConnectionStatus.Connecting);

        try
        {
            // Initialize local audio
            await _webRtc.InitializeAsync();

            // Get ICE server config and convert to IceServerInfo array for WebRTC
            var config = await _voiceHub.GetIceServersAsync();
            _currentIceServers = ConvertIceServerConfig(config);

            // Join the voice channel on server
            await _voiceHub.JoinVoiceChannelAsync(channelId);

            _voiceState.SetConnected(channelId, channelName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to join voice channel {ChannelId}", channelId);
            _voiceState.SetDisconnected();
            throw;
        }
    }

    /// <summary>
    /// Leaves the current voice channel, closing all peer connections.
    /// </summary>
    public async Task LeaveChannelAsync()
    {
        var channelId = _voiceState.CurrentVoiceChannelId;
        if (channelId is null) return;

        try
        {
            await _webRtc.CloseAllConnectionsAsync();
            await _voiceHub.LeaveVoiceChannelAsync(channelId.Value);
        }
        finally
        {
            ClearPeerMappings();
            _voiceState.SetDisconnected();
        }
    }

    /// <summary>
    /// Toggles the local mute state and notifies the server.
    /// </summary>
    public async Task ToggleMuteAsync()
    {
        var newMuted = !_voiceState.IsMuted;
        _voiceState.SetMuted(newMuted);
        await _webRtc.SetMicrophoneEnabledAsync(!newMuted);

        if (_voiceState.CurrentVoiceChannelId is Guid channelId)
        {
            await _voiceHub.ToggleMuteAsync(channelId, newMuted);
        }
    }

    /// <summary>
    /// Toggles the local deafen state and mutes/unmutes remote audio playback.
    /// </summary>
    public async Task ToggleDeafenAsync()
    {
        var newDeafened = !_voiceState.IsDeafened;
        _voiceState.SetDeafened(newDeafened);
        await _webRtc.SetRemoteAudioMutedAsync(newDeafened);

        if (_voiceState.CurrentVoiceChannelId is Guid channelId)
        {
            await _voiceHub.ToggleDeafenAsync(channelId, newDeafened);
        }
    }

    // --- Event Handlers ---

    private async void HandleUserJoined(Guid channelId, VoiceUserInfo userInfo)
    {
        // A new user joined our channel -- create peer connection and send offer
        if (channelId != _voiceState.CurrentVoiceChannelId) return;

        TrackPeer(userInfo);
        _voiceState.AddPeer(new VoicePeerInfo
        {
            UserId = userInfo.UserId,
            DisplayName = userInfo.DisplayName,
            IsMuted = userInfo.IsMuted,
            IsDeafened = userInfo.IsDeafened
        });

        try
        {
            // Use connectionId as peerId for WebRTC (matches what the hub sends in ReceiveOffer/Answer)
            await _webRtc.CreatePeerConnectionAsync(userInfo.ConnectionId, _currentIceServers ?? []);
            var offer = await _webRtc.CreateOfferAsync(userInfo.ConnectionId);
            await _voiceHub.SendOfferAsync(userInfo.ConnectionId, offer);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create offer for peer {UserId}", userInfo.UserId);
        }
    }

    private async void HandleReceiveOffer(string fromConnectionId, string sdp)
    {
        // Received offer from peer -- set remote desc, create answer, send it back
        try
        {
            await _webRtc.CreatePeerConnectionAsync(fromConnectionId, _currentIceServers ?? []);
            await _webRtc.SetRemoteDescriptionAsync(fromConnectionId, "offer", sdp);
            var answer = await _webRtc.CreateAnswerAsync(fromConnectionId);
            await _voiceHub.SendAnswerAsync(fromConnectionId, answer);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle offer from connection {ConnectionId}", fromConnectionId);
        }
    }

    private async void HandleReceiveAnswer(string fromConnectionId, string sdp)
    {
        try
        {
            await _webRtc.SetRemoteDescriptionAsync(fromConnectionId, "answer", sdp);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle answer from connection {ConnectionId}", fromConnectionId);
        }
    }

    private async void HandleReceiveIceCandidate(string fromConnectionId, string candidateJson)
    {
        try
        {
            await _webRtc.AddIceCandidateAsync(fromConnectionId, candidateJson);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add ICE candidate from connection {ConnectionId}", fromConnectionId);
        }
    }

    private async void HandleIceCandidateReady(string peerId, string candidateJson)
    {
        // Local ICE candidate ready -- send to peer via signaling
        // peerId here is the connectionId of the remote peer
        try
        {
            await _voiceHub.SendIceCandidateAsync(peerId, candidateJson);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send ICE candidate to peer {PeerId}", peerId);
        }
    }

    private void HandleUserLeft(Guid channelId, Guid userId)
    {
        if (channelId != _voiceState.CurrentVoiceChannelId) return;

        // Find the connectionId for this user so we can close the right peer connection
        if (_userToConnection.TryGetValue(userId, out var connectionId))
        {
            _ = _webRtc.CloseConnectionAsync(connectionId);
            UntrackPeer(userId);
        }

        _voiceState.RemovePeer(userId);
    }

    private void HandleVoiceChannelUsers(Guid channelId, VoiceUserInfo[] users)
    {
        // Received current user list on join -- populate peers and track mappings
        foreach (var user in users)
        {
            TrackPeer(user);
            _voiceState.AddPeer(new VoicePeerInfo
            {
                UserId = user.UserId,
                DisplayName = user.DisplayName,
                IsMuted = user.IsMuted,
                IsDeafened = user.IsDeafened
            });
        }
        // Note: We don't create peer connections here -- the existing users will
        // receive UserJoinedVoice and send us offers, which we'll answer
    }

    private void HandleUserMuteChanged(Guid channelId, Guid userId, bool isMuted)
    {
        _voiceState.UpdatePeerMute(userId, isMuted);
    }

    private void HandleUserDeafenChanged(Guid channelId, Guid userId, bool isDeafened)
    {
        _voiceState.UpdatePeerDeafen(userId, isDeafened);
    }

    private void HandlePeerConnectionStateChanged(string peerId, string state)
    {
        _logger.LogInformation("Peer {PeerId} connection state: {State}", peerId, state);
    }

    private void HandleConnectionChanged(bool isConnected)
    {
        if (!isConnected)
        {
            _voiceState.SetConnectionStatus(VoiceConnectionStatus.Disconnected);
        }
    }

    // --- Peer mapping helpers ---

    private void TrackPeer(VoiceUserInfo userInfo)
    {
        _connectionToUser[userInfo.ConnectionId] = userInfo.UserId;
        _userToConnection[userInfo.UserId] = userInfo.ConnectionId;
    }

    private void UntrackPeer(Guid userId)
    {
        if (_userToConnection.TryGetValue(userId, out var connectionId))
        {
            _connectionToUser.Remove(connectionId);
            _userToConnection.Remove(userId);
        }
    }

    private void ClearPeerMappings()
    {
        _connectionToUser.Clear();
        _userToConnection.Clear();
    }

    /// <summary>
    /// Converts the hub's <see cref="IceServerConfig"/> into an array of
    /// <see cref="IceServerInfo"/> suitable for passing to the WebRTC JS layer.
    /// </summary>
    private static IceServerInfo[] ConvertIceServerConfig(IceServerConfig config)
    {
        var servers = new List<IceServerInfo>();

        if (config.StunUrls.Length > 0)
        {
            servers.Add(new IceServerInfo { Urls = config.StunUrls });
        }

        if (!string.IsNullOrEmpty(config.TurnUrl))
        {
            servers.Add(new IceServerInfo
            {
                Urls = [config.TurnUrl],
                Username = config.TurnUsername,
                Credential = config.TurnCredential
            });
        }

        return servers.ToArray();
    }

    public ValueTask DisposeAsync()
    {
        // Unsubscribe events
        _voiceHub.OnUserJoinedVoice -= HandleUserJoined;
        _voiceHub.OnUserLeftVoice -= HandleUserLeft;
        _voiceHub.OnReceiveOffer -= HandleReceiveOffer;
        _voiceHub.OnReceiveAnswer -= HandleReceiveAnswer;
        _voiceHub.OnReceiveIceCandidate -= HandleReceiveIceCandidate;
        _voiceHub.OnUserMuteChanged -= HandleUserMuteChanged;
        _voiceHub.OnUserDeafenChanged -= HandleUserDeafenChanged;
        _voiceHub.OnVoiceChannelUsers -= HandleVoiceChannelUsers;
        _voiceHub.OnConnectionChanged -= HandleConnectionChanged;
        _webRtc.OnIceCandidateReady -= HandleIceCandidateReady;
        _webRtc.OnPeerConnectionStateChanged -= HandlePeerConnectionStateChanged;

        ClearPeerMappings();

        return ValueTask.CompletedTask;
    }
}
