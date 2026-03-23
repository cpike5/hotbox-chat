namespace HotBox.Core.Entities;

public class Invite
{
    public Guid Id { get; init; }

    public string Code { get; set; } = string.Empty;

    public Guid CreatedById { get; set; }

    public AppUser CreatedBy { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public int? MaxUses { get; set; }

    public int UseCount { get; set; }

    public bool IsRevoked { get; set; }
}
