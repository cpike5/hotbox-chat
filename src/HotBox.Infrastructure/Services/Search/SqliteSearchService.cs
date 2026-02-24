using System.Net;
using HotBox.Core.Enums;
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

            // Create the FTS5 virtual table for Messages if it doesn't exist
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

            // Create the FTS5 virtual table for DirectMessages if it doesn't exist
            await _context.Database.ExecuteSqlRawAsync(
                """
                CREATE VIRTUAL TABLE IF NOT EXISTS "DirectMessagesFts"
                USING fts5(content, content="DirectMessages", content_rowid="rowid")
                """,
                ct);

            // Insert trigger for DirectMessages
            await _context.Database.ExecuteSqlRawAsync(
                """
                CREATE TRIGGER IF NOT EXISTS "DirectMessages_ai" AFTER INSERT ON "DirectMessages" BEGIN
                    INSERT INTO "DirectMessagesFts"(rowid, content) VALUES (new.rowid, new."Content");
                END
                """,
                ct);

            // Delete trigger for DirectMessages
            await _context.Database.ExecuteSqlRawAsync(
                """
                CREATE TRIGGER IF NOT EXISTS "DirectMessages_ad" AFTER DELETE ON "DirectMessages" BEGIN
                    INSERT INTO "DirectMessagesFts"("DirectMessagesFts", rowid, content) VALUES('delete', old.rowid, old."Content");
                END
                """,
                ct);

            // Update trigger for DirectMessages
            await _context.Database.ExecuteSqlRawAsync(
                """
                CREATE TRIGGER IF NOT EXISTS "DirectMessages_au" AFTER UPDATE ON "DirectMessages" BEGIN
                    INSERT INTO "DirectMessagesFts"("DirectMessagesFts", rowid, content) VALUES('delete', old.rowid, old."Content");
                    INSERT INTO "DirectMessagesFts"(rowid, content) VALUES (new.rowid, new."Content");
                END
                """,
                ct);

            _logger.LogInformation("SQLite FTS5 virtual tables and triggers initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize SQLite FTS5 virtual tables");
            throw;
        }
    }

    public async Task ReindexAsync(CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Rebuilding SQLite FTS5 indexes");

            await _context.Database.ExecuteSqlRawAsync(
                """INSERT INTO "MessagesFts"("MessagesFts") VALUES('rebuild')""",
                ct);

            await _context.Database.ExecuteSqlRawAsync(
                """INSERT INTO "DirectMessagesFts"("DirectMessagesFts") VALUES('rebuild')""",
                ct);

            _logger.LogInformation("SQLite FTS5 indexes rebuilt successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rebuild SQLite FTS5 indexes");
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
            var searchChannels = query.Scope is SearchScope.All or SearchScope.Channels;
            var searchDms = (query.Scope is SearchScope.All or SearchScope.DirectMessages)
                            && query.CallerUserId.HasValue;

            var allItems = new List<SearchResultItem>();
            var totalEstimate = 0;

            if (searchChannels)
            {
                var (channelItems, channelCount) = await SearchChannelMessagesAsync(query, offset, limit, ct);
                allItems.AddRange(channelItems);
                totalEstimate += channelCount;
            }

            if (searchDms)
            {
                var (dmItems, dmCount) = await SearchDirectMessagesAsync(query, offset, limit, ct);
                allItems.AddRange(dmItems);
                totalEstimate += dmCount;
            }

            // For "All" scope, sort merged results by relevance then time
            if (query.Scope == SearchScope.All)
            {
                allItems = allItems
                    .OrderByDescending(i => i.RelevanceScore)
                    .ThenByDescending(i => i.CreatedAtUtc)
                    .ToList();
            }

            var hasMore = allItems.Count > limit;
            var resultItems = allItems.Take(limit).ToList();

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

    private async Task<(List<SearchResultItem> Items, int Count)> SearchChannelMessagesAsync(
        SearchQuery query, int offset, int limit, CancellationToken ct)
    {
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

        var resultItems = items
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

        // Count total
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

        return (resultItems, totalEstimate);
    }

    private async Task<(List<SearchResultItem> Items, int Count)> SearchDirectMessagesAsync(
        SearchQuery query, int offset, int limit, CancellationToken ct)
    {
        var callerUserId = query.CallerUserId!.Value;

        var parameters = new List<object> { query.QueryText, callerUserId }; // {0} = query text, {1} = callerUserId
        var paramIndex = 2;

        var offsetParamIndex = paramIndex;
        parameters.Add(offset);
        paramIndex++;
        var limitParamIndex = paramIndex;
        parameters.Add(limit + 1);

        var sql = @$"SELECT
    dm.""Id"" AS ""MessageId"",
    snippet(""DirectMessagesFts"", 0, X'01', X'02', '...', 32) AS ""Snippet"",
    dm.""SenderId"",
    dm.""RecipientId"",
    sender.""DisplayName"" AS ""SenderDisplayName"",
    recipient.""DisplayName"" AS ""RecipientDisplayName"",
    dm.""CreatedAtUtc"",
    CAST(-""DirectMessagesFts"".rank AS REAL) AS ""RelevanceScore""
FROM ""DirectMessagesFts""
INNER JOIN ""DirectMessages"" dm ON dm.rowid = ""DirectMessagesFts"".rowid
INNER JOIN ""AspNetUsers"" sender ON sender.""Id"" = dm.""SenderId""
INNER JOIN ""AspNetUsers"" recipient ON recipient.""Id"" = dm.""RecipientId""
WHERE ""DirectMessagesFts"" MATCH {{0}}
    AND (dm.""SenderId"" = {{1}} OR dm.""RecipientId"" = {{1}})
ORDER BY ""DirectMessagesFts"".rank
LIMIT {{{limitParamIndex}}} OFFSET {{{offsetParamIndex}}}";

        var items = await _context.Database
            .SqlQueryRaw<DmSearchResultItemRaw>(sql, parameters.ToArray())
            .ToListAsync(ct);

        var resultItems = items
            .Select(r => MapDmResult(r, callerUserId, SanitizeSnippet(r.Snippet)))
            .ToList();

        // Count
        var countSql = @$"SELECT COUNT(*) AS ""Value""
FROM ""DirectMessagesFts""
INNER JOIN ""DirectMessages"" dm ON dm.rowid = ""DirectMessagesFts"".rowid
WHERE ""DirectMessagesFts"" MATCH {{0}}
    AND (dm.""SenderId"" = {{1}} OR dm.""RecipientId"" = {{1}})";

        var totalEstimate = await _context.Database
            .SqlQueryRaw<int>(countSql, [query.QueryText, callerUserId])
            .FirstOrDefaultAsync(ct);

        return (resultItems, totalEstimate);
    }

    private static SearchResultItem MapDmResult(DmSearchResultItemRaw r, Guid callerUserId, string snippet)
    {
        var isCallerSender = r.SenderId == callerUserId;
        return new SearchResultItem
        {
            MessageId = r.MessageId,
            Snippet = snippet,
            ChannelId = Guid.Empty,
            ChannelName = "",
            AuthorId = r.SenderId,
            AuthorDisplayName = r.SenderDisplayName,
            CreatedAtUtc = r.CreatedAtUtc,
            RelevanceScore = r.RelevanceScore,
            IsDirectMessage = true,
            OtherParticipantId = isCallerSender ? r.RecipientId : r.SenderId,
            OtherParticipantDisplayName = isCallerSender ? r.RecipientDisplayName : r.SenderDisplayName,
        };
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

    private class DmSearchResultItemRaw
    {
        public Guid MessageId { get; set; }
        public string Snippet { get; set; } = string.Empty;
        public Guid SenderId { get; set; }
        public Guid RecipientId { get; set; }
        public string SenderDisplayName { get; set; } = string.Empty;
        public string RecipientDisplayName { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
        public double RelevanceScore { get; set; }
    }
}
