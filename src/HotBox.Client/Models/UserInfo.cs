using HotBox.Core.Enums;

namespace HotBox.Client.Models;

public class UserInfo
{
    public Guid Id { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string? AvatarUrl { get; set; }

    public UserStatus Status { get; set; } = UserStatus.Online;

    public string Role { get; set; } = "Member";
}
