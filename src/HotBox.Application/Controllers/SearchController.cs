using System.Security.Claims;
using HotBox.Application.Models;
using HotBox.Core.Enums;
using HotBox.Core.Interfaces;
using HotBox.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotBox.Application.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SearchController : ControllerBase
{
    private readonly ISearchService _searchService;
    private readonly ILogger<SearchController> _logger;

    public SearchController(ISearchService searchService, ILogger<SearchController> logger)
    {
        _searchService = searchService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<SearchResultResponse>> Search(
        [FromQuery] string q,
        [FromQuery] Guid? channelId,
        [FromQuery] Guid? senderId,
        [FromQuery] string? cursor,
        [FromQuery] int limit = 20,
        [FromQuery] SearchScope scope = SearchScope.All,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
        {
            return BadRequest("Query must be at least 2 characters.");
        }

        var userId = GetUserId();

        var query = new SearchQuery
        {
            QueryText = q,
            ChannelId = channelId,
            SenderId = senderId,
            Cursor = cursor,
            Limit = Math.Min(limit, 50),
            Scope = scope,
            CallerUserId = userId
        };

        var result = await _searchService.SearchMessagesAsync(query, ct);

        var response = new SearchResultResponse(
            result.Items.Select(i => new SearchResultItemResponse(
                i.MessageId,
                i.Snippet,
                i.ChannelId,
                i.ChannelName,
                i.AuthorId,
                i.AuthorDisplayName,
                i.CreatedAt,
                i.RelevanceScore,
                i.IsDirectMessage,
                i.OtherParticipantId,
                i.OtherParticipantDisplayName
            )).ToList(),
            result.Cursor,
            result.TotalEstimate);

        return Ok(response);
    }

    [HttpGet("status")]
    public ActionResult<SearchStatusResponse> GetStatus()
    {
        return Ok(new SearchStatusResponse(
            _searchService.IsFullTextSearchAvailable,
            _searchService.ProviderName));
    }

    [HttpPost("reindex")]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> Reindex(CancellationToken ct)
    {
        _logger.LogInformation("Admin triggered search reindex");
        await _searchService.ReindexAsync(ct);
        return Ok();
    }

    private Guid GetUserId()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier)
                        ?? throw new UnauthorizedAccessException("User ID not found in claims.");
        return Guid.Parse(userIdStr);
    }
}
