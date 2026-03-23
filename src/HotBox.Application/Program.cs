using HotBox.Application.DependencyInjection;
using HotBox.Application.Hubs;
using HotBox.Application.Middleware;
using HotBox.Core.Interfaces;
using HotBox.Infrastructure.Data;
using HotBox.Infrastructure.DependencyInjection;
using HotBox.Infrastructure.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Scalar.AspNetCore;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Observability
    builder.Host.AddObservability(builder.Configuration);
    builder.Services.AddOpenTelemetryObservability(builder.Configuration);

    // Infrastructure (EF Core, Identity, repos, services, validators, options)
    builder.Services.AddInfrastructure(builder.Configuration);

    // Application services (auth, SignalR, controllers, health checks)
    builder.Services.AddApplicationServices(builder.Configuration);

    // HybridCache with Redis L2
    builder.Services.AddHybridCache(options =>
    {
        options.DefaultEntryOptions = new HybridCacheEntryOptions
        {
            Expiration = TimeSpan.FromMinutes(5),
            LocalCacheExpiration = TimeSpan.FromMinutes(1),
        };
    });
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = builder.Configuration.GetConnectionString("Redis");
        options.InstanceName = "HotBox:";
    });

    // Health checks
    builder.Services.AddHealthChecks()
        .AddDbContextCheck<HotBoxDbContext>("database")
        .AddRedis(builder.Configuration.GetConnectionString("Redis")!, name: "redis");

    var app = builder.Build();

    // Auto-migrate
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<HotBoxDbContext>();
        db.Database.Migrate();
    }

    // Initialize search
    using (var scope = app.Services.CreateScope())
    {
        var searchService = scope.ServiceProvider.GetRequiredService<ISearchService>();
        await searchService.InitializeIndexAsync(CancellationToken.None);
    }

    // Pipeline
    app.UseSerilogRequestLogging();
    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapScalarApiReference();
    }
    app.UseHttpsRedirection();
    app.UseBlazorFrameworkFiles();
    app.UseStaticFiles();
    app.UseCors();
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseMiddleware<AgentPresenceMiddleware>();
    app.MapControllers();
    app.MapHub<ChatHub>("/hubs/chat");
    app.MapHub<VoiceSignalingHub>("/hubs/voice");
    app.MapHealthChecks("/health");
    app.MapFallbackToFile("index.html");

    // Wire presence events to SignalR
    var presenceService = app.Services.GetRequiredService<PresenceService>();
    var hubContext = app.Services.GetRequiredService<IHubContext<ChatHub>>();
    presenceService.OnUserStatusChanged += (userId, displayName, status, isAgent) =>
    {
        _ = hubContext.Clients.All.SendAsync("UserStatusChanged", userId, displayName, status, isAgent);
    };

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// Make the auto-generated Program class accessible to integration tests
public partial class Program;
