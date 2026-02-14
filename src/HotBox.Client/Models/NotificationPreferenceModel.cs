using HotBox.Core.Enums;

namespace HotBox.Client.Models;

public class NotificationPreferenceModel
{
    public NotificationSourceType SourceType { get; set; }
    public Guid SourceId { get; set; }
    public bool IsMuted { get; set; }
}
