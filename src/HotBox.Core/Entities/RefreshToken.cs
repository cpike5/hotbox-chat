namespace HotBox.Core.Entities;

public class RefreshToken
{
    public Guid Id { get; init; }

    public string TokenHash { get; set; } = string.Empty;

    public Guid UserId { get; set; }

    public AppUser User { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    public string? ReplacedByToken { get; set; }

    public bool IsRevoked => RevokedAt.HasValue;

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;

    public bool IsActive => !IsRevoked && !IsExpired;
}
