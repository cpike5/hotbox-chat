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
            _logger.LogInformation("Initializing PostgreSQL GIN indexes on Messages.Content and DirectMessages.Content");

            await _context.Database.ExecuteSqlRawAsync(
                """
                CREATE INDEX IF NOT EXISTS "IX_Messages_Content_FTS"
                ON "Messages" USING GIN (to_tsvector('english', "Content"))
                """,
                ct);

            await _context.Database.ExecuteSqlRawAsync(
                """
                CREATE INDEX IF NOT EXISTS "IX_DirectMessages_Content_FTS"
                ON "DirectMessages" USING GIN (to_tsvector('english', "Content"))
                """,
                ct);

            _logger.LogInformation("PostgreSQL GIN indexes initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize PostgreSQL GIN indexes");
            throw;
        }
    }

    public async Task ReindexAsync(CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Rebuilding PostgreSQL GIN indexes");

            await _context.Database.ExecuteSqlRawAsync(
                """REINDEX INDEX "IX_Messages_Content_FTS" """,
                ct);

            await _context.Database.ExecuteSqlRawAsync(
                """REINDEX INDEX "IX_DirectMessages_Content_FTS" """,
                ct);

            _logger.LogInformation("PostgreSQL GIN indexes rebuilt successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rebuild PostgreSQL GIN indexes");
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
            _logger.LogError(ex, "PostgreSQL full-text search failed for query {Query}", query.QueryText);
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

        // Count
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
    ts_headline('english', dm.""Content"", plainto_tsquery('english', {{0}}),
        'StartSel=\x01, StopSel=\x02, MaxFragments=1, MaxWords=50, MinWords=20') AS ""Snippet"",
    dm.""SenderId"",
    dm.""RecipientId"",
    sender.""DisplayName"" AS ""SenderDisplayName"",
    recipient.""DisplayName"" AS ""RecipientDisplayName"",
    dm.""CreatedAtUtc"",
    ts_rank(to_tsvector('english', dm.""Content""), plainto_tsquery('english', {{0}})) AS ""RelevanceScore""
FROM ""DirectMessages"" dm
INNER JOIN ""AspNetUsers"" sender ON sender.""Id"" = dm.""SenderId""
INNER JOIN ""AspNetUsers"" recipient ON recipient.""Id"" = dm.""RecipientId""
WHERE to_tsvector('english', dm.""Content"") @@ plainto_tsquery('english', {{0}})
    AND (dm.""SenderId"" = {{1}} OR dm.""RecipientId"" = {{1}})
ORDER BY ""RelevanceScore"" DESC, dm.""CreatedAtUtc"" DESC
OFFSET {{{offsetParamIndex}}} ROWS FETCH NEXT {{{limitParamIndex}}} ROWS ONLY";

        var items = await _context.Database
            .SqlQueryRaw<DmSearchResultItemRaw>(sql, parameters.ToArray())
            .ToListAsync(ct);

        var resultItems = items
            .Select(r => MapDmResult(r, callerUserId, SanitizeSnippet(r.Snippet)))
            .ToList();

        // Count
        var countSql = @$"SELECT COUNT(*)::int AS ""Value""
FROM ""DirectMessages"" dm
WHERE to_tsvector('english', dm.""Content"") @@ plainto_tsquery('english', {{0}})
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
