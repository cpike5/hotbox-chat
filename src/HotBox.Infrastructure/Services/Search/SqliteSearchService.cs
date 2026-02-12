using System.Net;
using HotBox.Core.Interfaces;
using HotBox.Core.Models;
using HotBox.Core.Options;
using HotBox.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HotBox.Infrastructure.Services.Search;

public class SqliteSearchService : ISearchService
{
    private readonly HotBoxDbContext _context;
    private readonly ILogger<SqliteSearchService> _logger;
    private readonly SearchOptions _options;

    public SqliteSearchService(
        HotBoxDbContext context,
        ILogger<SqliteSearchService> logger,
        IOptions<SearchOptions> options)
    {
        _context = context;
        _logger = logger;
        _options = options.Value;
    }

    public bool IsFullTextSearchAvailable => true;

    public string ProviderName => "SQLite (FTS5)";

    public async Task InitializeIndexAsync(CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Initializing SQLite FTS5 virtual table and triggers");

            // Create the FTS5 virtual table if it doesn't exist
            await _context.Database.ExecuteSqlRawAsync(
                """
                CREATE VIRTUAL TABLE IF NOT EXISTS "MessagesFts"
                USING fts5(content, content="Messages", content_rowid="rowid")
                """,
                ct);

            // Create triggers to keep the FTS table in sync
            // Insert trigger
            await _context.Database.ExecuteSqlRawAsync(
                """
                CREATE TRIGGER IF NOT EXISTS "Messages_ai" AFTER INSERT ON "Messages" BEGIN
                    INSERT INTO "MessagesFts"(rowid, content) VALUES (new.rowid, new."Content");
                END
                """,
                ct);

            // Delete trigger
            await _context.Database.ExecuteSqlRawAsync(
                """
                CREATE TRIGGER IF NOT EXISTS "Messages_ad" AFTER DELETE ON "Messages" BEGIN
                    INSERT INTO "MessagesFts"("MessagesFts", rowid, content) VALUES('delete', old.rowid, old."Content");
                END
                """,
                ct);

            // Update trigger
            await _context.Database.ExecuteSqlRawAsync(
                """
                CREATE TRIGGER IF NOT EXISTS "Messages_au" AFTER UPDATE ON "Messages" BEGIN
                    INSERT INTO "MessagesFts"("MessagesFts", rowid, content) VALUES('delete', old.rowid, old."Content");
                    INSERT INTO "MessagesFts"(rowid, content) VALUES (new.rowid, new."Content");
                END
                """,
                ct);

            _logger.LogInformation("SQLite FTS5 virtual table and triggers initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize SQLite FTS5 virtual table");
            throw;
        }
    }

    public async Task ReindexAsync(CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Rebuilding SQLite FTS5 index");

            // Rebuild the FTS5 index by repopulating from the content table
            await _context.Database.ExecuteSqlRawAsync(
                """INSERT INTO "MessagesFts"("MessagesFts") VALUES('rebuild')""",
                ct);

            _logger.LogInformation("SQLite FTS5 index rebuilt successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rebuild SQLite FTS5 index");
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

            var offsetParamIndex = paramIndex;
            parameters.Add(offset);
            paramIndex++;
            var limitParamIndex = paramIndex;
            parameters.Add(limit + 1);

            var sql = @$"SELECT
    m.""Id"" AS ""MessageId"",
    snippet(""MessagesFts"", 0, X'01', X'02', '...', 32) AS ""Snippet"",
    m.""ChannelId"",
    c.""Name"" AS ""ChannelName"",
    m.""AuthorId"",
    u.""DisplayName"" AS ""AuthorDisplayName"",
    m.""CreatedAtUtc"",
    CAST(-""MessagesFts"".rank AS REAL) AS ""RelevanceScore""
FROM ""MessagesFts""
INNER JOIN ""Messages"" m ON m.rowid = ""MessagesFts"".rowid
INNER JOIN ""Channels"" c ON c.""Id"" = m.""ChannelId""
INNER JOIN ""AspNetUsers"" u ON u.""Id"" = m.""AuthorId""
WHERE ""MessagesFts"" MATCH {{0}}
    {filterClause}
ORDER BY ""MessagesFts"".rank
LIMIT {{{limitParamIndex}}} OFFSET {{{offsetParamIndex}}}";

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

            var countSql = @$"SELECT COUNT(*) AS ""Value""
FROM ""MessagesFts""
INNER JOIN ""Messages"" m ON m.rowid = ""MessagesFts"".rowid
WHERE ""MessagesFts"" MATCH {{0}}
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
            _logger.LogError(ex, "SQLite FTS5 search failed for query {Query}", query.QueryText);
            return new SearchResult { Items = [], Cursor = null, TotalEstimate = 0 };
        }
    }

    private static string SanitizeSnippet(string snippet)
    {
        if (string.IsNullOrEmpty(snippet))
            return string.Empty;

        var sanitized = WebUtility.HtmlEncode(snippet);
        return sanitized
            .Replace("\x01", "<mark>")
            .Replace("\x02", "</mark>");
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
