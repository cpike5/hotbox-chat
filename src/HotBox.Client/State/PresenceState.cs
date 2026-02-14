using HotBox.Client.Models;

namespace HotBox.Client.State;

public record UserPresenceInfo(Guid UserId, string DisplayName, string Status, bool IsAgent);

public class PresenceState
{
    private readonly Dictionary<Guid, UserPresenceInfo> _users = new();

    public bool IsLoading { get; private set; }

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
    /// Sets the loading state for the members panel.
    /// </summary>
    public void SetLoading(bool loading)
    {
        IsLoading = loading;
        NotifyStateChanged();
    }

    /// <summary>
    /// Seeds all registered users as "Offline". Called before SetOnlineUsers so that
    /// users who have never connected still appear in the members panel.
    /// Does not overwrite existing entries.
    /// </summary>
    public void SeedAllUsers(List<UserProfileResponse> users)
    {
        foreach (var user in users)
        {
            if (!_users.ContainsKey(user.Id))
            {
                _users[user.Id] = new UserPresenceInfo(user.Id, user.DisplayName, "Offline", user.IsAgent);
            }
        }

        NotifyStateChanged();
    }

    /// <summary>
    /// Sets the initial list of online users, typically received when first connecting.
    /// Updates existing entries rather than clearing, so seeded offline users are preserved.
    /// </summary>
    public void SetOnlineUsers(List<OnlineUserInfoModel> users)
    {
        // Mark all existing users as Offline first
        foreach (var userId in _users.Keys.ToList())
        {
            _users[userId] = _users[userId] with { Status = "Offline" };
        }

        // Now overlay the actual online statuses
        foreach (var user in users)
        {
            _users[user.UserId] = new UserPresenceInfo(user.UserId, user.DisplayName, user.Status, user.IsAgent);
        }

        IsLoading = false;
        NotifyStateChanged();
    }

    /// <summary>
    /// Updates a single user's status. Adds the user if not already tracked.
    /// </summary>
    public void UpdateUserStatus(Guid userId, string displayName, string status, bool isAgent = false)
    {
        _users[userId] = new UserPresenceInfo(userId, displayName, status, isAgent);
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
