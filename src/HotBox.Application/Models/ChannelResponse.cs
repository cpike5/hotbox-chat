using HotBox.Core.Enums;

namespace HotBox.Application.Models;

public class ChannelResponse
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Topic { get; set; }

    public ChannelType Type { get; set; }

    public int SortOrder { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
