using HotBox.Core.Enums;

namespace HotBox.Core.Entities;

public class ServerSettings
{
    public Guid Id { get; init; }

    public string ServerName { get; set; } = "HotBox";

    public RegistrationMode RegistrationMode { get; set; } = RegistrationMode.InviteOnly;
}
