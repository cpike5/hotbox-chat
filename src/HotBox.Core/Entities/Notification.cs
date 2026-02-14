using HotBox.Core.Enums;

namespace HotBox.Core.Entities;

public class Notification
{
    public Guid Id { get; init; }

    public NotificationType Type { get; set; }

    public Guid RecipientId { get; set; }

    public Guid SenderId { get; set; }

    public string PayloadJson { get; set; } = string.Empty;

    public Guid SourceId { get; set; }

    public NotificationSourceType SourceType { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? ReadAtUtc { get; set; }

    // Navigation properties
    public AppUser Recipient { get; set; } = null!;

    public AppUser Sender { get; set; } = null!;
}
