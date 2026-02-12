using HotBox.Client.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

namespace HotBox.Client.Services;

public class ChatHubService : IAsyncDisposable
{
    private readonly string _baseUrl;
    private readonly ILogger<ChatHubService> _logger;
    private HubConnection? _hubConnection;

    public ChatHubService(NavigationManager navigation, ILogger<ChatHubService> logger)
    {
        _baseUrl = navigation.BaseUri.TrimEnd('/');
        _logger = logger;
    }

    /// <summary>
    /// Indicates whether the hub connection is currently in the Connected state.
    /// </summary>
    public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;

    // ----- Events for UI components to subscribe to -----

    /// <summary>Raised when a new message is received from any channel the user has joined.</summary>
    public event Action<MessageResponse>? OnMessageReceived;

    /// <summary>Raised when another user starts typing in a channel.</summary>
    public event Action<Guid, Guid, string>? OnUserTyping;

    /// <summary>Raised when another user stops typing in a channel.</summary>
    public event Action<Guid, Guid>? OnUserStoppedTyping;

    /// <summary>Raised when a user joins a channel.</summary>
    public event Action<Guid, Guid, string>? OnUserJoinedChannel;

    /// <summary>Raised when a user leaves a channel.</summary>
    public event Action<Guid, Guid>? OnUserLeftChannel;

    /// <summary>Raised when the connection state changes. True means connected, false means disconnected or reconnecting.</summary>
    public event Action<bool>? OnConnectionChanged;

    /// <summary>Raised when a direct message is received.</summary>
    public event Action<DirectMessageResponse>? OnDirectMessageReceived;

    /// <summary>Raised when another user starts typing in a DM conversation.</summary>
    public event Action<Guid, string>? OnDirectMessageTyping;

    /// <summary>Raised when another user stops typing in a DM conversation.</summary>
    public event Action<Guid>? OnDirectMessageStoppedTyping;

    /// <summary>
    /// Raised when a user's presence status changes. Parameters: userId, displayName, status.
    /// The status parameter is a stringified <c>UserStatus</c> enum value
    /// (e.g. "Online", "Idle", "DoNotDisturb", "Offline") because SignalR serializes
    /// enums as strings in JSON by default.
    /// </summary>
    public event Action<Guid, string, string>? OnUserStatusChanged;

    /// <summary>Raised when the initial list of online users is received after connecting.</summary>
    public event Action<List<OnlineUserInfoModel>>? OnOnlineUsers;

