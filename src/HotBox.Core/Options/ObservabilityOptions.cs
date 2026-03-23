namespace HotBox.Core.Options;

public class ObservabilityOptions
{
    public const string SectionName = "Observability";

    public string SeqUrl { get; set; } = "http://localhost:5341";

    public string OtlpEndpoint { get; set; } = string.Empty;

    public string OtlpApiKey { get; set; } = string.Empty;

    public string ElasticsearchUrl { get; set; } = string.Empty;

    public string ElasticsearchApiKey { get; set; } = string.Empty;

    public string Environment { get; set; } = "development";

    public string LogLevel { get; set; } = "Information";
}
