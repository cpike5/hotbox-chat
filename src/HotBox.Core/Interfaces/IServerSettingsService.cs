using HotBox.Core.Entities;
using HotBox.Core.Enums;

namespace HotBox.Core.Interfaces;

public interface IServerSettingsService
{
    Task<ServerSettings> GetAsync(CancellationToken ct = default);

    Task<ServerSettings> UpdateAsync(string serverName, RegistrationMode registrationMode, CancellationToken ct = default);
}
