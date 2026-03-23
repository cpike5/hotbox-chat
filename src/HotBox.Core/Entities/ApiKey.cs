namespace HotBox.Core.Entities;

public class ApiKey
{
    public Guid Id { get; init; }

    public string KeyHash { get; set; } = string.Empty;

    public string KeyPrefix { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public Guid UserId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    public bool IsRevoked => RevokedAt.HasValue;

    public bool IsActive => !IsRevoked;

    public ICollection<AppUser> CreatedAgents { get; set; } = [];
}
