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
}
