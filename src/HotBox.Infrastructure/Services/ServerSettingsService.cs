using HotBox.Core.Entities;
using HotBox.Core.Enums;
using HotBox.Core.Interfaces;
using HotBox.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HotBox.Infrastructure.Services;

public class ServerSettingsService : IServerSettingsService
{
    private readonly HotBoxDbContext _dbContext;
    private readonly ILogger<ServerSettingsService> _logger;

    public ServerSettingsService(HotBoxDbContext dbContext, ILogger<ServerSettingsService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<ServerSettings> GetAsync(CancellationToken ct = default)
    {
        var settings = await _dbContext.ServerSettings.FirstOrDefaultAsync(ct);

        if (settings is null)
        {
            _logger.LogWarning("No server settings found, returning defaults");
            return new ServerSettings
            {
                Id = Guid.NewGuid(),
                ServerName = "HotBox",
                RegistrationMode = RegistrationMode.InviteOnly
            };
        }

        return settings;
    }

    public async Task<ServerSettings> UpdateAsync(
        string serverName,
        RegistrationMode registrationMode,
        CancellationToken ct = default)
    {
        var settings = await _dbContext.ServerSettings.FirstOrDefaultAsync(ct);

        if (settings is null)
        {
            settings = new ServerSettings
            {
                Id = Guid.NewGuid(),
                ServerName = serverName,
                RegistrationMode = registrationMode
            };

            _dbContext.ServerSettings.Add(settings);
        }
        else
        {
            settings.ServerName = serverName;
            settings.RegistrationMode = registrationMode;
        }

        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Server settings updated: ServerName={ServerName}, RegistrationMode={RegistrationMode}",
            serverName, registrationMode);

        return settings;
    }
}
