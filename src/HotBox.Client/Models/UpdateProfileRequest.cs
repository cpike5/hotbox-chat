namespace HotBox.Client.Models;

public class UpdateProfileRequest
{
    public string DisplayName { get; set; } = string.Empty;

    public string? AvatarUrl { get; set; }

    public string? Bio { get; set; }

    public string? Pronouns { get; set; }

    public string? CustomStatus { get; set; }
}
