namespace HotBox.Client.Models;

public class SearchResultModel
{
    public List<SearchResultItemModel> Items { get; set; } = new();
    public string? Cursor { get; set; }
    public int TotalEstimate { get; set; }
}
