using System.Security.Claims;
using HotBox.Application.Hubs;
using HotBox.Application.Models;
using HotBox.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace HotBox.Application.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public class MessagesController : ControllerBase
{
    private readonly IMessageService _messageService;
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly ILogger<MessagesController> _logger;

    public MessagesController(
        IMessageService messageService,
        IHubContext<ChatHub> hubContext,
        ILogger<MessagesController> logger)
    {
        _messageService = messageService;
        _hubContext = hubContext;
        _logger = logger;
    }

    [HttpGet("channels/{channelId:guid}/messages")]
    public async Task<IActionResult> GetByChannel(
        Guid channelId,
        [FromQuery] DateTime? before = null,
        [FromQuery] int limit = 50,
        CancellationToken ct = default)
    {
        if (limit is < 1 or > 100)
        {
            return BadRequest(new { error = "Limit must be between 1 and 100." });
        }

        var messages = await _messageService.GetByChannelAsync(channelId, before, limit, ct);

        var response = messages.Select(m => new MessageResponse
        {
            Id = m.Id,
            Content = m.Content,
            ChannelId = m.ChannelId,
            AuthorId = m.AuthorId,
            AuthorDisplayName = m.Author?.DisplayName ?? "Unknown",
            CreatedAtUtc = m.CreatedAtUtc,
            EditedAtUtc = m.EditedAtUtc,
        }).ToList();

        return Ok(response);
    }

    [HttpGet("messages/{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var message = await _messageService.GetByIdAsync(id, ct);
        if (message is null)
        {
            return NotFound(new { error = $"Message {id} not found." });
        }

        var response = new MessageResponse
        {
            Id = message.Id,
            Content = message.Content,
            ChannelId = message.ChannelId,
            AuthorId = message.AuthorId,
            AuthorDisplayName = message.Author?.DisplayName ?? "Unknown",
            CreatedAtUtc = message.CreatedAtUtc,
            EditedAtUtc = message.EditedAtUtc,
        };

        return Ok(response);
    }

    [HttpPost("channels/{channelId:guid}/messages")]
    public async Task<IActionResult> SendMessage(
        Guid channelId,
        [FromBody] SendMessageRequest request,
        CancellationToken ct)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new { error = "Unable to determine user identity." });
        }

        try
        {
            var message = await _messageService.SendAsync(channelId, userId, request.Content, ct);

            var response = new MessageResponse
            {
                Id = message.Id,
                Content = message.Content,
                ChannelId = message.ChannelId,
                AuthorId = message.AuthorId,
                AuthorDisplayName = message.Author?.DisplayName ?? "Unknown",
                CreatedAtUtc = message.CreatedAtUtc,
                EditedAtUtc = message.EditedAtUtc,
            };

            await _hubContext.Clients.Group(channelId.ToString())
                .SendAsync("ReceiveMessage", response, ct);

            return CreatedAtAction(nameof(GetById), new { id = message.Id }, response);
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
}
