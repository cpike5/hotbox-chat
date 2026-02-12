using HotBox.Application.Models;
using HotBox.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotBox.Application.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public class MessagesController : ControllerBase
{
    private readonly IMessageService _messageService;
    private readonly ILogger<MessagesController> _logger;

    public MessagesController(
        IMessageService messageService,
        ILogger<MessagesController> logger)
    {
        _messageService = messageService;
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
}
