using HotBox.Core.Enums;

namespace HotBox.Core.Entities;

public class Channel
{
    public Guid Id { get; init; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public ChannelType Type { get; set; }

    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid CreatedById { get; set; }

    public AppUser CreatedBy { get; set; } = null!;

    public ICollection<Message> Messages { get; set; } = [];
}
