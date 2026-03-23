using HotBox.Core.Enums;

namespace HotBox.Core.Entities;

public class UserNotificationPreference
{
    public Guid Id { get; init; }

    public Guid UserId { get; set; }

    public NotificationSourceType SourceType { get; set; }

    public Guid SourceId { get; set; }

    public bool IsMuted { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public AppUser User { get; set; } = null!;
}
