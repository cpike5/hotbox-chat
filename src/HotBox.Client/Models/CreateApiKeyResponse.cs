namespace HotBox.Client.Models;

public class CreateApiKeyResponse
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Key { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }
}
