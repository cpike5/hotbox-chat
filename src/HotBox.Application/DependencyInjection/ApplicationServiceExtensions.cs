using System.Text;
using HotBox.Application.Services;
using HotBox.Core.Interfaces;
using HotBox.Core.Options;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace HotBox.Application.DependencyInjection;

public static class ApplicationServiceExtensions
{
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind options that Application owns
        services.Configure<ServerOptions>(configuration.GetSection(ServerOptions.SectionName));
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<OAuthOptions>(configuration.GetSection(OAuthOptions.SectionName));
        services.Configure<IceServerOptions>(configuration.GetSection(IceServerOptions.SectionName));
        services.Configure<AdminSeedOptions>(configuration.GetSection(AdminSeedOptions.SectionName));

        // JWT Bearer Authentication
        var jwtOptions = configuration
            .GetSection(JwtOptions.SectionName)
            .Get<JwtOptions>()
            ?? new JwtOptions();

        if (string.IsNullOrWhiteSpace(jwtOptions.Secret) || jwtOptions.Secret.Length < 32)
        {
            throw new InvalidOperationException(
                "Jwt:Secret must be configured and at least 32 characters for HMAC-SHA256");
        }

        var authBuilder = services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtOptions.Issuer,
                ValidateAudience = true,
                ValidAudience = jwtOptions.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwtOptions.Secret)),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30),
            };
        });

        // Conditionally add OAuth providers
        var oauthOptions = configuration
            .GetSection(OAuthOptions.SectionName)
            .Get<OAuthOptions>()
            ?? new OAuthOptions();

        if (oauthOptions.Google.Enabled
            && !string.IsNullOrWhiteSpace(oauthOptions.Google.ClientId)
            && !string.IsNullOrWhiteSpace(oauthOptions.Google.ClientSecret))
        {
            authBuilder.AddGoogle(options =>
            {
                options.ClientId = oauthOptions.Google.ClientId;
                options.ClientSecret = oauthOptions.Google.ClientSecret;
            });
        }

        if (oauthOptions.Microsoft.Enabled
            && !string.IsNullOrWhiteSpace(oauthOptions.Microsoft.ClientId)
            && !string.IsNullOrWhiteSpace(oauthOptions.Microsoft.ClientSecret))
        {
            authBuilder.AddMicrosoftAccount(options =>
            {
                options.ClientId = oauthOptions.Microsoft.ClientId;
                options.ClientSecret = oauthOptions.Microsoft.ClientSecret;
            });
        }

        services.AddAuthorization();

        // API controllers
        services.AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(
                    new System.Text.Json.Serialization.JsonStringEnumConverter());
            });

        // SignalR for real-time hubs
        services.AddSignalR()
            .AddJsonProtocol(options =>
            {
                options.PayloadSerializerOptions.Converters.Add(
                    new System.Text.Json.Serialization.JsonStringEnumConverter());
            });

        // CORS — permissive for development, tighten for production
        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.AllowAnyOrigin()
                      .AllowAnyMethod()
                      .AllowAnyHeader();
            });
        });

        // OpenAPI / Swagger
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();

        // Application-layer services (depend on IHubContext, so cannot live in Infrastructure)
        services.AddScoped<INotificationService, NotificationService>();

        return services;
    }
}
