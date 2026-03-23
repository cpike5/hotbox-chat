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
public class NotificationsController : ControllerBase
{
    private readonly INotificationRepository _notificationRepository;
    private readonly ILogger<NotificationsController> _logger;

    public NotificationsController(
        INotificationRepository notificationRepository,
        ILogger<NotificationsController> logger)
    {
        _notificationRepository = notificationRepository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<NotificationResponse>>> GetNotifications(
        [FromQuery] DateTime? before,
        [FromQuery] int limit = 50,
        CancellationToken ct = default)
    {
        var userId = GetUserId();
        var notifications = await _notificationRepository.GetByRecipientAsync(userId, before, limit, ct);

        var response = notifications.Select(n => new NotificationResponse(
            n.Id,
            n.Type,
            n.SenderId,
            n.PayloadJson,
            n.SourceId,
            n.SourceType,
            n.CreatedAt,
            n.ReadAt
        )).ToList();

        return Ok(response);
    }

    [HttpGet("unread/count")]
    public async Task<ActionResult<int>> GetUnreadCount(CancellationToken ct)
    {
        var userId = GetUserId();
        var count = await _notificationRepository.GetUnreadCountAsync(userId, ct);
        return Ok(count);
    }

    [HttpPost("read")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        var userId = GetUserId();
        await _notificationRepository.MarkAllAsReadAsync(userId, ct);
        return Ok();
    }

    [HttpGet("preferences")]
    public async Task<ActionResult<IReadOnlyList<object>>> GetPreferences(CancellationToken ct)
    {
        var userId = GetUserId();
        var preferences = await _notificationRepository.GetPreferencesAsync(userId, ct);

        var response = preferences.Select(p => new
        {
            p.Id,
            p.SourceType,
            p.SourceId,
            p.IsMuted,
            p.CreatedAt,
            p.UpdatedAt
        }).ToList();

        return Ok(response);
    }

    [HttpPut("preferences/{sourceType}/{sourceId:guid}")]
    public async Task<IActionResult> SetMutePreference(
        NotificationSourceType sourceType,
        Guid sourceId,
        [FromQuery] bool muted,
        CancellationToken ct)
    {
        var userId = GetUserId();
        await _notificationRepository.SetMutePreferenceAsync(userId, sourceType, sourceId, muted, ct);

        _logger.LogDebug("User {UserId} set mute={Muted} for {SourceType}:{SourceId}", userId, muted, sourceType, sourceId);

        return Ok();
    }

    private Guid GetUserId()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier)
                        ?? throw new UnauthorizedAccessException("User ID not found in claims.");
        return Guid.Parse(userIdStr);
    }
}
