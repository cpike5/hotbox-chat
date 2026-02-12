namespace HotBox.Client.Models;

public class IceServerConfig
{
    public string[] StunUrls { get; set; } = [];

    public string TurnUrl { get; set; } = string.Empty;

    public string TurnUsername { get; set; } = string.Empty;

    public string TurnCredential { get; set; } = string.Empty;
}
