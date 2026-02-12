namespace HotBox.Client.Models;

public class AdminUserResponse
{
    public Guid Id { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime LastSeenUtc { get; set; }
}
