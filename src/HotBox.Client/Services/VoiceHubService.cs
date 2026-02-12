using HotBox.Client.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

namespace HotBox.Client.Services;

public class VoiceHubService : IAsyncDisposable
{
    private readonly string _baseUrl;
    private readonly ILogger<VoiceHubService> _logger;
    private HubConnection? _hubConnection;

    public VoiceHubService(NavigationManager navigation, ILogger<VoiceHubService> logger)
    {
        _baseUrl = navigation.BaseUri.TrimEnd('/');
        _logger = logger;
    }

    /// <summary>
    /// Indicates whether the hub connection is currently in the Connected state.
    /// </summary>
    public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;

    // ----- Events for UI components to subscribe to -----

    /// <summary>Raised when a user joins a voice channel. Receives the channel ID and user info DTO.</summary>
    public event Action<Guid, VoiceUserInfo>? OnUserJoinedVoice;

    /// <summary>Raised when a user leaves a voice channel.</summary>
    public event Action<Guid, Guid>? OnUserLeftVoice;

    /// <summary>Raised when a WebRTC offer is received from another user. Receives the sender's connectionId and SDP.</summary>
    public event Action<string, string>? OnReceiveOffer;

    /// <summary>Raised when a WebRTC answer is received from another user. Receives the sender's connectionId and SDP.</summary>
    public event Action<string, string>? OnReceiveAnswer;

    /// <summary>Raised when an ICE candidate is received from another user. Receives the sender's connectionId and candidate JSON.</summary>
    public event Action<string, string>? OnReceiveIceCandidate;

    /// <summary>Raised when a user's mute state changes.</summary>
    public event Action<Guid, Guid, bool>? OnUserMuteChanged;

    /// <summary>Raised when a user's deafen state changes.</summary>
    public event Action<Guid, Guid, bool>? OnUserDeafenChanged;

    /// <summary>Raised when the list of users in a voice channel is received (on join).</summary>
    public event Action<Guid, VoiceUserInfo[]>? OnVoiceChannelUsers;

    /// <summary>Raised when the connection state changes. True means connected, false means disconnected or reconnecting.</summary>
    public event Action<bool>? OnConnectionChanged;

    // ----- Connection lifecycle -----

    /// <summary>
    /// Builds the hub connection with JWT authentication and automatic reconnect, then starts it.
    /// </summary>
    public async Task StartAsync(string accessToken)
    {
        if (_hubConnection is not null)
        {
            _logger.LogWarning("StartAsync called while a hub connection already exists; stopping existing connection first");
            await StopAsync();
        }

        _hubConnection = new HubConnectionBuilder()
            .WithUrl($"{_baseUrl}/hubs/voice", options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(accessToken);
            })
            .WithAutomaticReconnect()
            .Build();

        RegisterHandlers(_hubConnection);
        RegisterLifecycleEvents(_hubConnection);

        try
        {
            await _hubConnection.StartAsync();
            _logger.LogInformation("VoiceHub connection started");
            OnConnectionChanged?.Invoke(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start VoiceHub connection");
            throw;
        }
    }

    /// <summary>
    /// Gracefully stops the hub connection if one is active.
    /// </summary>
    public async Task StopAsync()
    {
        if (_hubConnection is not null)
        {
            try
            {
                await _hubConnection.StopAsync();
                _logger.LogInformation("VoiceHub connection stopped");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping VoiceHub connection");
            }

            await _hubConnection.DisposeAsync();
            _hubConnection = null;
            OnConnectionChanged?.Invoke(false);
        }
    }

    // ----- Client to Server methods -----

    /// <summary>
    /// Joins a voice channel so the user can participate in voice communication.
    /// </summary>
    public async Task JoinVoiceChannelAsync(Guid channelId)
    {
        EnsureConnected();
        await _hubConnection!.InvokeAsync("JoinVoiceChannel", channelId);
    }

    /// <summary>
    /// Leaves a voice channel, stopping voice communication.
    /// </summary>
    public async Task LeaveVoiceChannelAsync(Guid channelId)
    {
        EnsureConnected();
        await _hubConnection!.InvokeAsync("LeaveVoiceChannel", channelId);
    }

    /// <summary>
    /// Sends a WebRTC offer SDP to a specific user by their connection ID.
    /// </summary>
    public async Task SendOfferAsync(string targetConnectionId, string sdp)
    {
        EnsureConnected();
        await _hubConnection!.InvokeAsync("SendOffer", targetConnectionId, sdp);
    }

