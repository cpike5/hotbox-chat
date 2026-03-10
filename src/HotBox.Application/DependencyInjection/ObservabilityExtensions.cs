using HotBox.Core.Options;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Sinks.Elasticsearch;

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

            // Elasticsearch sink (if URL configured)
            if (!string.IsNullOrWhiteSpace(obsOptions.ElasticsearchUrl))
            {
                var esSinkOptions = new ElasticsearchSinkOptions(new Uri(obsOptions.ElasticsearchUrl))
                {
                    AutoRegisterTemplate = true,
                    IndexFormat = "hotbox-logs-{0:yyyy.MM.dd}"
                };

                if (!string.IsNullOrWhiteSpace(obsOptions.ElasticsearchApiKey))
                {
                    esSinkOptions.ModifyConnectionSettings = conn =>
                        conn.ApiKeyAuthentication(new Elasticsearch.Net.ApiKeyAuthenticationCredentials(obsOptions.ElasticsearchApiKey));
                }

                loggerConfig.WriteTo.Elasticsearch(esSinkOptions);
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

        var environment = obsOptions.Environment;

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService("hotbox")
                .AddAttributes(new Dictionary<string, object>
                {
                    ["deployment.environment"] = environment
                }))
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
                            opts.Headers = $"Authorization=ApiKey {obsOptions.OtlpApiKey}";
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
                            opts.Headers = $"Authorization=ApiKey {obsOptions.OtlpApiKey}";
                    });
                }
            });

        return services;
    }
}
