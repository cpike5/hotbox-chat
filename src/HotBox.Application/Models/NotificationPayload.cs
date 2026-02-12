namespace HotBox.Application.Models;

public class NotificationPayload
{
    public string SenderDisplayName { get; set; } = string.Empty;

    public string ChannelName { get; set; } = string.Empty;

    public Guid ChannelId { get; set; }

    public string MessagePreview { get; set; } = string.Empty;
}
