using System.Security.Claims;
using HotBox.Application.Models;
using HotBox.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotBox.Application.Controllers;

[ApiController]
[Route("api/dm")]
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

    [HttpGet]
    public async Task<IActionResult> GetConversations(CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        _logger.LogDebug("Listing DM conversations for user {UserId}", userId.Value);

        var conversations = await _directMessageService.GetConversationsAsync(userId.Value, ct);
        var unreadCounts = await _readStateService.GetDmUnreadCountsAsync(userId.Value, ct);

        var response = conversations.Select(c => new ConversationSummaryResponse
        {
            UserId = c.UserId,
            DisplayName = c.DisplayName,
            LastMessageContent = string.Empty,
            LastMessageAtUtc = c.LastMessageAtUtc,
            UnreadCount = unreadCounts.GetValueOrDefault(c.UserId, 0),
        }).ToList();

        return Ok(response);
    }

    [HttpGet("{userId:guid}")]
    public async Task<IActionResult> GetConversation(
        Guid userId,
        [FromQuery] DateTime? before = null,
        [FromQuery] int limit = 50,
        CancellationToken ct = default)
    {
        if (limit is < 1 or > 100)
        {
            return BadRequest(new { error = "Limit must be between 1 and 100." });
        }

        var currentUserId = GetUserId();
        if (currentUserId is null)
        {
            return Unauthorized();
        }

        _logger.LogDebug(
            "Fetching DM conversation between {CurrentUserId} and {OtherUserId}",
            currentUserId.Value,
            userId);

        var messages = await _directMessageService.GetConversationAsync(
            currentUserId.Value, userId, before, limit, ct);

        var response = messages.Select(m => new DirectMessageResponse
        {
            Id = m.Id,
            Content = m.Content,
            SenderId = m.SenderId,
            SenderDisplayName = m.Sender?.DisplayName ?? "Unknown",
            RecipientId = m.RecipientId,
            CreatedAtUtc = m.CreatedAtUtc,
            ReadAtUtc = m.ReadAtUtc,
        }).ToList();

        return Ok(response);
    }

    [HttpPost("{userId:guid}")]
    public async Task<IActionResult> SendMessage(
        Guid userId,
        [FromBody] SendDirectMessageRequest request,
        CancellationToken ct)
    {
        var currentUserId = GetUserId();
        if (currentUserId is null)
        {
            return Unauthorized();
        }

        _logger.LogDebug(
            "User {SenderId} sending DM to {RecipientId}",
            currentUserId.Value,
            userId);

        try
        {
            var message = await _directMessageService.SendAsync(
                currentUserId.Value, userId, request.Content, ct);

            var response = new DirectMessageResponse
            {
                Id = message.Id,
                Content = message.Content,
                SenderId = message.SenderId,
                SenderDisplayName = message.Sender?.DisplayName ?? "Unknown",
                RecipientId = message.RecipientId,
                CreatedAtUtc = message.CreatedAtUtc,
                ReadAtUtc = message.ReadAtUtc,
            };

            return CreatedAtAction(
                nameof(GetConversation),
                new { userId },
                response);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{userId:guid}/read")]
    public async Task<IActionResult> MarkAsRead(Guid userId, CancellationToken ct)
    {
        var currentUserId = GetUserId();
        if (currentUserId is null)
        {
            return Unauthorized();
        }

        await _readStateService.MarkDmAsReadAsync(currentUserId.Value, userId, ct);
        return NoContent();
    }

    [HttpGet("unread")]
    public async Task<IActionResult> GetUnreadCounts(CancellationToken ct)
    {
        var currentUserId = GetUserId();
        if (currentUserId is null)
        {
            return Unauthorized();
        }

        var unreadCounts = await _readStateService.GetDmUnreadCountsAsync(currentUserId.Value, ct);
        return Ok(unreadCounts);
    }

    private Guid? GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return null;
        }

        return userId;
    }
}
