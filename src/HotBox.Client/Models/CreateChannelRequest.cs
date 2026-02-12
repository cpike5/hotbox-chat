using HotBox.Core.Enums;

namespace HotBox.Client.Models;

public class CreateChannelRequest
{
    public string Name { get; set; } = string.Empty;

    public string? Topic { get; set; }

    public ChannelType Type { get; set; }
}
