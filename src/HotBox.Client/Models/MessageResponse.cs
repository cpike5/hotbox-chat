namespace HotBox.Client.Models;

public class MessageResponse
{
    public Guid Id { get; set; }

    public string Content { get; set; } = string.Empty;

    public Guid ChannelId { get; set; }

    public Guid AuthorId { get; set; }

    public string AuthorDisplayName { get; set; } = string.Empty;

    public bool IsAgent { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? EditedAtUtc { get; set; }
}
