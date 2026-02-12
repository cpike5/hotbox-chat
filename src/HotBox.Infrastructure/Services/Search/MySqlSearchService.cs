using System.Net;
using HotBox.Core.Interfaces;
using HotBox.Core.Models;
using HotBox.Core.Options;
using HotBox.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HotBox.Infrastructure.Services.Search;

public class MySqlSearchService : ISearchService
{
    private readonly HotBoxDbContext _context;
    private readonly ILogger<MySqlSearchService> _logger;
    private readonly SearchOptions _options;

    public MySqlSearchService(
        HotBoxDbContext context,
        ILogger<MySqlSearchService> logger,
        IOptions<SearchOptions> options)
    {
        _context = context;
        _logger = logger;
        _options = options.Value;
    }

    public bool IsFullTextSearchAvailable => true;

    public string ProviderName => "MySQL (FULLTEXT)";

    public async Task InitializeIndexAsync(CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Initializing MySQL FULLTEXT index on Messages.Content");

            // MySQL doesn't support CREATE INDEX IF NOT EXISTS directly,
            // so check if it exists first via information_schema
            var indexExists = await _context.Database
                .SqlQueryRaw<int>(
                    """
                    SELECT COUNT(*) AS `Value`
                    FROM information_schema.STATISTICS
                    WHERE TABLE_SCHEMA = DATABASE()
                      AND TABLE_NAME = 'Messages'
                      AND INDEX_NAME = 'IX_Messages_Content_FTS'
                    """)
                .FirstOrDefaultAsync(ct);

            if (indexExists == 0)
            {
                await _context.Database.ExecuteSqlRawAsync(
                    "CREATE FULLTEXT INDEX `IX_Messages_Content_FTS` ON `Messages` (`Content`)",
                    ct);
            }

            _logger.LogInformation("MySQL FULLTEXT index initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize MySQL FULLTEXT index");
            throw;
        }
    }

    public async Task ReindexAsync(CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Rebuilding MySQL FULLTEXT index on Messages.Content");

            // MySQL rebuilds fulltext indexes via OPTIMIZE TABLE
            await _context.Database.ExecuteSqlRawAsync(
                "OPTIMIZE TABLE `Messages`",
                ct);

            _logger.LogInformation("MySQL FULLTEXT index rebuilt successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rebuild MySQL FULLTEXT index");
            throw;
        }
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

            // Build filter clauses and parameters dynamically
            var filters = new List<string>();
            var parameters = new List<object> { query.QueryText }; // {0} = query text
            var paramIndex = 1;

            if (query.ChannelId.HasValue)
            {
                filters.Add($"AND m.`ChannelId` = {{{paramIndex}}}");
                parameters.Add(query.ChannelId.Value);
                paramIndex++;
            }

            if (query.SenderId.HasValue)
            {
                filters.Add($"AND m.`AuthorId` = {{{paramIndex}}}");
                parameters.Add(query.SenderId.Value);
                paramIndex++;
            }

            var filterClause = filters.Count > 0 ? string.Join(" ", filters) : "";

            var offsetParamIndex = paramIndex;
            parameters.Add(offset);
            paramIndex++;
            var limitParamIndex = paramIndex;
            parameters.Add(limit + 1);

            var sql = @$"SELECT
    m.`Id` AS `MessageId`,
    m.`Content` AS `Snippet`,
    m.`ChannelId`,
    c.`Name` AS `ChannelName`,
    m.`AuthorId`,
    u.`DisplayName` AS `AuthorDisplayName`,
    m.`CreatedAtUtc`,
    MATCH(m.`Content`) AGAINST({{0}} IN NATURAL LANGUAGE MODE) AS `RelevanceScore`
FROM `Messages` m
INNER JOIN `Channels` c ON c.`Id` = m.`ChannelId`
INNER JOIN `AspNetUsers` u ON u.`Id` = m.`AuthorId`
WHERE MATCH(m.`Content`) AGAINST({{0}} IN NATURAL LANGUAGE MODE)
    {filterClause}
ORDER BY `RelevanceScore` DESC, m.`CreatedAtUtc` DESC
LIMIT {{{limitParamIndex}}} OFFSET {{{offsetParamIndex}}}";

            var items = await _context.Database
                .SqlQueryRaw<SearchResultItemRaw>(sql, parameters.ToArray())
                .ToListAsync(ct);

            var hasMore = items.Count > limit;
            var snippetLength = _options.SnippetLength;

            var resultItems = items
                .Take(limit)
                .Select(r => new SearchResultItem
                {
                    MessageId = r.MessageId,
                    Snippet = GenerateSnippet(r.Snippet, query.QueryText, snippetLength),
                    ChannelId = r.ChannelId,
                    ChannelName = r.ChannelName,
                    AuthorId = r.AuthorId,
                    AuthorDisplayName = r.AuthorDisplayName,
                    CreatedAtUtc = r.CreatedAtUtc,
                    RelevanceScore = r.RelevanceScore,
                })
                .ToList();

            // Count total estimate
            var countParams = new List<object> { query.QueryText };
            var countFilters = new List<string>();
            var countParamIdx = 1;

            if (query.ChannelId.HasValue)
            {
                countFilters.Add($"AND m.`ChannelId` = {{{countParamIdx}}}");
                countParams.Add(query.ChannelId.Value);
                countParamIdx++;
            }

            if (query.SenderId.HasValue)
            {
                countFilters.Add($"AND m.`AuthorId` = {{{countParamIdx}}}");
                countParams.Add(query.SenderId.Value);
                countParamIdx++;
            }

            var countFilterClause = countFilters.Count > 0 ? string.Join(" ", countFilters) : "";

            var countSql = @$"SELECT COUNT(*) AS `Value`
FROM `Messages` m
WHERE MATCH(m.`Content`) AGAINST({{0}} IN NATURAL LANGUAGE MODE)
    {countFilterClause}";

            var totalEstimate = await _context.Database
                .SqlQueryRaw<int>(countSql, countParams.ToArray())
                .FirstOrDefaultAsync(ct);

            return new SearchResult
            {
                Items = resultItems,
                Cursor = hasMore ? (offset + limit).ToString() : null,
                TotalEstimate = totalEstimate,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MySQL full-text search failed for query {Query}", query.QueryText);
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
            // Try to match individual words from the query
            var words = queryText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var word in words)
            {
                index = content.IndexOf(word, StringComparison.OrdinalIgnoreCase);
                if (index >= 0) break;
            }
        }

        if (index < 0)
        {
            // No match found, return start of content
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

    private class SearchResultItemRaw
    {
        public Guid MessageId { get; set; }
        public string Snippet { get; set; } = string.Empty;
        public Guid ChannelId { get; set; }
        public string ChannelName { get; set; } = string.Empty;
        public Guid AuthorId { get; set; }
        public string AuthorDisplayName { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
        public double RelevanceScore { get; set; }
    }
}
