using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using HotBox.Application.Models;
using HotBox.Core.Entities;
using HotBox.Core.Interfaces;
using HotBox.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotBox.Application.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly IServerSettingsService _serverSettingsService;
    private readonly IInviteService _inviteService;
    private readonly IChannelRepository _channelRepository;
    private readonly UserManager<AppUser> _userManager;
    private readonly HotBoxDbContext _dbContext;
    private readonly ILogger<AdminController> _logger;

    private static readonly string[] ValidRoles = ["Admin", "Moderator", "Member"];

    public AdminController(
        IServerSettingsService serverSettingsService,
        IInviteService inviteService,
        IChannelRepository channelRepository,
        UserManager<AppUser> userManager,
        HotBoxDbContext dbContext,
        ILogger<AdminController> logger)
    {
        _serverSettingsService = serverSettingsService;
        _inviteService = inviteService;
        _channelRepository = channelRepository;
        _userManager = userManager;
        _dbContext = dbContext;
        _logger = logger;
    }

    // --- Server Settings ---

    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings(CancellationToken ct)
    {
        var settings = await _serverSettingsService.GetAsync(ct);

        return Ok(new AdminSettingsResponse
        {
            ServerName = settings.ServerName,
            RegistrationMode = settings.RegistrationMode,
        });
    }

    [HttpPut("settings")]
    public async Task<IActionResult> UpdateSettings([FromBody] UpdateSettingsRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized(new { error = "Unable to determine user identity." });
        }

        try
        {
            var settings = await _serverSettingsService.UpdateAsync(
                request.ServerName, request.RegistrationMode, ct);

            _logger.LogInformation(
                "Server settings updated by admin {UserId}: ServerName={ServerName}, RegistrationMode={RegistrationMode}",
                userId, request.ServerName, request.RegistrationMode);

            return Ok(new AdminSettingsResponse
            {
                ServerName = settings.ServerName,
                RegistrationMode = settings.RegistrationMode,
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // --- User Management ---

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers(CancellationToken ct)
    {
        // Note: GetRolesAsync is called per user (N+1), but this project targets
        // ~100 users max so the overhead is negligible. A bulk join query via
        // DbContext would be needed if the user base grew significantly.
        var users = await _userManager.Users
            .OrderBy(u => u.DisplayName)
            .ToListAsync(ct);

        var response = new List<AdminUserResponse>(users.Count);
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            response.Add(new AdminUserResponse
            {
                Id = user.Id,
                DisplayName = user.DisplayName,
                Email = user.Email ?? string.Empty,
                Role = roles.FirstOrDefault() ?? "Member",
                CreatedAtUtc = user.CreatedAtUtc,
                LastSeenUtc = user.LastSeenUtc,
                IsAgent = user.IsAgent,
            });
        }

        return Ok(response);
    }

    [HttpPost("users")]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
    {
        var adminUserId = GetUserId();
        if (adminUserId is null)
        {
            return Unauthorized(new { error = "Unable to determine user identity." });
        }

        if (!ValidRoles.Contains(request.Role))
        {
            return BadRequest(new { error = $"Invalid role '{request.Role}'. Valid roles are: {string.Join(", ", ValidRoles)}." });
        }

        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = request.Email,
            Email = request.Email,
            DisplayName = request.DisplayName,
            CreatedAtUtc = DateTime.UtcNow,
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            _logger.LogWarning(
                "Admin user creation failed for email {Email}: {Errors}",
                request.Email, errors);
            return BadRequest(new { error = errors });
        }

        await _userManager.AddToRoleAsync(user, request.Role);

        _logger.LogInformation(
            "Admin {AdminUserId} created user {UserId} ({Email}) with role {Role}",
            adminUserId, user.Id, request.Email, request.Role);

        return CreatedAtAction(nameof(GetUsers), null, new AdminUserResponse
        {
            Id = user.Id,
            DisplayName = user.DisplayName,
            Email = user.Email ?? string.Empty,
            Role = request.Role,
            CreatedAtUtc = user.CreatedAtUtc,
            LastSeenUtc = user.LastSeenUtc,
            IsAgent = user.IsAgent,
        });
    }

    [HttpPut("users/{id:guid}/role")]
    public async Task<IActionResult> ChangeRole(Guid id, [FromBody] ChangeRoleRequest request)
    {
        var adminUserId = GetUserId();
        if (adminUserId is null)
        {
            return Unauthorized(new { error = "Unable to determine user identity." });
        }

        if (!ValidRoles.Contains(request.Role))
        {
            return BadRequest(new { error = $"Invalid role '{request.Role}'. Valid roles are: {string.Join(", ", ValidRoles)}." });
        }

        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return NotFound(new { error = $"User {id} not found." });
        }

        var currentRoles = await _userManager.GetRolesAsync(user);
        if (currentRoles.Count > 0)
        {
            await _userManager.RemoveFromRolesAsync(user, currentRoles);
        }

        await _userManager.AddToRoleAsync(user, request.Role);

        _logger.LogInformation(
            "Admin {AdminUserId} changed role for user {UserId} from [{OldRoles}] to {NewRole}",
            adminUserId, id, string.Join(", ", currentRoles), request.Role);

        return Ok(new { message = $"User role updated to {request.Role}." });
    }

    [HttpGet("users/{id:guid}")]
    public async Task<IActionResult> GetUser(Guid id)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return NotFound(new { error = $"User {id} not found." });
        }

        var roles = await _userManager.GetRolesAsync(user);

        return Ok(new AdminUserDetailResponse
        {
            Id = user.Id,
            DisplayName = user.DisplayName,
            Email = user.Email ?? string.Empty,
            Role = roles.FirstOrDefault() ?? "Member",
            CreatedAtUtc = user.CreatedAtUtc,
            LastSeenUtc = user.LastSeenUtc,
            IsAgent = user.IsAgent,
            AvatarUrl = user.AvatarUrl,
            Bio = user.Bio,
            Pronouns = user.Pronouns,
            CustomStatus = user.CustomStatus,
            EmailConfirmed = user.EmailConfirmed,
            LockoutEnd = user.LockoutEnd,
            LockoutEnabled = user.LockoutEnabled,
        });
    }

    [HttpPut("users/{id:guid}/lockout")]
    public async Task<IActionResult> ToggleLockout(Guid id, [FromBody] ToggleLockoutRequest request)
    {
        var adminUserId = GetUserId();
        if (adminUserId is null)
        {
            return Unauthorized(new { error = "Unable to determine user identity." });
        }

        if (adminUserId == id)
        {
            return BadRequest(new { error = "Cannot lock out your own account." });
        }

        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return NotFound(new { error = $"User {id} not found." });
        }

        if (request.Disabled)
        {
            await _userManager.SetLockoutEnabledAsync(user, true);
            await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
            _logger.LogInformation("Admin {AdminUserId} disabled user {UserId}", adminUserId, id);
        }
        else
        {
            await _userManager.SetLockoutEndDateAsync(user, null);
            _logger.LogInformation("Admin {AdminUserId} enabled user {UserId}", adminUserId, id);
        }

        return Ok(new { message = request.Disabled ? "User disabled." : "User enabled." });
    }

    [HttpDelete("users/{id:guid}")]
    public async Task<IActionResult> DeleteUser(Guid id)
    {
        var adminUserId = GetUserId();
        if (adminUserId is null)
        {
            return Unauthorized(new { error = "Unable to determine user identity." });
        }

        if (adminUserId == id)
        {
            return BadRequest(new { error = "Cannot delete your own account." });
        }

        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return NotFound(new { error = $"User {id} not found." });
        }

        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            _logger.LogError(
                "Failed to delete user {UserId}: {Errors}",
                id, errors);
            return BadRequest(new { error = errors });
        }

        _logger.LogInformation(
            "Admin {AdminUserId} deleted user {UserId} ({Email})",
            adminUserId, id, user.Email);

        return Ok(new { message = "User deleted." });
    }

    // --- Invite Management ---

    [HttpGet("invites")]
    public async Task<IActionResult> GetInvites(CancellationToken ct)
    {
        var invites = await _inviteService.GetAllAsync(ct);

        var response = invites.Select(i => new AdminInviteResponse
        {
            Id = i.Id,
            Code = i.Code,
            CreatedByUserId = i.CreatedByUserId,
            CreatedByDisplayName = i.CreatedBy?.DisplayName,
            CreatedAtUtc = i.CreatedAtUtc,
            ExpiresAtUtc = i.ExpiresAtUtc,
            MaxUses = i.MaxUses,
            UseCount = i.UseCount,
            IsRevoked = i.IsRevoked,
        }).ToList();

        return Ok(response);
    }

    [HttpPost("invites")]
    public async Task<IActionResult> GenerateInvite([FromBody] GenerateInviteRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized(new { error = "Unable to determine user identity." });
        }

        try
        {
            var invite = await _inviteService.GenerateAsync(
                userId.Value, request.ExpiresAtUtc, request.MaxUses, ct);

            _logger.LogInformation(
                "Admin {UserId} generated invite {InviteCode}",
                userId, invite.Code);

            return CreatedAtAction(nameof(GetInvites), null, new AdminInviteResponse
            {
                Id = invite.Id,
                Code = invite.Code,
                CreatedByUserId = invite.CreatedByUserId,
                CreatedByDisplayName = invite.CreatedBy?.DisplayName,
                CreatedAtUtc = invite.CreatedAtUtc,
                ExpiresAtUtc = invite.ExpiresAtUtc,
                MaxUses = invite.MaxUses,
                UseCount = invite.UseCount,
                IsRevoked = invite.IsRevoked,
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("invites/{code}")]
    public async Task<IActionResult> RevokeInvite(string code, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized(new { error = "Unable to determine user identity." });
        }

        var revoked = await _inviteService.RevokeAsync(code, ct);
        if (!revoked)
        {
            return NotFound(new { error = $"Invite with code '{code}' not found." });
        }

        _logger.LogInformation(
            "Admin {UserId} revoked invite {InviteCode}",
            userId, code);

        return Ok(new { message = "Invite revoked." });
    }

    // --- Channel Management ---

    [HttpPut("channels/reorder")]
    public async Task<IActionResult> ReorderChannels([FromBody] ReorderChannelsRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized(new { error = "Unable to determine user identity." });
        }

        if (request.ChannelIds.Count == 0)
        {
            return BadRequest(new { error = "Channel ID list cannot be empty." });
        }

        try
        {
            await _channelRepository.ReorderAsync(request.ChannelIds, ct);

            _logger.LogInformation(
                "Admin {UserId} reordered {ChannelCount} channels",
                userId, request.ChannelIds.Count);

            return Ok(new { message = "Channels reordered." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // --- API Key Management ---

    [HttpPost("apikeys")]
    public async Task<IActionResult> CreateApiKey([FromBody] CreateApiKeyRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized(new { error = "Unable to determine user identity." });
        }

        var randomBytes = RandomNumberGenerator.GetBytes(32);
        var plaintextKey = Convert.ToBase64String(randomBytes);

        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(plaintextKey));
        var keyHash = Convert.ToBase64String(hashBytes);

        var keyPrefix = plaintextKey[^8..];

        var apiKey = new ApiKey
        {
            Id = Guid.NewGuid(),
            KeyValue = keyHash,
            KeyPrefix = keyPrefix,
            Name = request.Name,
            CreatedAtUtc = DateTime.UtcNow,
        };

        _dbContext.ApiKeys.Add(apiKey);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Admin {UserId} created API key {ApiKeyId} ({ApiKeyName})",
            userId, apiKey.Id, apiKey.Name);

        return CreatedAtAction(nameof(GetApiKeys), null, new CreateApiKeyResponse
        {
            Id = apiKey.Id,
            Name = apiKey.Name,
            Key = plaintextKey,
            CreatedAtUtc = apiKey.CreatedAtUtc,
        });
    }

    [HttpGet("apikeys")]
    public async Task<IActionResult> GetApiKeys(CancellationToken ct)
    {
        var apiKeys = await _dbContext.ApiKeys
            .AsNoTracking()
            .OrderByDescending(ak => ak.CreatedAtUtc)
            .ToListAsync(ct);

        var response = apiKeys.Select(ak => new AdminApiKeyResponse
        {
            Id = ak.Id,
            Name = ak.Name,
            MaskedKey = $"\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022{ak.KeyPrefix}",
            CreatedAtUtc = ak.CreatedAtUtc,
            RevokedAtUtc = ak.RevokedAtUtc,
            RevokedReason = ak.RevokedReason,
            IsActive = ak.IsActive,
        }).ToList();

        return Ok(response);
    }

    [HttpPut("apikeys/{id:guid}/revoke")]
    public async Task<IActionResult> RevokeApiKey(Guid id, [FromBody] RevokeApiKeyRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized(new { error = "Unable to determine user identity." });
        }

        var apiKey = await _dbContext.ApiKeys.FindAsync([id], ct);
        if (apiKey is null)
        {
            return NotFound(new { error = $"API key {id} not found." });
        }

        if (apiKey.RevokedAtUtc.HasValue)
        {
            return BadRequest(new { error = "API key is already revoked." });
        }

        apiKey.RevokedAtUtc = DateTime.UtcNow;
        apiKey.RevokedReason = request.Reason;
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Admin {UserId} revoked API key {ApiKeyId} ({ApiKeyName}). Reason: {RevokeReason}",
            userId, apiKey.Id, apiKey.Name, request.Reason ?? "none");

        return Ok(new { message = "API key revoked." });
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
