using System.Security.Claims;
using HotBox.Application.Models;
using HotBox.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotBox.Application.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class InvitesController : ControllerBase
{
    private readonly IInviteService _inviteService;
    private readonly ILogger<InvitesController> _logger;

    public InvitesController(IInviteService inviteService, ILogger<InvitesController> logger)
    {
        _inviteService = inviteService;
        _logger = logger;
    }

    [HttpGet]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult<IReadOnlyList<InviteResponse>>> GetAll(CancellationToken ct)
    {
        var invites = await _inviteService.GetAllAsync(ct);
        var response = invites.Select(MapToResponse).ToList();
        return Ok(response);
    }

    [HttpPost]
    [Authorize(Policy = "Moderator")]
    public async Task<ActionResult<InviteResponse>> Create(
        [FromBody] CreateInviteRequest request,
        CancellationToken ct)
    {
        var userId = GetUserId();
        var invite = await _inviteService.GenerateAsync(userId, request.ExpiresAt, request.MaxUses, ct);

        _logger.LogInformation("User {UserId} created invite {InviteCode}", userId, invite.Code);

        return CreatedAtAction(nameof(GetAll), MapToResponse(invite));
    }

    [HttpDelete("{code}")]
    [Authorize(Policy = "Moderator")]
    public async Task<IActionResult> Revoke(string code, CancellationToken ct)
    {
        var userId = GetUserId();
        var revoked = await _inviteService.RevokeAsync(code, ct);

        if (!revoked)
        {
            return NotFound("Invite not found.");
        }

        _logger.LogInformation("User {UserId} revoked invite {InviteCode}", userId, code);

        return NoContent();
    }

    [HttpGet("validate/{code}")]
    [AllowAnonymous]
    public async Task<ActionResult<ValidateInviteResponse>> Validate(string code, CancellationToken ct)
    {
        // Peek at invite without consuming it
        var invites = await _inviteService.GetAllAsync(ct);
        var invite = invites.FirstOrDefault(i => i.Code == code && !i.IsRevoked);

        if (invite is null)
        {
            return Ok(new ValidateInviteResponse(false, code));
        }

        if (invite.ExpiresAt.HasValue && invite.ExpiresAt.Value < DateTime.UtcNow)
        {
            return Ok(new ValidateInviteResponse(false, code));
        }

        if (invite.MaxUses.HasValue && invite.UseCount >= invite.MaxUses.Value)
        {
            return Ok(new ValidateInviteResponse(false, code));
        }

        return Ok(new ValidateInviteResponse(true, code));
    }

    private Guid GetUserId()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier)
                        ?? throw new UnauthorizedAccessException("User ID not found in claims.");
        return Guid.Parse(userIdStr);
    }

    private static InviteResponse MapToResponse(Core.Entities.Invite invite)
    {
        return new InviteResponse(
            invite.Id,
            invite.Code,
            invite.CreatedById,
            invite.CreatedAt,
            invite.ExpiresAt,
            invite.MaxUses,
            invite.UseCount,
            invite.IsRevoked);
    }
}
