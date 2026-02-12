using System.ComponentModel.DataAnnotations;

namespace HotBox.Application.Models;

public class UpdateChannelRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Topic { get; set; }
}
