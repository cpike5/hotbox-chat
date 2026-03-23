using System.Security.Claims;
using HotBox.Application.Models;
using HotBox.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotBox.Application.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DirectMessagesController : ControllerBase
{
    private readonly IDirectMessageService _directMessageService;
    private readonly IReadStateService _readStateService;
    private readonly ILogger<DirectMessagesController> _logger;

    public DirectMessagesController(
        IDirectMessageService directMessageService,
        IReadStateService readStateService,
        ILogger<DirectMessagesController> logger)
    {
        _directMessageService = directMessageService;
        _readStateService = readStateService;
        _logger = logger;
    }

    [HttpGet("conversations")]
    public async Task<ActionResult<IReadOnlyList<ConversationSummaryResponse>>> GetConversations(CancellationToken ct)
    {
        var userId = GetUserId();
        var conversations = await _directMessageService.GetConversationsAsync(userId, ct);
        var unreadCounts = await _readStateService.GetDmUnreadCountsAsync(userId, ct);

        var response = conversations.Select(c => new ConversationSummaryResponse(
            c.UserId,
            c.DisplayName,
            c.LastMessageAt,
            c.LastMessageContent,
            unreadCounts.GetValueOrDefault(c.UserId, 0)
        )).ToList();

        return Ok(response);
    }

    [HttpGet("{otherUserId:guid}")]
    public async Task<ActionResult<IReadOnlyList<DirectMessageResponse>>> GetConversation(
        Guid otherUserId,
        [FromQuery] DateTime? before,
        [FromQuery] int limit = 50,
        CancellationToken ct = default)
    {
        var userId = GetUserId();
        var messages = await _directMessageService.GetConversationAsync(userId, otherUserId, before, limit, ct);
        var response = messages.Select(MapToResponse).ToList();
        return Ok(response);
    }

    [HttpPost]
    public async Task<ActionResult<DirectMessageResponse>> Send(
        [FromBody] SendDirectMessageRequest request,
        CancellationToken ct)
    {
        var userId = GetUserId();
        var dm = await _directMessageService.SendAsync(userId, request.RecipientId, request.Content, ct);

        _logger.LogDebug("User {UserId} sent DM {MessageId} to {RecipientId}", userId, dm.Id, request.RecipientId);

        return CreatedAtAction(nameof(GetConversation), new { otherUserId = request.RecipientId }, MapToResponse(dm));
    }

    [HttpPost("{otherUserId:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid otherUserId, CancellationToken ct)
    {
        var userId = GetUserId();
        await _readStateService.MarkDmAsReadAsync(userId, otherUserId, ct);
        return Ok();
    }

    [HttpGet("unread")]
    public async Task<ActionResult<Dictionary<Guid, int>>> GetUnreadCounts(CancellationToken ct)
    {
        var userId = GetUserId();
        var counts = await _readStateService.GetDmUnreadCountsAsync(userId, ct);
        return Ok(counts);
    }

    private Guid GetUserId()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier)
                        ?? throw new UnauthorizedAccessException("User ID not found in claims.");
        return Guid.Parse(userIdStr);
    }

    private static DirectMessageResponse MapToResponse(Core.Entities.DirectMessage dm)
    {
        return new DirectMessageResponse(
            dm.Id,
            dm.Content,
            dm.SenderId,
            dm.Sender.DisplayName,
            dm.Sender.AvatarUrl,
            dm.RecipientId,
            dm.Recipient.DisplayName,
            dm.CreatedAt,
            dm.EditedAt,
            dm.ReadAt);
    }
}
