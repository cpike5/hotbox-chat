using System.Security.Claims;
using HotBox.Application.Models;
using HotBox.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotBox.Application.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MessagesController : ControllerBase
{
    private readonly IMessageService _messageService;
    private readonly ILogger<MessagesController> _logger;

    public MessagesController(IMessageService messageService, ILogger<MessagesController> logger)
    {
        _messageService = messageService;
        _logger = logger;
    }

    [HttpGet("channel/{channelId:guid}")]
    public async Task<ActionResult<IReadOnlyList<MessageResponse>>> GetByChannel(
        Guid channelId,
        [FromQuery] DateTime? before,
        [FromQuery] int limit = 50,
        CancellationToken ct = default)
    {
        var messages = await _messageService.GetByChannelAsync(channelId, before, limit, ct);
        var response = messages.Select(MapToResponse).ToList();
        return Ok(response);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MessageResponse>> GetById(Guid id, CancellationToken ct)
    {
        var message = await _messageService.GetByIdAsync(id, ct);
        if (message is null)
        {
            return NotFound();
        }

        return Ok(MapToResponse(message));
    }

    [HttpPost("channel/{channelId:guid}")]
    public async Task<ActionResult<MessageResponse>> Send(
        Guid channelId,
        [FromBody] SendMessageRequest request,
        CancellationToken ct)
    {
        var userId = GetUserId();
        var message = await _messageService.SendAsync(channelId, userId, request.Content, ct);

        _logger.LogDebug("User {UserId} sent message {MessageId} to channel {ChannelId}", userId, message.Id, channelId);

        return CreatedAtAction(nameof(GetById), new { id = message.Id }, MapToResponse(message));
    }

    private Guid GetUserId()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier)
                        ?? throw new UnauthorizedAccessException("User ID not found in claims.");
        return Guid.Parse(userIdStr);
    }

    private static MessageResponse MapToResponse(Core.Entities.Message message)
    {
        return new MessageResponse(
            message.Id,
            message.Content,
            message.ChannelId,
            message.UserId,
            message.User.DisplayName,
            message.User.AvatarUrl,
            message.User.IsAgent,
            message.CreatedAt,
            message.EditedAt);
    }
}
