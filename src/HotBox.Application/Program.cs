using HotBox.Application.DependencyInjection;
using HotBox.Application.Hubs;
using HotBox.Core.Enums;
using HotBox.Core.Interfaces;
using HotBox.Infrastructure.Data;
using HotBox.Infrastructure.Data.Seeding;
using HotBox.Infrastructure.DependencyInjection;
using HotBox.Infrastructure.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Serilog;

// Bootstrap Serilog early for startup logging
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Observability — Serilog replaces default logging
    builder.Host.AddObservability(builder.Configuration);
    builder.Services.AddOpenTelemetryObservability(builder.Configuration);

    // Add services to the container.
    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddApplicationServices(builder.Configuration);
    builder.Services.AddHostedService<DatabaseSeeder>();

    // Health checks (used by Docker and load balancers)
    builder.Services.AddHealthChecks()
        .AddDbContextCheck<HotBoxDbContext>("database");

    var app = builder.Build();

    // Auto-migrate database on startup
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HotBoxDbContext>();
        db.Database.Migrate();
    }

    // Initialize search indexes (FTS tables, GIN indexes, etc.)
    {
        using var scope = app.Services.CreateScope();
        var searchService = scope.ServiceProvider.GetRequiredService<ISearchService>();
        searchService.InitializeIndexAsync(CancellationToken.None).GetAwaiter().GetResult();
    }

    // Configure the HTTP request pipeline.
    app.UseSerilogRequestLogging();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseHttpsRedirection();
    app.UseBlazorFrameworkFiles();
    app.UseStaticFiles();
    app.UseCors();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();
    app.MapHub<ChatHub>("/hubs/chat");
    app.MapHub<VoiceSignalingHub>("/hubs/voice");
    app.MapHealthChecks("/health");
    app.MapFallbackToFile("index.html");

    // Wire up PresenceService status change events to broadcast via SignalR.
    // This handles deferred events like grace-period expiration and idle timeouts
    // that fire outside of a Hub method invocation.
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
