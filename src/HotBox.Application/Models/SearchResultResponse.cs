namespace HotBox.Application.Models;

public class SearchResultResponse
{
    public IReadOnlyList<SearchResultItemResponse> Items { get; set; } = [];

    public string? Cursor { get; set; }

    public int TotalEstimate { get; set; }
}
