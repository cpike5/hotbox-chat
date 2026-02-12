namespace HotBox.Core.Options;

public class ObservabilityOptions
{
    public const string SectionName = "Observability";

    public string SeqUrl { get; set; } = "http://localhost:5341";

    public string OtlpEndpoint { get; set; } = string.Empty;

    public string LogLevel { get; set; } = "Information";
}