    /// <summary>
    /// Sends a WebRTC answer SDP to a specific user by their connection ID.
    /// </summary>
    public async Task SendAnswerAsync(string targetConnectionId, string sdp)
    {
        EnsureConnected();
        await _hubConnection!.InvokeAsync("SendAnswer", targetConnectionId, sdp);
    }

    /// <summary>
    /// Sends an ICE candidate to a specific user by their connection ID.
    /// </summary>
    public async Task SendIceCandidateAsync(string targetConnectionId, string candidateJson)
    {
        EnsureConnected();
        await _hubConnection!.InvokeAsync("SendIceCandidate", targetConnectionId, candidateJson);
    }

    /// <summary>
    /// Toggles the mute state of the current user in a voice channel.
    /// </summary>
    public async Task ToggleMuteAsync(Guid channelId, bool isMuted)
    {
        EnsureConnected();
        await _hubConnection!.InvokeAsync("ToggleMute", channelId, isMuted);
    }

    /// <summary>
    /// Toggles the deafen state of the current user in a voice channel.
    /// </summary>
    public async Task ToggleDeafenAsync(Guid channelId, bool isDeafened)
    {
        EnsureConnected();
        await _hubConnection!.InvokeAsync("ToggleDeafen", channelId, isDeafened);
    }

    /// <summary>
    /// Requests ICE server configuration from the server. This is a request/response call.
    /// </summary>
    public async Task<IceServerConfig> GetIceServersAsync()
    {
        EnsureConnected();
        return await _hubConnection!.InvokeAsync<IceServerConfig>("GetIceServers");
    }

    // ----- IAsyncDisposable -----

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection is not null)
        {
            await _hubConnection.DisposeAsync();
            _hubConnection = null;
        }
    }

    // ----- Private helpers -----

    private void RegisterHandlers(HubConnection connection)
    {
        connection.On<Guid, VoiceUserInfo>("UserJoinedVoice", (channelId, userInfo) =>
        {
            OnUserJoinedVoice?.Invoke(channelId, userInfo);
        });

        connection.On<Guid, Guid>("UserLeftVoice", (channelId, userId) =>
        {
            OnUserLeftVoice?.Invoke(channelId, userId);
        });

        connection.On<string, string>("ReceiveOffer", (fromConnectionId, sdp) =>
        {
            OnReceiveOffer?.Invoke(fromConnectionId, sdp);
        });

        connection.On<string, string>("ReceiveAnswer", (fromConnectionId, sdp) =>
        {
            OnReceiveAnswer?.Invoke(fromConnectionId, sdp);
        });

        connection.On<string, string>("ReceiveIceCandidate", (fromConnectionId, candidateJson) =>
        {
            OnReceiveIceCandidate?.Invoke(fromConnectionId, candidateJson);
        });

        connection.On<Guid, Guid, bool>("UserMuteChanged", (channelId, userId, isMuted) =>
        {
            OnUserMuteChanged?.Invoke(channelId, userId, isMuted);
        });

        connection.On<Guid, Guid, bool>("UserDeafenChanged", (channelId, userId, isDeafened) =>
        {
            OnUserDeafenChanged?.Invoke(channelId, userId, isDeafened);
        });

        connection.On<Guid, VoiceUserInfo[]>("VoiceChannelUsers", (channelId, users) =>
        {
            OnVoiceChannelUsers?.Invoke(channelId, users);
        });
    }

    private void RegisterLifecycleEvents(HubConnection connection)
    {
        connection.Reconnecting += error =>
        {
            _logger.LogWarning(error, "VoiceHub reconnecting");
            OnConnectionChanged?.Invoke(false);
            return Task.CompletedTask;
        };

        connection.Reconnected += connectionId =>
        {
            _logger.LogInformation("VoiceHub reconnected with connection {ConnectionId}", connectionId);
            OnConnectionChanged?.Invoke(true);
            return Task.CompletedTask;
        };

        connection.Closed += error =>
        {
            if (error is not null)
            {
                _logger.LogError(error, "VoiceHub connection closed with error");
            }
            else
            {
                _logger.LogInformation("VoiceHub connection closed");
            }

            OnConnectionChanged?.Invoke(false);
            return Task.CompletedTask;
        };
    }

    private void EnsureConnected()
    {
        if (_hubConnection is null || _hubConnection.State != HubConnectionState.Connected)
        {
            throw new InvalidOperationException(
                "VoiceHub is not connected. Call StartAsync before invoking hub methods.");
        }
    }
}
