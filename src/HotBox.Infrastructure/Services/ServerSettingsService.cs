using HotBox.Core.Entities;
using HotBox.Core.Enums;
using HotBox.Core.Interfaces;
using HotBox.Core.Options;
using HotBox.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HotBox.Infrastructure.Services;

public class ServerSettingsService : IServerSettingsService
{
    private readonly HotBoxDbContext _context;
    private readonly ServerOptions _serverOptions;
    private readonly ILogger<ServerSettingsService> _logger;

    public ServerSettingsService(
        HotBoxDbContext context,
        IOptions<ServerOptions> serverOptions,
        ILogger<ServerSettingsService> logger)
    {
        _context = context;
        _serverOptions = serverOptions.Value;
        _logger = logger;
    }

    public async Task<ServerSettings> GetAsync(CancellationToken ct = default)
    {
        var settings = await _context.ServerSettings.FirstOrDefaultAsync(ct);

        if (settings is not null)
        {
            return settings;
        }

        // Fall back to appsettings.json defaults
        return new ServerSettings
        {
            Id = Guid.Empty,
            ServerName = _serverOptions.ServerName,
            RegistrationMode = _serverOptions.RegistrationMode,
        };
    }

    public async Task<ServerSettings> UpdateAsync(
        string serverName,
        RegistrationMode registrationMode,
        CancellationToken ct = default)
    {
        var settings = await _context.ServerSettings.FirstOrDefaultAsync(ct);

        if (settings is null)
        {
            settings = new ServerSettings
            {
                Id = Guid.NewGuid(),
                ServerName = serverName,
                RegistrationMode = registrationMode,
            };

            _context.ServerSettings.Add(settings);

            _logger.LogInformation(
                "Server settings created: ServerName={ServerName}, RegistrationMode={RegistrationMode}",
                serverName, registrationMode);
        }
        else
        {
            settings.ServerName = serverName;
            settings.RegistrationMode = registrationMode;

            _context.ServerSettings.Update(settings);

            _logger.LogInformation(
                "Server settings updated: ServerName={ServerName}, RegistrationMode={RegistrationMode}",
                serverName, registrationMode);
        }

        await _context.SaveChangesAsync(ct);
        return settings;
    }
}
