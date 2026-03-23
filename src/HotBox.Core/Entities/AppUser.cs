using HotBox.Core.Enums;
using Microsoft.AspNetCore.Identity;

namespace HotBox.Core.Entities;

public class AppUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;

    public string? AvatarUrl { get; set; }

    public string? Bio { get; set; }

    public string? Pronouns { get; set; }

    public string? CustomStatus { get; set; }

    public UserStatus Status { get; set; }

    public bool IsAgent { get; set; }

    public Guid? CreatedByApiKeyId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime LastSeenAt { get; set; }

    // Navigation
    public ApiKey? CreatedByApiKey { get; set; }

    public ICollection<Message> Messages { get; set; } = [];

    public ICollection<DirectMessage> SentDirectMessages { get; set; } = [];

    public ICollection<DirectMessage> ReceivedDirectMessages { get; set; } = [];
}
