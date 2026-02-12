namespace HotBox.Client.State;

public class AppState
{
    public AppState(AuthState auth, ChannelState channel, DirectMessageState directMessage, PresenceState presence, VoiceState voice)
    {
        Auth = auth;
        Channel = channel;
        DirectMessage = directMessage;
        Presence = presence;
        Voice = voice;

        Auth.OnChange += NotifyStateChanged;
        Channel.OnChange += NotifyStateChanged;
        DirectMessage.OnChange += NotifyStateChanged;
        Presence.OnChange += NotifyStateChanged;
        Voice.OnChange += NotifyStateChanged;
    }

    public AuthState Auth { get; }

    public ChannelState Channel { get; }

    public DirectMessageState DirectMessage { get; }

    public PresenceState Presence { get; }

    public VoiceState Voice { get; }

    public bool IsConnected { get; private set; }

    public event Action? OnChange;

    public void SetConnected(bool connected)
    {
        IsConnected = connected;
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}
