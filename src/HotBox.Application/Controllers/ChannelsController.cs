using System.Security.Claims;
using HotBox.Application.Models;
using HotBox.Core.Enums;
using HotBox.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotBox.Application.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ChannelsController : ControllerBase
{
    private readonly IChannelService _channelService;
    private readonly ILogger<ChannelsController> _logger;

    public ChannelsController(IChannelService channelService, ILogger<ChannelsController> logger)
    {
        _channelService = channelService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ChannelResponse>>> GetAll(CancellationToken ct)
    {
        var channels = await _channelService.GetAllAsync(ct);
        var response = channels.Select(MapToResponse).ToList();
        return Ok(response);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ChannelResponse>> GetById(Guid id, CancellationToken ct)
    {
        var channel = await _channelService.GetByIdAsync(id, ct);
        if (channel is null)
        {
            return NotFound();
        }

        return Ok(MapToResponse(channel));
    }

    [HttpGet("type/{type}")]
    public async Task<ActionResult<IReadOnlyList<ChannelResponse>>> GetByType(ChannelType type, CancellationToken ct)
    {
        var channels = await _channelService.GetByTypeAsync(type, ct);
        var response = channels.Select(MapToResponse).ToList();
        return Ok(response);
    }

    [HttpPost]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult<ChannelResponse>> Create([FromBody] CreateChannelRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        var channel = await _channelService.CreateAsync(userId, request.Name, request.Description, request.Type, ct);

        _logger.LogInformation("User {UserId} created channel {ChannelId} ({ChannelName})", userId, channel.Id, channel.Name);

        return CreatedAtAction(nameof(GetById), new { id = channel.Id }, MapToResponse(channel));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateChannelRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        await _channelService.UpdateAsync(userId, id, request.Name, request.Description, ct);

        _logger.LogInformation("User {UserId} updated channel {ChannelId}", userId, id);

        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        await _channelService.DeleteAsync(userId, id, ct);

        _logger.LogInformation("User {UserId} deleted channel {ChannelId}", userId, id);

        return NoContent();
    }

    private Guid GetUserId()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier)
                        ?? throw new UnauthorizedAccessException("User ID not found in claims.");
        return Guid.Parse(userIdStr);
    }

    private static ChannelResponse MapToResponse(Core.Entities.Channel channel)
    {
        return new ChannelResponse(
            channel.Id,
            channel.Name,
            channel.Description,
            channel.Type,
            channel.SortOrder,
            channel.CreatedAt,
            channel.CreatedById);
    }
}
