namespace HotBox.Client.Models;

public class AdminUserDetailResponse
{
    public Guid Id { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime LastSeenUtc { get; set; }

    public bool IsAgent { get; set; }

    public string? AvatarUrl { get; set; }

    public string? Bio { get; set; }

    public string? Pronouns { get; set; }

    public string? CustomStatus { get; set; }

    public bool EmailConfirmed { get; set; }

    public DateTimeOffset? LockoutEnd { get; set; }

    public bool LockoutEnabled { get; set; }
}
