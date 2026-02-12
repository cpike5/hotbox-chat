using HotBox.Core.Interfaces;
using HotBox.Core.Options;
using HotBox.Infrastructure.Data;
using HotBox.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HotBox.Infrastructure.DependencyInjection;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind options that Infrastructure owns
        services.Configure<DatabaseOptions>(opts =>
            configuration.GetSection(DatabaseOptions.SectionName).Bind(opts));
        services.Configure<ServerOptions>(opts =>
            configuration.GetSection(ServerOptions.SectionName).Bind(opts));
        services.Configure<ObservabilityOptions>(opts =>
            configuration.GetSection(ObservabilityOptions.SectionName).Bind(opts));

        // Read database options for provider configuration
        var dbOptions = configuration
            .GetSection(DatabaseOptions.SectionName)
            .Get<DatabaseOptions>()
            ?? throw new InvalidOperationException($"Missing {DatabaseOptions.SectionName} configuration section");

        if (string.IsNullOrWhiteSpace(dbOptions.ConnectionString))
        {
            throw new InvalidOperationException("Database:ConnectionString is required");
        }

        services.AddDbContext<HotBoxDbContext>(options =>
        {
            switch (dbOptions.Provider.ToLowerInvariant())
            {
                case "sqlite":
                    options.UseSqlite(dbOptions.ConnectionString);
                    break;
                case "postgresql":
                case "postgres":
                    options.UseNpgsql(dbOptions.ConnectionString);
                    break;
                case "mysql":
                case "mariadb":
                    options.UseMySql(
                        dbOptions.ConnectionString,
                        ServerVersion.AutoDetect(dbOptions.ConnectionString));
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Unsupported database provider: {dbOptions.Provider}");
            }
        });

        // Register repositories
        services.AddScoped<IChannelRepository, ChannelRepository>();
        services.AddScoped<IMessageRepository, MessageRepository>();
        services.AddScoped<IDirectMessageRepository, DirectMessageRepository>();
        services.AddScoped<IInviteRepository, InviteRepository>();

        return services;
    }
}
