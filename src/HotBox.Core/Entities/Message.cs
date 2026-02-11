namespace HotBox.Core.Entities;

public class Message
{
    public Guid Id { get; init; }

    public string Content { get; set; } = string.Empty;

    public Guid ChannelId { get; set; }

    public Guid AuthorId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? EditedAtUtc { get; set; }

    public Channel Channel { get; set; } = null!;

    public AppUser Author { get; set; } = null!;
}
