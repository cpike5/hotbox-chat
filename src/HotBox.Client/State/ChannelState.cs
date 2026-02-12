using HotBox.Client.Models;

namespace HotBox.Client.State;

public class ChannelState
{
    public List<ChannelResponse> Channels { get; private set; } = new();

    public ChannelResponse? ActiveChannel { get; private set; }

    public List<MessageResponse> Messages { get; private set; } = new();

    public HashSet<string> TypingUsers { get; private set; } = new();

    public bool IsLoadingMessages { get; private set; }

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
        TypingUsers = new();
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
        Messages.InsertRange(0, olderMessages);
        NotifyStateChanged();
    }

    public void AddTypingUser(string displayName)
    {
        if (TypingUsers.Add(displayName))
        {
            NotifyStateChanged();
        }
    }

    public void RemoveTypingUser(string displayName)
    {
        if (TypingUsers.Remove(displayName))
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

    private void NotifyStateChanged() => OnChange?.Invoke();
}
