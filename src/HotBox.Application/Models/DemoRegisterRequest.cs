using System.ComponentModel.DataAnnotations;

namespace HotBox.Application.Models;

public class DemoRegisterRequest
{
    [Required]
    [MaxLength(32)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [MaxLength(32)]
    public string DisplayName { get; set; } = string.Empty;
}
