using HotBox.Client.Models;

namespace HotBox.Client.State;

public record UserPresenceInfo(Guid UserId, string DisplayName, string Status);

public class PresenceState
{
    private readonly Dictionary<Guid, UserPresenceInfo> _users = new();

    public event Action? OnChange;

    /// <summary>
    /// Gets the status for a specific user. Returns "Offline" if the user is not tracked.
    /// </summary>
    public string GetStatus(Guid userId)
    {
        return _users.TryGetValue(userId, out var info) ? info.Status : "Offline";
    }

    /// <summary>
    /// Gets all users currently tracked with a status other than "Offline".
    /// </summary>
    public List<UserPresenceInfo> GetOnlineUsers()
    {
        return _users.Values
            .Where(u => u.Status != "Offline")
            .ToList();
    }

    /// <summary>
    /// Gets all tracked users regardless of status.
    /// </summary>
    public List<UserPresenceInfo> GetAllUsers()
    {
        return _users.Values.ToList();
    }

    /// <summary>
    /// Sets the initial list of online users, typically received when first connecting.
    /// </summary>
    public void SetOnlineUsers(List<OnlineUserInfoModel> users)
    {
        _users.Clear();
        foreach (var user in users)
        {
            _users[user.UserId] = new UserPresenceInfo(user.UserId, user.DisplayName, user.Status);
        }

        NotifyStateChanged();
    }

    /// <summary>
    /// Updates a single user's status. Adds the user if not already tracked.
    /// </summary>
    public void UpdateUserStatus(Guid userId, string displayName, string status)
    {
        _users[userId] = new UserPresenceInfo(userId, displayName, status);
        NotifyStateChanged();
    }

    /// <summary>
    /// Clears all presence data, typically on disconnect.
    /// </summary>
    public void Clear()
    {
        _users.Clear();
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}
