using System.ComponentModel.DataAnnotations;

namespace HotBox.Application.Models;

public class RegisterRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MinLength(8)]
    public string Password { get; set; } = string.Empty;

    [Required]
    [MaxLength(32)]
    public string DisplayName { get; set; } = string.Empty;

    public string? InviteCode { get; set; }
}
