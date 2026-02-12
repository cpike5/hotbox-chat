using HotBox.Core.Options;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

namespace HotBox.Application.DependencyInjection;

public static class ObservabilityExtensions
{
    public static IHostBuilder AddObservability(
        this IHostBuilder hostBuilder,
        IConfiguration configuration)
    {
        hostBuilder.UseSerilog((context, services, loggerConfig) =>
        {
            var obsOptions = configuration
                .GetSection(ObservabilityOptions.SectionName)
                .Get<ObservabilityOptions>() ?? new ObservabilityOptions();

            // Parse minimum log level from config with safe fallback
            if (!Enum.TryParse<Serilog.Events.LogEventLevel>(obsOptions.LogLevel, ignoreCase: true, out var minLevel))
            {
                minLevel = Serilog.Events.LogEventLevel.Information;
            }

            loggerConfig
                .MinimumLevel.Is(minLevel)
                .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore", Serilog.Events.LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .Enrich.WithMachineName()
                .Enrich.WithThreadId()
                .WriteTo.Console();

            // Seq sink (if URL configured)
            if (!string.IsNullOrWhiteSpace(obsOptions.SeqUrl))
            {
                loggerConfig.WriteTo.Seq(obsOptions.SeqUrl);
            }
        });

        return hostBuilder;
    }

    public static IServiceCollection AddOpenTelemetryObservability(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var obsOptions = configuration
            .GetSection(ObservabilityOptions.SectionName)
            .Get<ObservabilityOptions>() ?? new ObservabilityOptions();

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService("HotBox"))
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddEntityFrameworkCoreInstrumentation();

                if (!string.IsNullOrWhiteSpace(obsOptions.OtlpEndpoint))
                {
                    tracing.AddOtlpExporter(opts =>
                        opts.Endpoint = new Uri(obsOptions.OtlpEndpoint));
                }
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();

                if (!string.IsNullOrWhiteSpace(obsOptions.OtlpEndpoint))
                {
                    metrics.AddOtlpExporter(opts =>
                        opts.Endpoint = new Uri(obsOptions.OtlpEndpoint));
                }
            });

        return services;
    }
}
