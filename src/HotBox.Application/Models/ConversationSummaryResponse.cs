namespace HotBox.Application.Models;

public class ConversationSummaryResponse
{
    public Guid UserId { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string LastMessageContent { get; set; } = string.Empty;

    public DateTime LastMessageAtUtc { get; set; }

    public int UnreadCount { get; set; }
}
