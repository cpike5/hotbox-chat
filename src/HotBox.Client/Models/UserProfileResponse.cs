namespace HotBox.Client.Models;

public class UserProfileResponse
{
    public Guid Id { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string? AvatarUrl { get; set; }

    public string? Bio { get; set; }

    public string? Pronouns { get; set; }

    public string? CustomStatus { get; set; }

    public string Status { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime LastSeenUtc { get; set; }

    public bool IsAgent { get; set; }
}
