using HotBox.Core.Entities;
using HotBox.Core.Enums;

namespace HotBox.Core.Interfaces;

public interface INotificationRepository
{
    Task<Notification> CreateAsync(Notification notification, CancellationToken ct = default);
    Task<List<Notification>> GetByRecipientAsync(Guid recipientId, DateTime? before = null, int limit = 50, CancellationToken ct = default);
    Task<int> GetUnreadCountAsync(Guid recipientId, CancellationToken ct = default);
    Task MarkAllAsReadAsync(Guid recipientId, CancellationToken ct = default);
    Task<bool> IsSourceMutedAsync(Guid userId, NotificationSourceType sourceType, Guid sourceId, CancellationToken ct = default);
    Task<List<UserNotificationPreference>> GetPreferencesAsync(Guid userId, CancellationToken ct = default);
    Task SetMutePreferenceAsync(Guid userId, NotificationSourceType sourceType, Guid sourceId, bool isMuted, CancellationToken ct = default);
}
