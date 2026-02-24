using HotBox.Client.Models;

namespace HotBox.Client.State;

public class SearchState
{
    public string Query { get; private set; } = string.Empty;
    public List<SearchResultItemModel> Results { get; private set; } = new();
    public bool IsSearching { get; private set; }
    public bool IsOpen { get; private set; }
    public int TotalEstimate { get; private set; }
    public string? Cursor { get; private set; }
    public bool HasError { get; private set; }
    public string? ErrorMessage { get; private set; }

    public event Action? OnChange;

    public void Open()
    {
        IsOpen = true;
        NotifyStateChanged();
    }

    public void Close()
    {
        IsOpen = false;
        NotifyStateChanged();
    }

    public void SetResults(List<SearchResultItemModel> results, int totalEstimate, string? cursor)
    {
        Results = results;
        TotalEstimate = totalEstimate;
        Cursor = cursor;
        HasError = false;
        ErrorMessage = null;
        NotifyStateChanged();
    }

    public void AppendResults(List<SearchResultItemModel> results, int totalEstimate, string? cursor)
    {
        Results.AddRange(results);
        TotalEstimate = totalEstimate;
        Cursor = cursor;
        NotifyStateChanged();
    }

    public void SetError(string message)
    {
        HasError = true;
        ErrorMessage = message;
        IsSearching = false;
        NotifyStateChanged();
    }

    public void SetSearching(bool searching)
    {
        IsSearching = searching;
        NotifyStateChanged();
    }

    public void SetQuery(string query)
    {
        Query = query;
        NotifyStateChanged();
    }

    public void Clear()
    {
        Query = string.Empty;
        Results = new();
        IsSearching = false;
        TotalEstimate = 0;
        Cursor = null;
        HasError = false;
        ErrorMessage = null;
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}
