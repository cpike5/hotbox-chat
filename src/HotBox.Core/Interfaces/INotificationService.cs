using HotBox.Core.Enums;

namespace HotBox.Core.Interfaces;

public interface INotificationService
{
    Task CreateAsync(
        NotificationType type,
        Guid senderId,
        Guid recipientId,
        string senderDisplayName,
        string messagePreview,
        Guid sourceId,
        NotificationSourceType sourceType,
        string sourceName,
        CancellationToken ct = default);

    Task ProcessMentionNotificationsAsync(
        Guid senderId,
        string senderDisplayName,
        Guid channelId,
        string channelName,
        string messageContent,
        CancellationToken ct = default);
}
