namespace HotBox.Client.State;

public enum VoiceConnectionStatus
{
    Disconnected,
    Connecting,
    Connected
}

public class VoicePeerInfo
{
    public Guid UserId { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public bool IsMuted { get; set; }

    public bool IsDeafened { get; set; }
}

public class VoiceState
{
    public Guid? CurrentVoiceChannelId { get; private set; }

    public string? CurrentVoiceChannelName { get; private set; }

    public List<VoicePeerInfo> ConnectedPeers { get; private set; } = new();

    public bool IsMuted { get; private set; }

    public bool IsDeafened { get; private set; }

    public VoiceConnectionStatus ConnectionStatus { get; private set; } = VoiceConnectionStatus.Disconnected;

    public event Action? OnChange;

    public void SetConnected(Guid channelId, string channelName)
    {
        CurrentVoiceChannelId = channelId;
        CurrentVoiceChannelName = channelName;
        ConnectionStatus = VoiceConnectionStatus.Connected;
        ConnectedPeers = new();
        NotifyStateChanged();
    }

    public void SetDisconnected()
    {
        CurrentVoiceChannelId = null;
        CurrentVoiceChannelName = null;
        ConnectionStatus = VoiceConnectionStatus.Disconnected;
        ConnectedPeers = new();
        IsMuted = false;
        IsDeafened = false;
        NotifyStateChanged();
    }

    public void AddPeer(VoicePeerInfo peer)
    {
        if (ConnectedPeers.All(p => p.UserId != peer.UserId))
        {
            ConnectedPeers.Add(peer);
            NotifyStateChanged();
        }
    }

    public void RemovePeer(Guid userId)
    {
        var peer = ConnectedPeers.FirstOrDefault(p => p.UserId == userId);
        if (peer is not null)
        {
            ConnectedPeers.Remove(peer);
            NotifyStateChanged();
        }
    }

    public void SetMuted(bool muted)
    {
        IsMuted = muted;
        NotifyStateChanged();
    }

    public void SetDeafened(bool deafened)
    {
        IsDeafened = deafened;
        NotifyStateChanged();
    }

    public void SetConnectionStatus(VoiceConnectionStatus status)
    {
        ConnectionStatus = status;
        NotifyStateChanged();
    }

    public void UpdatePeerMute(Guid userId, bool isMuted)
    {
        var peer = ConnectedPeers.FirstOrDefault(p => p.UserId == userId);
        if (peer is not null)
        {
            peer.IsMuted = isMuted;
            NotifyStateChanged();
        }
    }

    public void UpdatePeerDeafen(Guid userId, bool isDeafened)
    {
        var peer = ConnectedPeers.FirstOrDefault(p => p.UserId == userId);
        if (peer is not null)
        {
            peer.IsDeafened = isDeafened;
            NotifyStateChanged();
        }
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}
