using System.Security.Claims;
using HotBox.Application.Models;
using HotBox.Core.Entities;
using HotBox.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotBox.Application.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly UserManager<AppUser> _userManager;
    private readonly IPresenceService _presenceService;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        UserManager<AppUser> userManager,
        IPresenceService presenceService,
        ILogger<UsersController> logger)
    {
        _userManager = userManager;
        _presenceService = presenceService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UserProfileResponse>>> GetAll()
    {
        var users = await _userManager.Users.ToListAsync();
        var result = new List<UserProfileResponse>();

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            var status = _presenceService.GetStatus(user.Id);
            result.Add(new UserProfileResponse(
                user.Id,
                user.DisplayName,
                user.Email!,
                user.AvatarUrl,
                user.Bio,
                user.Pronouns,
                user.CustomStatus,
                status,
                user.IsAgent,
                roles,
                user.CreatedAt,
                user.LastSeenAt));
        }

        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<UserProfileResponse>> GetById(Guid id)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return NotFound();
        }

        var roles = await _userManager.GetRolesAsync(user);
        var status = _presenceService.GetStatus(user.Id);

        return Ok(new UserProfileResponse(
            user.Id,
            user.DisplayName,
            user.Email!,
            user.AvatarUrl,
            user.Bio,
            user.Pronouns,
            user.CustomStatus,
            status,
            user.IsAgent,
            roles,
            user.CreatedAt,
            user.LastSeenAt));
    }

    [HttpPut("me")]
    public async Task<ActionResult<UserProfileResponse>> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        var userId = GetUserId();
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return NotFound();
        }

        if (request.DisplayName is not null)
            user.DisplayName = request.DisplayName;
        if (request.AvatarUrl is not null)
            user.AvatarUrl = request.AvatarUrl;
        if (request.Bio is not null)
            user.Bio = request.Bio;
        if (request.Pronouns is not null)
            user.Pronouns = request.Pronouns;
        if (request.CustomStatus is not null)
            user.CustomStatus = request.CustomStatus;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            return BadRequest("Failed to update profile.");
        }

        _logger.LogInformation("User {UserId} updated profile", userId);

        var roles = await _userManager.GetRolesAsync(user);
        var status = _presenceService.GetStatus(user.Id);

        return Ok(new UserProfileResponse(
            user.Id,
            user.DisplayName,
            user.Email!,
            user.AvatarUrl,
            user.Bio,
            user.Pronouns,
            user.CustomStatus,
            status,
            user.IsAgent,
            roles,
            user.CreatedAt,
            user.LastSeenAt));
    }

    [HttpGet("online")]
    public ActionResult<IReadOnlyList<OnlineUserInfo>> GetOnlineUsers()
    {
        var onlineUsers = _presenceService.GetAllOnlineUsers()
            .Select(u => new OnlineUserInfo(u.UserId, u.DisplayName, u.Status, u.IsAgent))
            .ToList();
        return Ok(onlineUsers);
    }

    private Guid GetUserId()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier)
                        ?? throw new UnauthorizedAccessException("User ID not found in claims.");
        return Guid.Parse(userIdStr);
    }
}
