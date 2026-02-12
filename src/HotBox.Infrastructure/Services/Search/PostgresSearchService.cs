using System.Net;
using HotBox.Core.Interfaces;
using HotBox.Core.Models;
using HotBox.Core.Options;
using HotBox.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HotBox.Infrastructure.Services.Search;

public class PostgresSearchService : ISearchService
{
    private readonly HotBoxDbContext _context;
    private readonly ILogger<PostgresSearchService> _logger;
    private readonly SearchOptions _options;

    public PostgresSearchService(
        HotBoxDbContext context,
        ILogger<PostgresSearchService> logger,
        IOptions<SearchOptions> options)
    {
        _context = context;
        _logger = logger;
        _options = options.Value;
    }

    public bool IsFullTextSearchAvailable => true;

    public string ProviderName => "PostgreSQL (tsvector/GIN)";

    public async Task InitializeIndexAsync(CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Initializing PostgreSQL GIN index on Messages.Content");

            await _context.Database.ExecuteSqlRawAsync(
                """
                CREATE INDEX IF NOT EXISTS "IX_Messages_Content_FTS"
                ON "Messages" USING GIN (to_tsvector('english', "Content"))
                """,
                ct);

            _logger.LogInformation("PostgreSQL GIN index initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize PostgreSQL GIN index");
            throw;
        }
    }

    public async Task ReindexAsync(CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Rebuilding PostgreSQL GIN index on Messages.Content");

            await _context.Database.ExecuteSqlRawAsync(
                """REINDEX INDEX "IX_Messages_Content_FTS" """,
                ct);

            _logger.LogInformation("PostgreSQL GIN index rebuilt successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rebuild PostgreSQL GIN index");
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
                filters.Add($@"AND m.""ChannelId"" = {{{paramIndex}}}");
                parameters.Add(query.ChannelId.Value);
                paramIndex++;
            }

            if (query.SenderId.HasValue)
            {
                filters.Add($@"AND m.""AuthorId"" = {{{paramIndex}}}");
                parameters.Add(query.SenderId.Value);
                paramIndex++;
            }

            var filterClause = filters.Count > 0 ? string.Join(" ", filters) : "";

            // Offset and limit parameters
            var offsetParamIndex = paramIndex;
            parameters.Add(offset);
            paramIndex++;
            var limitParamIndex = paramIndex;
            parameters.Add(limit + 1); // Fetch one extra to detect next page

            var sql = @$"SELECT
    m.""Id"" AS ""MessageId"",
    ts_headline('english', m.""Content"", plainto_tsquery('english', {{0}}),
        'StartSel=\x01, StopSel=\x02, MaxFragments=1, MaxWords=50, MinWords=20') AS ""Snippet"",
    m.""ChannelId"",
    c.""Name"" AS ""ChannelName"",
    m.""AuthorId"",
    u.""DisplayName"" AS ""AuthorDisplayName"",
    m.""CreatedAtUtc"",
    ts_rank(to_tsvector('english', m.""Content""), plainto_tsquery('english', {{0}})) AS ""RelevanceScore""
FROM ""Messages"" m
INNER JOIN ""Channels"" c ON c.""Id"" = m.""ChannelId""
INNER JOIN ""AspNetUsers"" u ON u.""Id"" = m.""AuthorId""
WHERE to_tsvector('english', m.""Content"") @@ plainto_tsquery('english', {{0}})
    {filterClause}
ORDER BY ""RelevanceScore"" DESC, m.""CreatedAtUtc"" DESC
OFFSET {{{offsetParamIndex}}} ROWS FETCH NEXT {{{limitParamIndex}}} ROWS ONLY";

            var items = await _context.Database
                .SqlQueryRaw<SearchResultItemRaw>(sql, parameters.ToArray())
                .ToListAsync(ct);

            var hasMore = items.Count > limit;
            var resultItems = items
                .Take(limit)
                .Select(r => new SearchResultItem
                {
                    MessageId = r.MessageId,
                    Snippet = SanitizeSnippet(r.Snippet),
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
                countFilters.Add($@"AND m.""ChannelId"" = {{{countParamIdx}}}");
                countParams.Add(query.ChannelId.Value);
                countParamIdx++;
            }

            if (query.SenderId.HasValue)
            {
                countFilters.Add($@"AND m.""AuthorId"" = {{{countParamIdx}}}");
                countParams.Add(query.SenderId.Value);
                countParamIdx++;
            }

            var countFilterClause = countFilters.Count > 0 ? string.Join(" ", countFilters) : "";

            var countSql = @$"SELECT COUNT(*)::int AS ""Value""
FROM ""Messages"" m
WHERE to_tsvector('english', m.""Content"") @@ plainto_tsquery('english', {{0}})
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
            _logger.LogError(ex, "PostgreSQL full-text search failed for query {Query}", query.QueryText);
            return new SearchResult { Items = [], Cursor = null, TotalEstimate = 0 };
        }
    }

    /// <summary>
    /// HTML-encodes snippet content while preserving highlight markers.
    /// ts_headline uses control chars \x01/\x02 as markers which pass through HtmlEncode unchanged.
    /// </summary>
    private static string SanitizeSnippet(string snippet)
    {
        if (string.IsNullOrEmpty(snippet))
            return string.Empty;

        var sanitized = WebUtility.HtmlEncode(snippet);
        return sanitized
            .Replace("\x01", "<mark>")
            .Replace("\x02", "</mark>");
    }

    // Internal DTO for raw SQL mapping
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
