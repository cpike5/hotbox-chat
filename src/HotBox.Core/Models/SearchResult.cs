namespace HotBox.Core.Models;

public class SearchResult
{
    public IReadOnlyList<SearchResultItem> Items { get; set; } = [];

    public string? Cursor { get; set; }

    public int TotalEstimate { get; set; }
}
