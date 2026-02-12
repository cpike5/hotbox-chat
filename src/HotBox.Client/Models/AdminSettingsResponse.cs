using HotBox.Core.Enums;

namespace HotBox.Client.Models;

public class AdminSettingsResponse
{
    public string ServerName { get; set; } = string.Empty;

    public RegistrationMode RegistrationMode { get; set; }
}
