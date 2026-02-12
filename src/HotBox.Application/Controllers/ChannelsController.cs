using System.Security.Claims;
using HotBox.Application.Models;
using HotBox.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotBox.Application.Controllers;

[ApiController]
[Route("api/channels")]
[Authorize]
public class ChannelsController : ControllerBase
{
    private readonly IChannelService _channelService;
    private readonly ILogger<ChannelsController> _logger;

    public ChannelsController(
        IChannelService channelService,
        ILogger<ChannelsController> logger)
    {
        _channelService = channelService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var channels = await _channelService.GetAllAsync(ct);

        var response = channels.Select(c => new ChannelResponse
        {
            Id = c.Id,
            Name = c.Name,
            Topic = c.Topic,
            Type = c.Type,
            SortOrder = c.SortOrder,
            CreatedAtUtc = c.CreatedAtUtc,
        }).ToList();

        return Ok(response);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var channel = await _channelService.GetByIdAsync(id, ct);
        if (channel is null)
        {
            return NotFound(new { error = $"Channel {id} not found." });
        }

        var response = new ChannelResponse
        {
            Id = channel.Id,
            Name = channel.Name,
            Topic = channel.Topic,
            Type = channel.Type,
            SortOrder = channel.SortOrder,
            CreatedAtUtc = channel.CreatedAtUtc,
        };

        return Ok(response);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateChannelRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized(new { error = "Unable to determine user identity." });
        }

        try
        {
            var channel = await _channelService.CreateAsync(
                userId.Value, request.Name, request.Topic, request.Type, ct);

            var response = new ChannelResponse
            {
                Id = channel.Id,
                Name = channel.Name,
                Topic = channel.Topic,
                Type = channel.Type,
                SortOrder = channel.SortOrder,
                CreatedAtUtc = channel.CreatedAtUtc,
            };

            return CreatedAtAction(nameof(GetById), new { id = channel.Id }, response);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Channel creation denied for user {UserId}: {Reason}", userId, ex.Message);
            return StatusCode(403, new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Channel creation failed: {Reason}", ex.Message);
            return BadRequest(new { error = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateChannelRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized(new { error = "Unable to determine user identity." });
        }

        try
        {
            await _channelService.UpdateAsync(userId.Value, id, request.Name, request.Topic, ct);
            return Ok(new { message = "Channel updated." });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Channel update denied for user {UserId}: {Reason}", userId, ex.Message);
            return StatusCode(403, new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Channel update failed for channel {ChannelId}: {Reason}", id, ex.Message);
            return BadRequest(new { error = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized(new { error = "Unable to determine user identity." });
        }

        try
        {
            await _channelService.DeleteAsync(userId.Value, id, ct);
            return Ok(new { message = "Channel deleted." });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Channel deletion denied for user {UserId}: {Reason}", userId, ex.Message);
            return StatusCode(403, new { error = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning("Channel deletion failed — channel {ChannelId} not found", id);
            return NotFound(new { error = ex.Message });
        }
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
