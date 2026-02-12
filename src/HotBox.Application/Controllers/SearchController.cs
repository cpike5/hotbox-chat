using HotBox.Application.Models;
using HotBox.Core.Interfaces;
using HotBox.Core.Models;
using HotBox.Core.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace HotBox.Application.Controllers;

[ApiController]
[Route("api/search")]
[Authorize]
public class SearchController : ControllerBase
{
    private readonly ISearchService _searchService;
    private readonly ILogger<SearchController> _logger;
    private readonly SearchOptions _options;

    public SearchController(
        ISearchService searchService,
        ILogger<SearchController> logger,
        IOptions<SearchOptions> options)
    {
        _searchService = searchService;
        _logger = logger;
        _options = options.Value;
    }

    [HttpGet("messages")]
    public async Task<IActionResult> SearchMessages(
        [FromQuery(Name = "q")] string? query,
        [FromQuery] Guid? channelId = null,
        [FromQuery] string? cursor = null,
        [FromQuery] int? limit = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return BadRequest(new { error = "Query parameter 'q' is required." });
        }

        if (query.Length < _options.MinQueryLength)
        {
            return BadRequest(new { error = $"Query must be at least {_options.MinQueryLength} characters." });
        }

        var effectiveLimit = limit ?? _options.DefaultLimit;
        if (effectiveLimit is < 1 or > 100)
        {
            return BadRequest(new { error = "Limit must be between 1 and 100." });
        }

        var searchQuery = new SearchQuery
        {
            QueryText = query,
            ChannelId = channelId,
            Cursor = cursor,
            Limit = effectiveLimit,
        };

        _logger.LogInformation(
            "Search request: Query={Query}, ChannelId={ChannelId}, Cursor={Cursor}, Limit={Limit}",
            query, channelId, cursor, effectiveLimit);

        var result = await _searchService.SearchMessagesAsync(searchQuery, ct);

        var response = new SearchResultResponse
        {
            Items = result.Items.Select(item => new SearchResultItemResponse
            {
                MessageId = item.MessageId,
                Snippet = item.Snippet,
                ChannelId = item.ChannelId,
                ChannelName = item.ChannelName,
                AuthorId = item.AuthorId,
                AuthorDisplayName = item.AuthorDisplayName,
                CreatedAtUtc = item.CreatedAtUtc,
                RelevanceScore = item.RelevanceScore,
            }).ToList(),
            Cursor = result.Cursor,
            TotalEstimate = result.TotalEstimate,
        };

        return Ok(response);
    }

    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        var response = new SearchStatusResponse
        {
            IsFullTextSearchAvailable = _searchService.IsFullTextSearchAvailable,
            ProviderName = _searchService.ProviderName,
        };

        return Ok(response);
    }

    [HttpPost("reindex")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Reindex(CancellationToken ct)
    {
        _logger.LogInformation("Admin-initiated search reindex started using provider {ProviderName}",
            _searchService.ProviderName);

        try
        {
            await _searchService.ReindexAsync(ct);

            _logger.LogInformation("Admin-initiated search reindex completed successfully");

            return Ok(new { message = "Search index rebuild completed.", provider = _searchService.ProviderName });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Search reindex failed");
            return StatusCode(500, new { error = "Search reindex failed. Check server logs for details." });
        }
    }
}
