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
public class AuthController : ControllerBase
{
    private readonly UserManager<AppUser> _userManager;
    private readonly SignInManager<AppUser> _signInManager;
    private readonly ITokenService _tokenService;
    private readonly IInviteService _inviteService;
    private readonly IServerSettingsService _serverSettingsService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        UserManager<AppUser> userManager,
        SignInManager<AppUser> signInManager,
        ITokenService tokenService,
        IInviteService inviteService,
        IServerSettingsService serverSettingsService,
        ILogger<AuthController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _tokenService = tokenService;
        _inviteService = inviteService;
        _serverSettingsService = serverSettingsService;
        _logger = logger;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request)
    {
        var settings = await _serverSettingsService.GetAsync();

        if (settings.RegistrationMode == RegistrationMode.Closed)
        {
            return BadRequest("Registration is currently closed.");
        }

        if (settings.RegistrationMode == RegistrationMode.InviteOnly)
        {
            if (string.IsNullOrWhiteSpace(request.InviteCode))
            {
                return BadRequest("An invite code is required to register.");
            }

            var invite = await _inviteService.ValidateAndConsumeAsync(request.InviteCode);
            if (invite is null)
            {
                return BadRequest("Invalid or expired invite code.");
            }
        }

        var existingUser = await _userManager.FindByEmailAsync(request.Email);
        if (existingUser is not null)
        {
            return Conflict("A user with this email already exists.");
        }

        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = request.Email,
            Email = request.Email,
            DisplayName = request.DisplayName,
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow,
            LastSeenAt = DateTime.UtcNow,
            Status = UserStatus.Offline
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            return BadRequest(errors);
        }

        await _userManager.AddToRoleAsync(user, nameof(UserRole.Member));

        _logger.LogInformation("User {UserId} registered with email {Email}", user.Id, user.Email);

        var accessToken = await _tokenService.GenerateAccessTokenAsync(user);
        var refreshToken = await _tokenService.GenerateRefreshTokenAsync(user.Id);

        SetRefreshTokenCookie(refreshToken.TokenHash, refreshToken.ExpiresAt);

        var roles = await _userManager.GetRolesAsync(user);
        var profile = MapToProfile(user, roles);

        return Ok(new AuthResponse(accessToken, DateTime.UtcNow.AddMinutes(15), profile));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            return Unauthorized("Invalid email or password.");
        }

        if (await _userManager.IsLockedOutAsync(user))
        {
            return Unauthorized("Account is locked. Please try again later.");
        }

        var passwordValid = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!passwordValid)
        {
            await _userManager.AccessFailedAsync(user);
            return Unauthorized("Invalid email or password.");
        }

        await _userManager.ResetAccessFailedCountAsync(user);

        user.LastSeenAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        var accessToken = await _tokenService.GenerateAccessTokenAsync(user);
        var refreshToken = await _tokenService.GenerateRefreshTokenAsync(user.Id);

        SetRefreshTokenCookie(refreshToken.TokenHash, refreshToken.ExpiresAt);

        var roles = await _userManager.GetRolesAsync(user);
        var profile = MapToProfile(user, roles);

        _logger.LogInformation("User {UserId} logged in", user.Id);

        return Ok(new AuthResponse(accessToken, DateTime.UtcNow.AddMinutes(15), profile));
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh()
    {
        var refreshTokenValue = Request.Cookies["refreshToken"];
        if (string.IsNullOrWhiteSpace(refreshTokenValue))
        {
            return Unauthorized("No refresh token provided.");
        }

        var existingToken = await _tokenService.ValidateRefreshTokenAsync(refreshTokenValue);
        if (existingToken is null)
        {
            return Unauthorized("Invalid or expired refresh token.");
        }

        var user = await _userManager.FindByIdAsync(existingToken.UserId.ToString());
        if (user is null)
        {
            return Unauthorized("User not found.");
        }

        var newRefreshToken = await _tokenService.RotateRefreshTokenAsync(existingToken);
        var accessToken = await _tokenService.GenerateAccessTokenAsync(user);

        SetRefreshTokenCookie(newRefreshToken.TokenHash, newRefreshToken.ExpiresAt);

        var roles = await _userManager.GetRolesAsync(user);
        var profile = MapToProfile(user, roles);

        return Ok(new AuthResponse(accessToken, DateTime.UtcNow.AddMinutes(15), profile));
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        var refreshTokenValue = Request.Cookies["refreshToken"];
        if (!string.IsNullOrWhiteSpace(refreshTokenValue))
        {
            await _tokenService.RevokeRefreshTokenAsync(refreshTokenValue);
        }

        Response.Cookies.Delete("refreshToken");

        return Ok();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserProfileResponse>> Me()
    {
        var userId = GetUserId();
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return NotFound();
        }

        var roles = await _userManager.GetRolesAsync(user);
        return Ok(MapToProfile(user, roles));
    }

    [HttpPost("external/{provider}")]
    public IActionResult ExternalLogin(string provider, [FromQuery] string? returnUrl)
    {
        // OAuth external login redirect - placeholder for full OAuth flow
        var redirectUrl = Url.Action(nameof(ExternalLoginCallback), "Auth", new { returnUrl });
        var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
        return Challenge(properties, provider);
    }

    [HttpGet("external/callback")]
    public async Task<IActionResult> ExternalLoginCallback([FromQuery] string? returnUrl)
    {
        var info = await _signInManager.GetExternalLoginInfoAsync();
        if (info is null)
        {
            return BadRequest("External login info not available.");
        }

        var email = info.Principal.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrWhiteSpace(email))
        {
            return BadRequest("Email not provided by external provider.");
        }

        var user = await _userManager.FindByEmailAsync(email);
        if (user is null)
        {
            // Auto-create user from external login
            var displayName = info.Principal.FindFirstValue(ClaimTypes.Name) ?? email.Split('@')[0];
            user = new AppUser
            {
                Id = Guid.NewGuid(),
                UserName = email,
                Email = email,
                DisplayName = displayName,
                EmailConfirmed = true,
                CreatedAt = DateTime.UtcNow,
                LastSeenAt = DateTime.UtcNow,
                Status = UserStatus.Offline
            };

            var createResult = await _userManager.CreateAsync(user);
            if (!createResult.Succeeded)
            {
                return BadRequest("Failed to create user from external login.");
            }

            await _userManager.AddToRoleAsync(user, nameof(UserRole.Member));
            await _userManager.AddLoginAsync(user, info);
        }

        var accessToken = await _tokenService.GenerateAccessTokenAsync(user);
        var refreshToken = await _tokenService.GenerateRefreshTokenAsync(user.Id);
        SetRefreshTokenCookie(refreshToken.TokenHash, refreshToken.ExpiresAt);

        // Redirect back to the app with the token
        var redirect = string.IsNullOrWhiteSpace(returnUrl) ? "/" : returnUrl;
        return Redirect($"{redirect}?token={accessToken}");
    }

    private void SetRefreshTokenCookie(string tokenHash, DateTime expires)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = expires
        };
        Response.Cookies.Append("refreshToken", tokenHash, cookieOptions);
    }

    private Guid GetUserId()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier)
                        ?? throw new UnauthorizedAccessException("User ID not found in claims.");
        return Guid.Parse(userIdStr);
    }

    private static UserProfileResponse MapToProfile(AppUser user, IList<string> roles)
    {
        return new UserProfileResponse(
            user.Id,
            user.DisplayName,
            user.Email!,
            user.AvatarUrl,
            user.Bio,
            user.Pronouns,
            user.CustomStatus,
            user.Status,
            user.IsAgent,
            roles,
            user.CreatedAt,
            user.LastSeenAt);
    }
}
