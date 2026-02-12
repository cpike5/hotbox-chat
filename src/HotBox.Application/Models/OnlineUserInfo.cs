using HotBox.Core.Enums;

namespace HotBox.Application.Models;

public class OnlineUserInfo
{
    public Guid UserId { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public UserStatus Status { get; set; }
}
