using System.Security.Claims;
using HotBox.Application.Models;
using HotBox.Core.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotBox.Application.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly UserManager<AppUser> _userManager;

    public UsersController(UserManager<AppUser> userManager)
    {
        _userManager = userManager;
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMyProfile()
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        var user = await _userManager.FindByIdAsync(userId.Value.ToString());
        if (user is null)
            return NotFound();

        var roles = await _userManager.GetRolesAsync(user);
        return Ok(MapToResponse(user, roles.FirstOrDefault() ?? "Member"));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetUserProfile(Guid id)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null)
            return NotFound();

        var roles = await _userManager.GetRolesAsync(user);
        return Ok(MapToResponse(user, roles.FirstOrDefault() ?? "Member"));
    }

    [HttpPut("me")]
    public async Task<IActionResult> UpdateMyProfile([FromBody] UpdateProfileRequest request)
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        var user = await _userManager.FindByIdAsync(userId.Value.ToString());
        if (user is null)
            return NotFound();

        user.DisplayName = request.DisplayName;
        user.AvatarUrl = request.AvatarUrl;
        user.Bio = request.Bio;
        user.Pronouns = request.Pronouns;
        user.CustomStatus = request.CustomStatus;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            return BadRequest(new { error = errors });
        }

        var roles = await _userManager.GetRolesAsync(user);
        return Ok(MapToResponse(user, roles.FirstOrDefault() ?? "Member"));
    }

    [HttpGet]
    public async Task<IActionResult> GetAllUsers(CancellationToken ct = default)
    {
        var users = await _userManager.Users.ToListAsync(ct);
        var result = new List<UserProfileResponse>();
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            result.Add(MapToResponse(user, roles.FirstOrDefault() ?? "Member"));
        }
        return Ok(result);
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search(
        [FromQuery] string? q = null,
        CancellationToken ct = default)
    {
        var currentUserId = GetUserId();
        if (currentUserId is null)
            return Unauthorized();

        var query = _userManager.Users
            .Where(u => u.Id != currentUserId.Value);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(u => u.DisplayName.Contains(term));
        }

        var users = await query
            .OrderBy(u => u.DisplayName)
            .Take(20)
            .Select(u => new UserSearchResult
            {
                Id = u.Id,
                DisplayName = u.DisplayName,
            })
            .ToListAsync(ct);

        return Ok(users);
    }

    private Guid? GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            return null;

        return userId;
    }

    private static UserProfileResponse MapToResponse(AppUser user, string role) => new()
    {
        Id = user.Id,
        DisplayName = user.DisplayName,
        AvatarUrl = user.AvatarUrl,
        Bio = user.Bio,
        Pronouns = user.Pronouns,
        CustomStatus = user.CustomStatus,
        Status = user.Status.ToString(),
        Role = role,
        CreatedAtUtc = user.CreatedAtUtc,
        LastSeenUtc = user.LastSeenUtc,
        IsAgent = user.IsAgent,
    };
}
