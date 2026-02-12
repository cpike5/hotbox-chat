namespace HotBox.Client.Models;

public class IceServerInfo
{
    public string[] Urls { get; set; } = [];

    public string? Username { get; set; }

    public string? Credential { get; set; }
}
