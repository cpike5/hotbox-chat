namespace HotBox.Client.Models;

public class VoiceUserInfo
{
    public Guid UserId { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string ConnectionId { get; set; } = string.Empty;

    public bool IsMuted { get; set; }

    public bool IsDeafened { get; set; }
}
