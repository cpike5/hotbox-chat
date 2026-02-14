using HotBox.Core.Enums;

namespace HotBox.Client.Models;

public class NotificationResponseModel
{
    public Guid Id { get; set; }
    public NotificationType Type { get; set; }
    public Guid SenderId { get; set; }
    public string SenderDisplayName { get; set; } = string.Empty;
    public string MessagePreview { get; set; } = string.Empty;
    public Guid SourceId { get; set; }
    public NotificationSourceType SourceType { get; set; }
    public string SourceName { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ReadAtUtc { get; set; }
}
