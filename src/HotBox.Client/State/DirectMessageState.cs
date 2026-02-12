using HotBox.Client.Models;

namespace HotBox.Client.State;

public class DirectMessageState
{
    public List<ConversationSummaryResponse> Conversations { get; private set; } = new();

    public Guid? ActiveConversationUserId { get; private set; }

    public string? ActiveConversationDisplayName { get; private set; }

    public List<DirectMessageResponse> Messages { get; private set; } = new();

    /// <summary>Maps userId to displayName for users currently typing in the active DM conversation.</summary>
    public Dictionary<Guid, string> TypingUsers { get; private set; } = new();

    /// <summary>Display names of currently typing users, for UI consumption.</summary>
    public IReadOnlyList<string> TypingDisplayNames => TypingUsers.Values.ToList();

    public bool IsLoadingMessages { get; private set; }

    public Guid? CurrentUserId { get; private set; }

    public void SetCurrentUserId(Guid userId) { CurrentUserId = userId; }

    public event Action? OnChange;

    public void SetConversations(List<ConversationSummaryResponse> conversations)
    {
        Conversations = conversations;
        NotifyStateChanged();
    }

    public void SetActiveConversation(Guid userId, string displayName)
    {
        ActiveConversationUserId = userId;
        ActiveConversationDisplayName = displayName;
        Messages = new();
        TypingUsers = new();
        NotifyStateChanged();
    }

    public void ClearActiveConversation()
    {
        ActiveConversationUserId = null;
        ActiveConversationDisplayName = null;
        Messages = new();
        TypingUsers = new();
        NotifyStateChanged();
    }

    public void SetMessages(List<DirectMessageResponse> messages)
    {
        Messages = messages;
        NotifyStateChanged();
    }

    public void AddMessage(DirectMessageResponse message)
    {
        Messages.Add(message);

        // Remove the sender from typing users when their message arrives
        TypingUsers.Remove(message.SenderId);

        // Determine the other user in this conversation
        var otherUserId = message.SenderId == CurrentUserId ? message.RecipientId : message.SenderId;

        // Update the conversation summary and move it to the top
        var conversation = Conversations.FirstOrDefault(c => c.UserId == otherUserId);
        if (conversation is not null)
        {
            conversation.LastMessageContent = message.Content;
            conversation.LastMessageAtUtc = message.CreatedAtUtc;
            Conversations.Remove(conversation);
            Conversations.Insert(0, conversation);
        }

        NotifyStateChanged();
    }

    public void PrependMessages(List<DirectMessageResponse> olderMessages)
    {
        Messages.InsertRange(0, olderMessages);
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

    private void NotifyStateChanged() => OnChange?.Invoke();
}
