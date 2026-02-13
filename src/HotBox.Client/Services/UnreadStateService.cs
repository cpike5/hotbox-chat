using Microsoft.Extensions.Logging;

namespace HotBox.Client.Services;

public class UnreadStateService : IDisposable
{
    private readonly ApiClient _api;
    private readonly ChatHubService _chatHub;
    private readonly ILogger<UnreadStateService> _logger;

    private readonly Dictionary<Guid, int> _channelUnreads = new();
    private readonly Dictionary<Guid, int> _dmUnreads = new();
    private bool _initialized;

    public event Action? OnChange;

    public UnreadStateService(ApiClient api, ChatHubService chatHub, ILogger<UnreadStateService> logger)
    {
        _api = api;
        _chatHub = chatHub;
        _logger = logger;

        _chatHub.OnUnreadCountUpdated += HandleChannelUnreadUpdate;
        _chatHub.OnDmUnreadCountUpdated += HandleDmUnreadUpdate;
    }

    public async Task InitializeAsync()
    {
        if (_initialized) return;

        var channelCounts = await _api.GetChannelUnreadCountsAsync();
        if (channelCounts != null)
        {
            foreach (var (id, count) in channelCounts)
                _channelUnreads[id] = count;
        }

        var dmCounts = await _api.GetDmUnreadCountsAsync();
        if (dmCounts != null)
        {
            foreach (var (id, count) in dmCounts)
                _dmUnreads[id] = count;
        }

        _initialized = true;
        _logger.LogDebug("Initialized unread state: {ChannelCount} channels, {DmCount} DM conversations",
            _channelUnreads.Count, _dmUnreads.Count);
        NotifyStateChanged();
    }

    public int GetChannelUnreadCount(Guid channelId)
        => _channelUnreads.GetValueOrDefault(channelId, 0);

    public int GetDmUnreadCount(Guid userId)
        => _dmUnreads.GetValueOrDefault(userId, 0);

    public async Task MarkChannelAsReadAsync(Guid channelId)
    {
        _channelUnreads[channelId] = 0;
        NotifyStateChanged();
        await _api.MarkChannelAsReadAsync(channelId);
    }

    public async Task MarkDmAsReadAsync(Guid userId)
    {
        _dmUnreads[userId] = 0;
        NotifyStateChanged();
        await _api.MarkDmAsReadAsync(userId);
    }

    private void HandleChannelUnreadUpdate(Guid channelId)
    {
        _channelUnreads[channelId] = _channelUnreads.GetValueOrDefault(channelId, 0) + 1;
        NotifyStateChanged();
    }

    private void HandleDmUnreadUpdate(Guid senderId)
    {
        _dmUnreads[senderId] = _dmUnreads.GetValueOrDefault(senderId, 0) + 1;
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnChange?.Invoke();

    public void Dispose()
    {
        _chatHub.OnUnreadCountUpdated -= HandleChannelUnreadUpdate;
        _chatHub.OnDmUnreadCountUpdated -= HandleDmUnreadUpdate;
    }
}
