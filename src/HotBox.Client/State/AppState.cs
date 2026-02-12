namespace HotBox.Client.State;

public class AppState
{
    public AppState(AuthState auth, ChannelState channel)
    {
        Auth = auth;
        Channel = channel;

        Auth.OnChange += NotifyStateChanged;
        Channel.OnChange += NotifyStateChanged;
    }

    public AuthState Auth { get; }

    public ChannelState Channel { get; }

    public bool IsConnected { get; private set; }

    public event Action? OnChange;

    public void SetConnected(bool connected)
    {
        IsConnected = connected;
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}
