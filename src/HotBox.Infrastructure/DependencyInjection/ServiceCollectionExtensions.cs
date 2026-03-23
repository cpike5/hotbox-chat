using FluentValidation;
using HotBox.Core.Entities;
using HotBox.Core.Interfaces;
using HotBox.Core.Options;
using HotBox.Infrastructure.Data;
using HotBox.Infrastructure.Data.Seeding;
using HotBox.Infrastructure.Repositories;
using HotBox.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HotBox.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Database
        services.AddDbContext<HotBoxDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Postgres")));

        // Identity
        services.AddIdentityCore<AppUser>(options =>
            {
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredLength = 8;
                options.User.RequireUniqueEmail = true;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<HotBoxDbContext>()
            .AddDefaultTokenProviders();

        // Options
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<AdminSeedOptions>(configuration.GetSection(AdminSeedOptions.SectionName));
        services.Configure<PresenceOptions>(configuration.GetSection(PresenceOptions.SectionName));
        services.Configure<SearchOptions>(configuration.GetSection(SearchOptions.SectionName));
        services.Configure<ServerOptions>(configuration.GetSection(ServerOptions.SectionName));
        services.Configure<OAuthOptions>(configuration.GetSection(OAuthOptions.SectionName));
        services.Configure<IceServerOptions>(configuration.GetSection(IceServerOptions.SectionName));
        services.Configure<ObservabilityOptions>(configuration.GetSection(ObservabilityOptions.SectionName));

        // Repositories
        services.AddScoped<IChannelRepository, ChannelRepository>();
        services.AddScoped<IMessageRepository, MessageRepository>();
        services.AddScoped<IDirectMessageRepository, DirectMessageRepository>();
        services.AddScoped<IInviteRepository, InviteRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();

        // Services
        services.AddScoped<IChannelService, ChannelService>();
        services.AddScoped<IMessageService, MessageService>();
        services.AddScoped<IDirectMessageService, DirectMessageService>();
        services.AddScoped<IInviteService, InviteService>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<ISearchService, SearchService>();
        services.AddScoped<IServerSettingsService, ServerSettingsService>();
        services.AddScoped<IReadStateService, ReadStateService>();
        services.AddScoped<INotificationService, NotificationService>();

        // Presence is singleton (in-memory state)
        services.AddSingleton<PresenceService>();
        services.AddSingleton<IPresenceService>(sp => sp.GetRequiredService<PresenceService>());

        // Database seeding
        services.AddHostedService<DatabaseSeeder>();

        // FluentValidation
        services.AddValidatorsFromAssembly(typeof(ServiceCollectionExtensions).Assembly);

        return services;
    }
}
