namespace HotBox.Core.Options;

public class SearchOptions
{
    public const string SectionName = "Search";

    public int MaxResults { get; set; } = 50;

    public int DefaultLimit { get; set; } = 20;

    public int MinQueryLength { get; set; } = 2;

    public int SnippetLength { get; set; } = 150;
}
