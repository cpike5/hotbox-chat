namespace HotBox.Core.Options;

public class IceServerOptions
{
    public const string SectionName = "IceServers";

    public string[] StunUrls { get; set; } = ["stun:stun.l.google.com:19302"];

    public string TurnUrl { get; set; } = string.Empty;

    public string TurnUsername { get; set; } = string.Empty;

    public string TurnCredential { get; set; } = string.Empty;
}
