using HotBox.Core.Models;

namespace HotBox.Core.Interfaces;

public interface ISearchService
{
    Task<SearchResult> SearchMessagesAsync(SearchQuery query, CancellationToken ct = default);

    Task InitializeIndexAsync(CancellationToken ct = default);

    Task ReindexAsync(CancellationToken ct = default);

    bool IsFullTextSearchAvailable { get; }

    string ProviderName { get; }
}
