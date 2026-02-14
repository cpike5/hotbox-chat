using System.Security.Claims;
using HotBox.Application.Models;
using HotBox.Core.Enums;
using HotBox.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotBox.Application.Controllers;

[ApiController]
[Route("api/notifications")]
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
    public async Task<IActionResult> GetNotifications(
        [FromQuery] DateTime? before = null,
        [FromQuery] int limit = 50,
        CancellationToken ct = default)
    {
        if (limit is < 1 or > 100)
            return BadRequest(new { error = "Limit must be between 1 and 100." });

        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var notifications = await _notificationRepository.GetByRecipientAsync(userId.Value, before, limit, ct);

        var response = notifications.Select(n => new NotificationResponse
        {
            Id = n.Id,
            Type = n.Type,
            SenderId = n.SenderId,
            SenderDisplayName = n.Sender?.DisplayName ?? "Unknown",
            MessagePreview = ExtractPreviewFromPayload(n.PayloadJson),
            SourceId = n.SourceId,
            SourceType = n.SourceType,
            SourceName = ExtractSourceNameFromPayload(n.PayloadJson),
            CreatedAtUtc = n.CreatedAtUtc,
            ReadAtUtc = n.ReadAtUtc
        }).ToList();

        return Ok(response);
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount(CancellationToken ct = default)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var count = await _notificationRepository.GetUnreadCountAsync(userId.Value, ct);
        return Ok(count);
    }

    [HttpPost("mark-all-read")]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken ct = default)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        await _notificationRepository.MarkAllAsReadAsync(userId.Value, ct);
        return NoContent();
    }

    [HttpGet("preferences")]
    public async Task<IActionResult> GetPreferences(CancellationToken ct = default)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var prefs = await _notificationRepository.GetPreferencesAsync(userId.Value, ct);
        var response = prefs.Select(p => new
        {
            p.SourceType,
            p.SourceId,
            p.IsMuted
        }).ToList();

        return Ok(response);
    }

    [HttpPut("preferences")]
    public async Task<IActionResult> SetPreference(
        [FromBody] SetPreferenceRequest request,
        CancellationToken ct = default)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        await _notificationRepository.SetMutePreferenceAsync(
            userId.Value, request.SourceType, request.SourceId, request.IsMuted, ct);

        return NoContent();
    }

    private Guid? GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            return null;
        return userId;
    }

    private static string ExtractPreviewFromPayload(string payloadJson)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(payloadJson);
            if (doc.RootElement.TryGetProperty("messagePreview", out var prop))
                return prop.GetString() ?? string.Empty;
        }
        catch { }
        return string.Empty;
    }

    private static string ExtractSourceNameFromPayload(string payloadJson)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(payloadJson);
            if (doc.RootElement.TryGetProperty("sourceName", out var prop))
                return prop.GetString() ?? string.Empty;
        }
        catch { }
        return string.Empty;
    }
}

public class SetPreferenceRequest
{
    public NotificationSourceType SourceType { get; set; }
    public Guid SourceId { get; set; }
    public bool IsMuted { get; set; }
}
