using System.ComponentModel.DataAnnotations;

namespace HotBox.Application.Models;

public record MessageResponse(
    Guid Id,
    string Content,
    Guid ChannelId,
    Guid UserId,
    string UserDisplayName,
    string? UserAvatarUrl,
    bool IsAgent,
    DateTime CreatedAt,
    DateTime? EditedAt);

public record SendMessageRequest(
    [Required] string Content);
