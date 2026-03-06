using System.ComponentModel.DataAnnotations;

namespace HotBox.Application.Models;

public class DemoRegisterRequest
{
    [Required]
    [MinLength(3)]
    [MaxLength(32)]
    [RegularExpression(@"^[a-zA-Z0-9_-]+$", ErrorMessage = "Username may only contain letters, numbers, underscores, and hyphens.")]
    public string Username { get; set; } = string.Empty;

    [Required]
    [MinLength(2)]
    [MaxLength(32)]
    [RegularExpression(@"^[a-zA-Z0-9 _\-'.]+$", ErrorMessage = "Display name contains invalid characters.")]
    public string DisplayName { get; set; } = string.Empty;
}
