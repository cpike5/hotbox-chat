using System.ComponentModel.DataAnnotations;

namespace HotBox.Application.Models;

public class UpdateProfileRequest
{
    [Required]
    [MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    [MaxLength(500)]
    [Url]
    public string? AvatarUrl { get; set; }

    [MaxLength(256)]
    public string? Bio { get; set; }

    [MaxLength(50)]
    public string? Pronouns { get; set; }

    [MaxLength(128)]
    public string? CustomStatus { get; set; }
}
