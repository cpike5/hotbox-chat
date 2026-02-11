namespace HotBox.Core.Models;

public record ConversationSummary(Guid UserId, string DisplayName, DateTime LastMessageAtUtc);
