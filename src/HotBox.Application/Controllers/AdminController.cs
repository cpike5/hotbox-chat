using System.Security.Claims;
using HotBox.Application.Models;
using HotBox.Core.Entities;
using HotBox.Core.Enums;
using HotBox.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace HotBox.Application.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "Admin")]
public class AdminController : ControllerBase
{
    private readonly IServerSettingsService _serverSettingsService;
    private readonly UserManager<AppUser> _userManager;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        IServerSettingsService serverSettingsService,
        UserManager<AppUser> userManager,
        ILogger<AdminController> logger)
    {
        _serverSettingsService = serverSettingsService;
        _userManager = userManager;
        _logger = logger;
    }

    [HttpGet("settings")]
    public async Task<ActionResult<ServerSettingsResponse>> GetSettings(CancellationToken ct)
    {
        var settings = await _serverSettingsService.GetAsync(ct);
        return Ok(new ServerSettingsResponse(settings.Id, settings.ServerName, settings.RegistrationMode));
    }

    [HttpPut("settings")]
    public async Task<ActionResult<ServerSettingsResponse>> UpdateSettings(
        [FromBody] UpdateServerSettingsRequest request,
        CancellationToken ct)
    {
        var adminId = GetUserId();
        var settings = await _serverSettingsService.UpdateAsync(request.ServerName, request.RegistrationMode, ct);

        _logger.LogInformation("Admin {AdminId} updated server settings: Name={ServerName}, RegistrationMode={RegistrationMode}",
            adminId, request.ServerName, request.RegistrationMode);

        return Ok(new ServerSettingsResponse(settings.Id, settings.ServerName, settings.RegistrationMode));
    }

    [HttpPut("users/{userId:guid}/role")]
    public async Task<IActionResult> ChangeUserRole(Guid userId, [FromBody] ChangeUserRoleRequest request)
    {
        var adminId = GetUserId();
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return NotFound("User not found.");
        }

        // Validate the role name
        var validRoles = new[] { nameof(UserRole.Admin), nameof(UserRole.Moderator), nameof(UserRole.Member) };
        if (!validRoles.Contains(request.Role))
        {
            return BadRequest($"Invalid role. Must be one of: {string.Join(", ", validRoles)}");
        }

        // Remove all existing roles
        var currentRoles = await _userManager.GetRolesAsync(user);
        if (currentRoles.Count > 0)
        {
            await _userManager.RemoveFromRolesAsync(user, currentRoles);
        }

        // Add new role
        var result = await _userManager.AddToRoleAsync(user, request.Role);
        if (!result.Succeeded)
        {
            return BadRequest("Failed to change user role.");
        }

        _logger.LogInformation("Admin {AdminId} changed user {UserId} role to {Role}", adminId, userId, request.Role);

        return Ok();
    }

    [HttpPost("users/{userId:guid}/ban")]
    public async Task<IActionResult> BanUser(Guid userId, [FromBody] BanUserRequest? request)
    {
        var adminId = GetUserId();
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return NotFound("User not found.");
        }

        // Lock the user out indefinitely
        await _userManager.SetLockoutEnabledAsync(user, true);
        await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);

        _logger.LogInformation("Admin {AdminId} banned user {UserId}. Reason: {Reason}",
            adminId, userId, request?.Reason ?? "No reason provided");

        return Ok();
    }

    [HttpPost("users/{userId:guid}/unban")]
    public async Task<IActionResult> UnbanUser(Guid userId)
    {
        var adminId = GetUserId();
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return NotFound("User not found.");
        }

        await _userManager.SetLockoutEndDateAsync(user, null);

        _logger.LogInformation("Admin {AdminId} unbanned user {UserId}", adminId, userId);

        return Ok();
    }

    private Guid GetUserId()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier)
                        ?? throw new UnauthorizedAccessException("User ID not found in claims.");
        return Guid.Parse(userIdStr);
    }
}
