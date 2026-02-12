using System.ComponentModel.DataAnnotations;
using HotBox.Core.Enums;

namespace HotBox.Application.Models;

public class CreateChannelRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Topic { get; set; }

    [Required]
    public ChannelType Type { get; set; }
}
