using HotBox.Core.Enums;
using Microsoft.AspNetCore.Identity;

namespace HotBox.Core.Entities;

public class AppUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;

    public string? AvatarUrl { get; set; }

    public UserStatus Status { get; set; }

    public DateTime LastSeenUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public ICollection<Message> Messages { get; set; } = [];

    public ICollection<DirectMessage> SentDirectMessages { get; set; } = [];

    public ICollection<DirectMessage> ReceivedDirectMessages { get; set; } = [];
}
