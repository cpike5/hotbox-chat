using System.Net;
using HotBox.Core.Interfaces;
using HotBox.Core.Models;
using HotBox.Core.Options;
using HotBox.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HotBox.Infrastructure.Services.Search;

public class FallbackSearchService : ISearchService
{
    private readonly HotBoxDbContext _context;
    private readonly ILogger<FallbackSearchService> _logger;
    private readonly SearchOptions _options;

    public FallbackSearchService(
        HotBoxDbContext context,
        ILogger<FallbackSearchService> logger,
        IOptions<SearchOptions> options)
    {
        _context = context;
        _logger = logger;
        _options = options.Value;
    }

    public bool IsFullTextSearchAvailable => false;

    public string ProviderName => "Fallback (SQL LIKE)";

    public Task InitializeIndexAsync(CancellationToken ct = default)
    {
        _logger.LogWarning(
            "Full-text search is not available. Using fallback SQL LIKE search which may be slow on large datasets");
        return Task.CompletedTask;
    }

    public Task ReindexAsync(CancellationToken ct = default)
    {
        _logger.LogWarning("Reindex requested but full-text search is not available; no index to rebuild");
        return Task.CompletedTask;
    }

    public async Task<SearchResult> SearchMessagesAsync(SearchQuery query, CancellationToken ct = default)
    {
        try
        {
            var offset = 0;
            if (!string.IsNullOrWhiteSpace(query.Cursor) && int.TryParse(query.Cursor, out var parsedOffset))
            {
                offset = parsedOffset;
            }

            var limit = Math.Min(query.Limit, _options.MaxResults);
            // Escape LIKE wildcards to prevent pattern injection
            var escapedQuery = query.QueryText
                .Replace("\\", "\\\\")
                .Replace("%", "\\%")
                .Replace("_", "\\_");
            var likePattern = $"%{escapedQuery}%";

            var messagesQuery = _context.Messages
                .Include(m => m.Channel)
                .Include(m => m.Author)
                .Where(m => EF.Functions.Like(m.Content, likePattern));

            if (query.ChannelId.HasValue)
            {
                messagesQuery = messagesQuery.Where(m => m.ChannelId == query.ChannelId.Value);
            }

            if (query.SenderId.HasValue)
            {
                messagesQuery = messagesQuery.Where(m => m.AuthorId == query.SenderId.Value);
            }

            var totalEstimate = await messagesQuery.CountAsync(ct);

            var messages = await messagesQuery
                .OrderByDescending(m => m.CreatedAtUtc)
                .Skip(offset)
                .Take(limit + 1)
                .ToListAsync(ct);

            var hasMore = messages.Count > limit;
            var snippetLength = _options.SnippetLength;

            var resultItems = messages
                .Take(limit)
                .Select(m => new SearchResultItem
                {
                    MessageId = m.Id,
                    Snippet = GenerateSnippet(m.Content, query.QueryText, snippetLength),
                    ChannelId = m.ChannelId,
                    ChannelName = m.Channel?.Name ?? "Unknown",
                    AuthorId = m.AuthorId,
                    AuthorDisplayName = m.Author?.DisplayName ?? "Unknown",
                    CreatedAtUtc = m.CreatedAtUtc,
                    RelevanceScore = 0.0,
                })
                .ToList();

            return new SearchResult
            {
                Items = resultItems,
                Cursor = hasMore ? (offset + limit).ToString() : null,
                TotalEstimate = totalEstimate,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallback search failed for query {Query}", query.QueryText);
            return new SearchResult { Items = [], Cursor = null, TotalEstimate = 0 };
        }
    }

    private static string GenerateSnippet(string content, string queryText, int snippetLength)
    {
        if (string.IsNullOrWhiteSpace(content))
            return string.Empty;

        var index = content.IndexOf(queryText, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return content.Length <= snippetLength
                ? content
                : content[..snippetLength] + "...";
        }

        var halfSnippet = snippetLength / 2;
        var start = Math.Max(0, index - halfSnippet);
        var end = Math.Min(content.Length, start + snippetLength);
        start = Math.Max(0, end - snippetLength);

        var snippet = content[start..end];
        var prefix = start > 0 ? "..." : "";
        var suffix = end < content.Length ? "..." : "";

        // Highlight matched terms with <mark> tags
        var highlighted = HighlightTerms(snippet, queryText);

        return prefix + highlighted + suffix;
    }

    private static string HighlightTerms(string text, string queryText)
    {
        // HTML-encode to prevent XSS before adding <mark> tags
        var encoded = WebUtility.HtmlEncode(text);
        var words = queryText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var result = encoded;

        foreach (var word in words)
        {
            var encodedWord = WebUtility.HtmlEncode(word);
            var idx = 0;
            while (idx < result.Length)
            {
                var pos = result.IndexOf(encodedWord, idx, StringComparison.OrdinalIgnoreCase);
                if (pos < 0) break;

                var matched = result.Substring(pos, encodedWord.Length);
                var replacement = $"<mark>{matched}</mark>";
                result = result[..pos] + replacement + result[(pos + encodedWord.Length)..];
                idx = pos + replacement.Length;
            }
        }

        return result;
    }
}
