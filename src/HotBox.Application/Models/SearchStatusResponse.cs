namespace HotBox.Application.Models;

public class SearchStatusResponse
{
    public bool IsFullTextSearchAvailable { get; set; }

    public string ProviderName { get; set; } = string.Empty;
}
