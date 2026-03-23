namespace HotBox.Core.Entities;

public class Message
{
    public Guid Id { get; init; }

    public string Content { get; set; } = string.Empty;

    public Guid ChannelId { get; set; }

    public Guid UserId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? EditedAt { get; set; }

    public Channel Channel { get; set; } = null!;

    public AppUser User { get; set; } = null!;
}