    /// <summary>Raised when a notification payload is received from the server.</summary>
    public event Action<NotificationPayloadModel>? OnNotificationReceived;

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
            .WithUrl($"{_baseUrl}/hubs/chat", options =>
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
            _logger.LogInformation("ChatHub connection started");
            OnConnectionChanged?.Invoke(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start ChatHub connection");
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
                _logger.LogInformation("ChatHub connection stopped");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping ChatHub connection");
            }

            await _hubConnection.DisposeAsync();
            _hubConnection = null;
            OnConnectionChanged?.Invoke(false);
        }
    }

    // ----- Client to Server methods -----

    /// <summary>
    /// Joins a channel so the user receives messages and presence events for it.
    /// </summary>
    public async Task JoinChannelAsync(Guid channelId)
    {
        EnsureConnected();
        await _hubConnection!.InvokeAsync("JoinChannel", channelId);
    }

    /// <summary>
    /// Leaves a channel, stopping message and presence events for it.
    /// </summary>
    public async Task LeaveChannelAsync(Guid channelId)
    {
        EnsureConnected();
        await _hubConnection!.InvokeAsync("LeaveChannel", channelId);
    }

    /// <summary>
    /// Sends a text message to a channel.
    /// </summary>
    public async Task SendMessageAsync(Guid channelId, string content)
    {
        EnsureConnected();
        await _hubConnection!.InvokeAsync("SendMessage", channelId, content);
    }

    /// <summary>
    /// Notifies the channel that the current user has started typing.
    /// </summary>
    public async Task StartTypingAsync(Guid channelId)
    {
        EnsureConnected();
        await _hubConnection!.InvokeAsync("StartTyping", channelId);
    }

    /// <summary>
    /// Notifies the channel that the current user has stopped typing.
    /// </summary>
    public async Task StopTypingAsync(Guid channelId)
    {
        EnsureConnected();
        await _hubConnection!.InvokeAsync("StopTyping", channelId);
    }

    /// <summary>
    /// Sends a direct message to a specific user.
    /// </summary>
    public async Task SendDirectMessageAsync(Guid recipientId, string content)
    {
        EnsureConnected();
        await _hubConnection!.InvokeAsync("SendDirectMessage", recipientId, content);
    }

    /// <summary>
    /// Notifies the recipient that the current user has started typing in a DM.
    /// </summary>
    public async Task DirectMessageTypingAsync(Guid recipientId)
    {
        EnsureConnected();
        await _hubConnection!.InvokeAsync("DirectMessageTyping", recipientId);
    }

    /// <summary>
    /// Notifies the recipient that the current user has stopped typing in a DM.
    /// </summary>
    public async Task DirectMessageStoppedTypingAsync(Guid recipientId)
    {
        EnsureConnected();
        await _hubConnection!.InvokeAsync("DirectMessageStoppedTyping", recipientId);
    }

    /// <summary>
    /// Sends a heartbeat to the server to indicate the user is still active.
    /// </summary>
    public async Task SendHeartbeatAsync()
    {
        EnsureConnected();
        await _hubConnection!.InvokeAsync("Heartbeat");
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
        connection.On<MessageResponse>("ReceiveMessage", message =>
        {
            OnMessageReceived?.Invoke(message);
        });

        connection.On<Guid, Guid, string>("UserTyping", (channelId, userId, displayName) =>
        {
            OnUserTyping?.Invoke(channelId, userId, displayName);
        });

        connection.On<Guid, Guid>("UserStoppedTyping", (channelId, userId) =>
        {
            OnUserStoppedTyping?.Invoke(channelId, userId);
        });

        connection.On<Guid, Guid, string>("UserJoinedChannel", (channelId, userId, displayName) =>
        {
            OnUserJoinedChannel?.Invoke(channelId, userId, displayName);
        });

        connection.On<Guid, Guid>("UserLeftChannel", (channelId, userId) =>
        {
            OnUserLeftChannel?.Invoke(channelId, userId);
        });

        connection.On<DirectMessageResponse>("ReceiveDirectMessage", message =>
        {
            OnDirectMessageReceived?.Invoke(message);
        });

        connection.On<Guid, string>("DirectMessageTyping", (senderId, displayName) =>
        {
            OnDirectMessageTyping?.Invoke(senderId, displayName);
        });

        connection.On<Guid>("DirectMessageStoppedTyping", senderId =>
        {
            OnDirectMessageStoppedTyping?.Invoke(senderId);
        });

        connection.On<Guid, string, string>("UserStatusChanged", (userId, displayName, status) =>
        {
            OnUserStatusChanged?.Invoke(userId, displayName, status);
        });

        connection.On<List<OnlineUserInfoModel>>("OnlineUsers", users =>
        {
            OnOnlineUsers?.Invoke(users);
        });

        connection.On<NotificationPayloadModel>("ReceiveNotification", payload =>
        {
            OnNotificationReceived?.Invoke(payload);
        });
    }

    private void RegisterLifecycleEvents(HubConnection connection)
    {
        connection.Reconnecting += error =>
        {
            _logger.LogWarning(error, "ChatHub reconnecting");
            OnConnectionChanged?.Invoke(false);
            return Task.CompletedTask;
        };

        connection.Reconnected += connectionId =>
        {
            _logger.LogInformation("ChatHub reconnected with connection {ConnectionId}", connectionId);
            OnConnectionChanged?.Invoke(true);
            return Task.CompletedTask;
        };

        connection.Closed += error =>
        {
            if (error is not null)
            {
                _logger.LogError(error, "ChatHub connection closed with error");
            }
            else
            {
                _logger.LogInformation("ChatHub connection closed");
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
                "ChatHub is not connected. Call StartAsync before invoking hub methods.");
        }
    }
}
