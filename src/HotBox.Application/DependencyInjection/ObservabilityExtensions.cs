using HotBox.Core.Options;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;

namespace HotBox.Application.DependencyInjection;

public static class ObservabilityExtensions
{
    public static IHostBuilder AddObservability(this IHostBuilder host, IConfiguration configuration)
    {
        var obsOptions = configuration.GetSection(ObservabilityOptions.SectionName).Get<ObservabilityOptions>()
                         ?? new ObservabilityOptions();

        host.UseSerilog((context, loggerConfig) =>
        {
            var minLevel = Enum.TryParse<LogEventLevel>(obsOptions.LogLevel, true, out var parsed)
                ? parsed
                : LogEventLevel.Information;

            loggerConfig
                .MinimumLevel.Is(minLevel)
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .Enrich.WithEnvironmentName()
                .Enrich.WithThreadId()
                .Enrich.WithProperty("Application", "HotBox")
                .WriteTo.Console();

            if (!string.IsNullOrWhiteSpace(obsOptions.SeqUrl))
            {
                loggerConfig.WriteTo.Seq(obsOptions.SeqUrl);
            }

            if (!string.IsNullOrWhiteSpace(obsOptions.ElasticsearchUrl))
            {
                loggerConfig.WriteTo.Elasticsearch(obsOptions.ElasticsearchUrl,
                    indexFormat: $"hotbox-{obsOptions.Environment}-{{0:yyyy.MM.dd}}");
            }
        });

        return host;
    }

    public static IServiceCollection AddOpenTelemetryObservability(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var obsOptions = configuration.GetSection(ObservabilityOptions.SectionName).Get<ObservabilityOptions>()
                         ?? new ObservabilityOptions();

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(
                    serviceName: "HotBox",
                    serviceVersion: typeof(ObservabilityExtensions).Assembly.GetName().Version?.ToString() ?? "0.0.0"))
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddEntityFrameworkCoreInstrumentation();

                if (!string.IsNullOrWhiteSpace(obsOptions.OtlpEndpoint))
                {
                    tracing.AddOtlpExporter(opts =>
                    {
                        opts.Endpoint = new Uri(obsOptions.OtlpEndpoint);
                        if (!string.IsNullOrWhiteSpace(obsOptions.OtlpApiKey))
                        {
                            opts.Headers = $"api-key={obsOptions.OtlpApiKey}";
                        }
                    });
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
                    {
                        opts.Endpoint = new Uri(obsOptions.OtlpEndpoint);
                        if (!string.IsNullOrWhiteSpace(obsOptions.OtlpApiKey))
                        {
                            opts.Headers = $"api-key={obsOptions.OtlpApiKey}";
                        }
                    });
                }
            });

        return services;
    }
}
