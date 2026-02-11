namespace HotBox.Core.Entities;

public class Invite
{
    public Guid Id { get; init; }

    public string Code { get; set; } = string.Empty;

    public Guid CreatedByUserId { get; set; }

    public AppUser CreatedBy { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? ExpiresAtUtc { get; set; }

    public int? MaxUses { get; set; }

    public int UseCount { get; set; }

    public bool IsRevoked { get; set; }
}
