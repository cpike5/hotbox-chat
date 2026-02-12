namespace HotBox.Client.State;

public class AppState
{
    public AppState(AuthState auth, ChannelState channel, DirectMessageState directMessage)
    {
        Auth = auth;
        Channel = channel;
        DirectMessage = directMessage;

        Auth.OnChange += NotifyStateChanged;
        Channel.OnChange += NotifyStateChanged;
        DirectMessage.OnChange += NotifyStateChanged;
    }

    public AuthState Auth { get; }

    public ChannelState Channel { get; }

    public DirectMessageState DirectMessage { get; }

    public bool IsConnected { get; private set; }

    public event Action? OnChange;

    public void SetConnected(bool connected)
    {
        IsConnected = connected;
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}
