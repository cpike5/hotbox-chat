using HotBox.Core.Enums;

namespace HotBox.Core.Entities;

public class UserNotificationPreference
{
    public Guid Id { get; init; }

    public Guid UserId { get; set; }

    public NotificationSourceType SourceType { get; set; }

    public Guid SourceId { get; set; }

    public bool IsMuted { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    // Navigation
    public AppUser User { get; set; } = null!;
}
