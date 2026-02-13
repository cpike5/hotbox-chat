namespace HotBox.Client.Models;

public class AdminApiKeyResponse
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string MaskedKey { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? RevokedAtUtc { get; set; }

    public string? RevokedReason { get; set; }

    public bool IsActive { get; set; }
}
