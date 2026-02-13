using HotBox.Client.Models;

namespace HotBox.Client.State;

public class ChannelState
{
    public const int MessageWindowSize = 200;

    public List<ChannelResponse> Channels { get; private set; } = new();

    public ChannelResponse? ActiveChannel { get; private set; }

    public List<MessageResponse> Messages { get; private set; } = new();

    public Dictionary<Guid, string> TypingUsers { get; private set; } = new();

    public bool IsLoadingMessages { get; private set; }

    public bool IsLoadingChannels { get; private set; }

    public bool IsLoadingOlderMessages { get; private set; }

    public bool HasMoreMessages { get; private set; } = true;

    public event Action? OnChange;

    public void SetChannels(List<ChannelResponse> channels)
    {
        Channels = channels;
        NotifyStateChanged();
    }

    public void SetActiveChannel(ChannelResponse channel)
    {
        ActiveChannel = channel;
        Messages = new();
        TypingUsers = new Dictionary<Guid, string>();
        HasMoreMessages = true;
        NotifyStateChanged();
    }

    public void SetMessages(List<MessageResponse> messages)
    {
        Messages = messages;
        NotifyStateChanged();
    }

    public void AddMessage(MessageResponse message)
    {
        Messages.Add(message);
        NotifyStateChanged();
    }

    public void PrependMessages(List<MessageResponse> olderMessages)
    {
        var existingIds = Messages.Select(m => m.Id).ToHashSet();
        var newMessages = olderMessages.Where(m => !existingIds.Contains(m.Id)).ToList();
        Messages.InsertRange(0, newMessages);
        NotifyStateChanged();
    }

    public void AddTypingUser(Guid userId, string displayName)
    {
        TypingUsers[userId] = displayName;
        NotifyStateChanged();
    }

    public void RemoveTypingUser(Guid userId)
    {
        if (TypingUsers.Remove(userId))
        {
            NotifyStateChanged();
        }
    }

    public void ClearTypingUsers()
    {
        if (TypingUsers.Count > 0)
        {
            TypingUsers.Clear();
            NotifyStateChanged();
        }
    }

    public void SetLoadingMessages(bool loading)
    {
        IsLoadingMessages = loading;
        NotifyStateChanged();
    }

    public void SetLoadingChannels(bool loading)
    {
        IsLoadingChannels = loading;
        NotifyStateChanged();
    }

    public void SetLoadingOlderMessages(bool loading)
    {
        IsLoadingOlderMessages = loading;
        NotifyStateChanged();
    }

    public void ClearActiveChannel()
    {
        ActiveChannel = null;
        Messages = new();
        TypingUsers = new();
        NotifyStateChanged();
    }

    public void SetHasMoreMessages(bool value)
    {
        HasMoreMessages = value;
        NotifyStateChanged();
    }

    public void PurgeOldMessages(int maxCount)
    {
        if (Messages.Count > maxCount)
        {
            var excessCount = Messages.Count - maxCount;
            Messages.RemoveRange(0, excessCount);
            NotifyStateChanged();
        }
    }

    public void PurgeNewMessages(int maxCount)
    {
        if (Messages.Count > maxCount)
        {
            var excessCount = Messages.Count - maxCount;
            Messages.RemoveRange(Messages.Count - excessCount, excessCount);
            NotifyStateChanged();
        }
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}
