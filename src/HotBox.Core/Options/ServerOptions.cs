using HotBox.Core.Enums;

namespace HotBox.Core.Options;

public class ServerOptions
{
    public const string SectionName = "Server";

    public string ServerName { get; set; } = "HotBox";

    public int Port { get; set; } = 5000;

    public RegistrationMode RegistrationMode { get; set; } = RegistrationMode.Open;
}
