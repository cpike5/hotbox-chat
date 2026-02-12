using HotBox.Client.Models;

namespace HotBox.Client.State;

public class AuthState
{
    public bool IsAuthenticated => CurrentUser is not null && !string.IsNullOrEmpty(AccessToken);

    public UserInfo? CurrentUser { get; private set; }

    public string? AccessToken { get; private set; }

    public event Action? OnChange;

    public void SetAuthenticated(string accessToken, UserInfo user)
    {
        AccessToken = accessToken;
        CurrentUser = user;
        NotifyStateChanged();
    }

    public void SetLoggedOut()
    {
        AccessToken = null;
        CurrentUser = null;
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}
