using HotBox.Core.Enums;
using HotBox.Core.Interfaces;
using HotBox.Core.Models;
using HotBox.Core.Options;
using HotBox.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HotBox.Infrastructure.Services;

public class SearchService : ISearchService
{
    private readonly HotBoxDbContext _dbContext;
    private readonly SearchOptions _searchOptions;
    private readonly ILogger<SearchService> _logger;

    public SearchService(
        HotBoxDbContext dbContext,
        IOptions<SearchOptions> searchOptions,
        ILogger<SearchService> logger)
    {
        _dbContext = dbContext;
        _searchOptions = searchOptions.Value;
        _logger = logger;
    }

    public bool IsFullTextSearchAvailable => true;

    public string ProviderName => "PostgreSQL tsvector";

    public async Task<SearchResult> SearchMessagesAsync(SearchQuery query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query.QueryText) || query.QueryText.Length < _searchOptions.MinQueryLength)
        {
            return new SearchResult { Items = [], TotalEstimate = 0 };
        }

        var limit = Math.Min(query.Limit, _searchOptions.MaxResults);
        var tsQuery = ToTsQueryString(query.QueryText);
        var result = new SearchResult();

        switch (query.Scope)
        {
            case SearchScope.All:
                var channelResults = await SearchChannelMessagesAsync(query, tsQuery, limit, ct);
                var dmResults = await SearchDirectMessagesAsync(query, tsQuery, limit, ct);
                var combined = channelResults.Concat(dmResults)
                    .OrderByDescending(r => r.RelevanceScore)
                    .ThenByDescending(r => r.CreatedAt)
                    .Take(limit)
                    .ToList();
                result.Items = combined;
                result.TotalEstimate = combined.Count;
                break;

            case SearchScope.Channels:
                result.Items = await SearchChannelMessagesAsync(query, tsQuery, limit, ct);
                result.TotalEstimate = result.Items.Count;
                break;

            case SearchScope.DirectMessages:
                result.Items = await SearchDirectMessagesAsync(query, tsQuery, limit, ct);
                result.TotalEstimate = result.Items.Count;
                break;
        }

        _logger.LogDebug("Search for {QueryText} returned {Count} results", query.QueryText, result.Items.Count);

        return result;
    }

    public Task InitializeIndexAsync(CancellationToken ct = default)
    {
        // PostgreSQL tsvector columns are maintained automatically via generated columns
        _logger.LogInformation("PostgreSQL full-text search indexes are managed via generated columns");
        return Task.CompletedTask;
    }

    public Task ReindexAsync(CancellationToken ct = default)
    {
        // Generated tsvector columns are maintained automatically by PostgreSQL
        _logger.LogInformation("PostgreSQL generated tsvector columns do not require manual reindexing");
        return Task.CompletedTask;
    }

    private async Task<List<SearchResultItem>> SearchChannelMessagesAsync(
        SearchQuery query,
        string tsQuery,
        int limit,
        CancellationToken ct)
    {
        var messagesQuery = _dbContext.Messages
            .Include(m => m.User)
            .Include(m => m.Channel)
            .Where(m => EF.Functions.ToTsVector("english", m.Content)
                .Matches(EF.Functions.ToTsQuery("english", tsQuery)));

        if (query.ChannelId.HasValue)
        {
            messagesQuery = messagesQuery.Where(m => m.ChannelId == query.ChannelId.Value);
        }

        if (query.SenderId.HasValue)
        {
            messagesQuery = messagesQuery.Where(m => m.UserId == query.SenderId.Value);
        }

        var messages = await messagesQuery
            .OrderByDescending(m => m.CreatedAt)
            .Take(limit)
            .AsNoTracking()
            .ToListAsync(ct);

        return messages.Select(m => new SearchResultItem
        {
            MessageId = m.Id,
            Snippet = TruncateSnippet(m.Content),
            ChannelId = m.ChannelId,
            ChannelName = m.Channel.Name,
            AuthorId = m.UserId,
            AuthorDisplayName = m.User.DisplayName,
            CreatedAt = m.CreatedAt,
            RelevanceScore = 1.0,
            IsDirectMessage = false
        }).ToList();
    }

    private async Task<List<SearchResultItem>> SearchDirectMessagesAsync(
        SearchQuery query,
        string tsQuery,
        int limit,
        CancellationToken ct)
    {
        if (query.CallerUserId is null)
        {
            return [];
        }

        var callerUserId = query.CallerUserId.Value;

        var dmQuery = _dbContext.DirectMessages
            .Include(dm => dm.Sender)
            .Include(dm => dm.Recipient)
            .Where(dm =>
                (dm.SenderId == callerUserId || dm.RecipientId == callerUserId) &&
                EF.Functions.ToTsVector("english", dm.Content)
                    .Matches(EF.Functions.ToTsQuery("english", tsQuery)));

        if (query.SenderId.HasValue)
        {
            dmQuery = dmQuery.Where(dm => dm.SenderId == query.SenderId.Value);
        }

        var messages = await dmQuery
            .OrderByDescending(dm => dm.CreatedAt)
            .Take(limit)
            .AsNoTracking()
            .ToListAsync(ct);

        return messages.Select(dm =>
        {
            var otherUser = dm.SenderId == callerUserId ? dm.Recipient : dm.Sender;
            return new SearchResultItem
            {
                MessageId = dm.Id,
                Snippet = TruncateSnippet(dm.Content),
                ChannelId = Guid.Empty,
                ChannelName = string.Empty,
                AuthorId = dm.SenderId,
                AuthorDisplayName = dm.Sender.DisplayName,
                CreatedAt = dm.CreatedAt,
                RelevanceScore = 1.0,
                IsDirectMessage = true,
                OtherParticipantId = otherUser.Id,
                OtherParticipantDisplayName = otherUser.DisplayName
            };
        }).ToList();
    }

    private string TruncateSnippet(string content)
    {
        if (content.Length <= _searchOptions.SnippetLength)
        {
            return content;
        }

        return string.Concat(content.AsSpan(0, _searchOptions.SnippetLength), "...");
    }

    private static string ToTsQueryString(string input)
    {
        // Split on whitespace, filter empty, join with & for AND matching
        var terms = input.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join(" & ", terms.Select(t => t.Replace("'", "''")));
    }
}
