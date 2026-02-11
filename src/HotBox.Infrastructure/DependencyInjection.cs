using HotBox.Infrastructure.Data;
using HotBox.Infrastructure.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HotBox.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
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

        return services;
    }
}
