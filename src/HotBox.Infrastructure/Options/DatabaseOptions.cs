namespace HotBox.Infrastructure.Options;

public class DatabaseOptions
{
    public const string SectionName = "Database";

    public string Provider { get; set; } = "sqlite";

    public string ConnectionString { get; set; } = string.Empty;
}
