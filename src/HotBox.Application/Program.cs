using HotBox.Application.DependencyInjection;
using HotBox.Infrastructure.Data;
using HotBox.Infrastructure.DependencyInjection;
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

    var app = builder.Build();

    // Auto-migrate database in development
    if (app.Environment.IsDevelopment())
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HotBoxDbContext>();
        db.Database.Migrate();
    }

    // Configure the HTTP request pipeline.
    app.UseSerilogRequestLogging();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseHttpsRedirection();
    app.UseCors();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();

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
