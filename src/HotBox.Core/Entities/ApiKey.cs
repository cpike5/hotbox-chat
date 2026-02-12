namespace HotBox.Core.Entities;

public class ApiKey
{
    public Guid Id { get; init; }

    public string KeyValue { get; set; } = string.Empty;

    public string KeyPrefix { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? RevokedAtUtc { get; set; }

    public string? RevokedReason { get; set; }

    public bool IsRevoked => RevokedAtUtc.HasValue;

    public bool IsActive => !IsRevoked;

    public ICollection<AppUser> CreatedAgents { get; set; } = [];
}
