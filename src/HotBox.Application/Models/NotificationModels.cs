using HotBox.Core.Enums;

namespace HotBox.Application.Models;

public record NotificationResponse(
    Guid Id,
    NotificationType Type,
    Guid SenderId,
    string PayloadJson,
    Guid SourceId,
    NotificationSourceType SourceType,
    DateTime CreatedAt,
    DateTime? ReadAt);
