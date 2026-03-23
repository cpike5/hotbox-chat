using System.ComponentModel.DataAnnotations;

namespace HotBox.Application.Models;

public record DirectMessageResponse(
    Guid Id,
    string Content,
    Guid SenderId,
    string SenderDisplayName,
    string? SenderAvatarUrl,
    Guid RecipientId,
    string RecipientDisplayName,
    DateTime CreatedAt,
    DateTime? EditedAt,
    DateTime? ReadAt);

public record SendDirectMessageRequest(
    [Required] Guid RecipientId,
    [Required] string Content);

public record ConversationSummaryResponse(
    Guid UserId,
    string DisplayName,
    DateTime LastMessageAt,
    string LastMessageContent,
    int UnreadCount);
