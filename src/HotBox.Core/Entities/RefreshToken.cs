namespace HotBox.Core.Entities;

public class RefreshToken
{
    public Guid Id { get; init; }

    public string Token { get; set; } = string.Empty;

    public Guid UserId { get; set; }

    public AppUser User { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime ExpiresAtUtc { get; set; }

    public DateTime? RevokedAtUtc { get; set; }

    public string? ReplacedByToken { get; set; }

    public bool IsRevoked => RevokedAtUtc.HasValue;

    public bool IsExpired => DateTime.UtcNow >= ExpiresAtUtc;

    public bool IsActive => !IsRevoked && !IsExpired;
}
