namespace HotBox.Core.Entities;

public class DirectMessage
{
    public Guid Id { get; init; }

    public string Content { get; set; } = string.Empty;

    public Guid SenderId { get; set; }

    public Guid RecipientId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? EditedAt { get; set; }

    public DateTime? ReadAt { get; set; }

    public AppUser Sender { get; set; } = null!;

    public AppUser Recipient { get; set; } = null!;
}
