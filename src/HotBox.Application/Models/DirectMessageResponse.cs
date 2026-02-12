namespace HotBox.Application.Models;

public class DirectMessageResponse
{
    public Guid Id { get; set; }

    public string Content { get; set; } = string.Empty;

    public Guid SenderId { get; set; }

    public string SenderDisplayName { get; set; } = string.Empty;

    public Guid RecipientId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? ReadAtUtc { get; set; }
}
