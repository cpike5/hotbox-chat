using System.Security.Claims;
using HotBox.Application.Models;
using HotBox.Core.Entities;
using HotBox.Core.Enums;
using HotBox.Core.Interfaces;
using HotBox.Core.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace HotBox.Application.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly ITokenService _tokenService;
    private readonly JwtOptions _jwtOptions;
    private readonly OAuthOptions _oauthOptions;
    private readonly UserManager<AppUser> _userManager;
    private readonly SignInManager<AppUser> _signInManager;
    private readonly IInviteService _inviteService;
    private readonly IServerSettingsService _serverSettingsService;
    private readonly ILogger<AuthController> _logger;
    private readonly IWebHostEnvironment _environment;

    private const string RefreshTokenCookieName = "refreshToken";
    private const string DefaultRole = "Member";

    private static readonly HashSet<string> SupportedProviders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Google",
        "Microsoft",
    };

    public AuthController(
        ITokenService tokenService,
        IOptions<JwtOptions> jwtOptions,
        IOptions<OAuthOptions> oauthOptions,
        UserManager<AppUser> userManager,
        SignInManager<AppUser> signInManager,
        IInviteService inviteService,
        IServerSettingsService serverSettingsService,
        ILogger<AuthController> logger,
        IWebHostEnvironment environment)
    {
        _tokenService = tokenService;
        _jwtOptions = jwtOptions.Value;
        _oauthOptions = oauthOptions.Value;
        _userManager = userManager;
        _signInManager = signInManager;
        _inviteService = inviteService;
        _serverSettingsService = serverSettingsService;
        _logger = logger;
        _environment = environment;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        // Enforce registration mode (read from DB, falls back to appsettings)
        var serverSettings = await _serverSettingsService.GetAsync(ct);
        switch (serverSettings.RegistrationMode)
        {
            case RegistrationMode.Closed:
                _logger.LogWarning("Registration attempt rejected: registration is closed");
                return StatusCode(403, new { error = "Registration is currently closed." });

            case RegistrationMode.InviteOnly:
                if (string.IsNullOrWhiteSpace(request.InviteCode))
                {
                    _logger.LogWarning("Registration attempt rejected: invite code required but not provided");
                    return BadRequest(new { error = "An invite code is required to register." });
                }

                var invite = await _inviteService.ValidateAndConsumeAsync(request.InviteCode, ct);
                if (invite is null)
                {
                    _logger.LogWarning("Registration attempt rejected: invalid or expired invite code");
                    return BadRequest(new { error = "The invite code is invalid or has expired." });
                }
                break;

            case RegistrationMode.Open:
                break;
        }

        // Check if a user with this email already exists
        var existingUser = await _userManager.FindByEmailAsync(request.Email);
        if (existingUser is not null)
        {
            _logger.LogWarning("Registration attempt for existing email {Email}", request.Email);
            return BadRequest(new { error = "An account with this email already exists." });
        }

        var user = new AppUser
        {
            UserName = request.Email,
            Email = request.Email,
            DisplayName = request.DisplayName,
            Status = UserStatus.Offline,
            CreatedAtUtc = DateTime.UtcNow,
            LastSeenUtc = DateTime.UtcNow,
        };

        var createResult = await _userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            var errors = string.Join("; ", createResult.Errors.Select(e => e.Description));
            _logger.LogWarning("User creation failed for {Email}: {Errors}", request.Email, errors);
            return BadRequest(new { error = errors });
        }

        var roleResult = await _userManager.AddToRoleAsync(user, DefaultRole);
        if (!roleResult.Succeeded)
        {
            _logger.LogError("Failed to assign {Role} role to user {UserId}: {Errors}",
                DefaultRole, user.Id, string.Join("; ", roleResult.Errors.Select(e => e.Description)));
        }

        // Generate tokens
        var accessToken = await _tokenService.GenerateAccessTokenAsync(user, ct);
        var refreshToken = await _tokenService.GenerateRefreshTokenAsync(user.Id, ct);

        SetRefreshTokenCookie(refreshToken.Token, refreshToken.ExpiresAtUtc);

        _logger.LogInformation("User registered successfully: {UserId} ({Email})", user.Id, request.Email);

        return Ok(new AuthResponse { AccessToken = accessToken });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            _logger.LogWarning("Login attempt for non-existent email {Email}", request.Email);
            return Unauthorized(new { error = "Invalid email or password." });
        }

        var signInResult = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!signInResult.Succeeded)
        {
            _logger.LogWarning("Failed login attempt for user {UserId} ({Email})", user.Id, request.Email);
            return Unauthorized(new { error = "Invalid email or password." });
        }

        var accessToken = await _tokenService.GenerateAccessTokenAsync(user, ct);
        var refreshToken = await _tokenService.GenerateRefreshTokenAsync(user.Id, ct);

        SetRefreshTokenCookie(refreshToken.Token, refreshToken.ExpiresAtUtc);

        _logger.LogInformation("User logged in: {UserId} ({Email})", user.Id, request.Email);

        return Ok(new AuthResponse { AccessToken = accessToken });
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(CancellationToken ct)
    {
        var refreshTokenValue = Request.Cookies[RefreshTokenCookieName];

        if (string.IsNullOrWhiteSpace(refreshTokenValue))
        {
            _logger.LogWarning("Refresh attempt with missing refresh token cookie");
            return Unauthorized(new { error = "Refresh token is required." });
        }

        var existingToken = await _tokenService.ValidateRefreshTokenAsync(refreshTokenValue, ct);

        if (existingToken is null)
        {
            _logger.LogWarning("Refresh attempt with invalid or expired refresh token");
            ClearRefreshTokenCookie();
            return Unauthorized(new { error = "Invalid or expired refresh token." });
        }

        // Rotate the refresh token (revokes old, creates new)
        var newRefreshToken = await _tokenService.RotateRefreshTokenAsync(existingToken, ct);

        // Generate a new access token
        var accessToken = await _tokenService.GenerateAccessTokenAsync(existingToken.User, ct);

        // Set the new refresh token as an HttpOnly cookie
        SetRefreshTokenCookie(newRefreshToken.Token, newRefreshToken.ExpiresAtUtc);

        _logger.LogInformation("Token refreshed for user {UserId}", existingToken.UserId);

        return Ok(new { accessToken });
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var refreshTokenValue = Request.Cookies[RefreshTokenCookieName];

        if (string.IsNullOrWhiteSpace(refreshTokenValue))
        {
            _logger.LogDebug("Logout called with no refresh token cookie present");
            return NoContent();
        }

        await _tokenService.RevokeRefreshTokenAsync(refreshTokenValue, ct);

        ClearRefreshTokenCookie();

        _logger.LogInformation("Refresh token revoked via logout");

        return NoContent();
    }

    [HttpGet("providers")]
    public IActionResult GetProviders()
    {
        var providers = new List<ProviderInfo>();

        if (_oauthOptions.Google.Enabled
            && !string.IsNullOrWhiteSpace(_oauthOptions.Google.ClientId)
            && !string.IsNullOrWhiteSpace(_oauthOptions.Google.ClientSecret))
        {
            providers.Add(new ProviderInfo { Name = "Google" });
        }

        if (_oauthOptions.Microsoft.Enabled
            && !string.IsNullOrWhiteSpace(_oauthOptions.Microsoft.ClientId)
            && !string.IsNullOrWhiteSpace(_oauthOptions.Microsoft.ClientSecret))
        {
            providers.Add(new ProviderInfo { Name = "Microsoft" });
        }

        return Ok(providers);
    }

    [HttpGet("registration-mode")]
    public async Task<IActionResult> GetRegistrationMode(CancellationToken ct)
    {
        var serverSettings = await _serverSettingsService.GetAsync(ct);
        return Ok(new { mode = serverSettings.RegistrationMode.ToString() });
    }

    [HttpGet("external/{provider}")]
    public IActionResult ExternalLogin(string provider)
    {
        if (!SupportedProviders.Contains(provider))
        {
            _logger.LogWarning("External login attempt with unsupported provider {Provider}", provider);
            return BadRequest(new { error = $"Provider '{provider}' is not supported." });
        }

        var redirectUrl = Url.Action(nameof(ExternalCallback), "Auth");
        var properties = new AuthenticationProperties { RedirectUri = redirectUrl };

        return Challenge(properties, provider);
    }

    [HttpGet("external/callback")]
    public async Task<IActionResult> ExternalCallback(CancellationToken ct)
    {
        var authenticateResult = await HttpContext.AuthenticateAsync(IdentityConstants.ExternalScheme);

        if (!authenticateResult.Succeeded || authenticateResult.Principal is null)
        {
            _logger.LogWarning("External authentication callback failed");
            return Unauthorized(new { error = "External authentication failed." });
        }

        var email = authenticateResult.Principal.FindFirstValue(ClaimTypes.Email);
        var name = authenticateResult.Principal.FindFirstValue(ClaimTypes.Name);

        if (string.IsNullOrWhiteSpace(email))
        {
            _logger.LogWarning("External authentication returned no email claim");
            return BadRequest(new { error = "Email claim not provided by the external provider." });
        }

        var user = await _userManager.FindByEmailAsync(email);

        if (user is null)
        {
            // Check registration mode for new OAuth users
            var oauthSettings = await _serverSettingsService.GetAsync(ct);
            if (oauthSettings.RegistrationMode == RegistrationMode.Closed)
            {
                _logger.LogWarning("OAuth registration rejected: registration is closed for {Email}", email);
                return StatusCode(403, new { error = "Registration is currently closed." });
            }

            user = new AppUser
            {
                UserName = email,
                Email = email,
                DisplayName = name ?? email,
                EmailConfirmed = true,
                Status = UserStatus.Offline,
                CreatedAtUtc = DateTime.UtcNow,
                LastSeenUtc = DateTime.UtcNow,
            };

            var createResult = await _userManager.CreateAsync(user);
            if (!createResult.Succeeded)
            {
                var errors = string.Join("; ", createResult.Errors.Select(e => e.Description));
                _logger.LogError("Failed to create user via OAuth for {Email}: {Errors}", email, errors);
                return BadRequest(new { error = errors });
            }

            var roleResult = await _userManager.AddToRoleAsync(user, DefaultRole);
            if (!roleResult.Succeeded)
            {
                _logger.LogError("Failed to assign {Role} role to OAuth user {UserId}: {Errors}",
                    DefaultRole, user.Id, string.Join("; ", roleResult.Errors.Select(e => e.Description)));
            }

            _logger.LogInformation("New user created via OAuth: {UserId} ({Email})", user.Id, email);
        }

        // Link external login if not already linked
        var loginInfo = await _signInManager.GetExternalLoginInfoAsync();
        if (loginInfo is not null)
        {
            var existingLogins = await _userManager.GetLoginsAsync(user);
            var alreadyLinked = existingLogins.Any(l =>
                l.LoginProvider == loginInfo.LoginProvider && l.ProviderKey == loginInfo.ProviderKey);

            if (!alreadyLinked)
            {
                var addLoginResult = await _userManager.AddLoginAsync(user, loginInfo);
                if (!addLoginResult.Succeeded)
                {
                    _logger.LogWarning("Failed to link external login for user {UserId}: {Errors}",
                        user.Id, string.Join("; ", addLoginResult.Errors.Select(e => e.Description)));
                }
            }
        }

        // Sign out of the external cookie scheme
        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

        // Generate tokens
        var accessToken = await _tokenService.GenerateAccessTokenAsync(user, ct);
        var refreshToken = await _tokenService.GenerateRefreshTokenAsync(user.Id, ct);

        SetRefreshTokenCookie(refreshToken.Token, refreshToken.ExpiresAtUtc);

        _logger.LogInformation("User authenticated via OAuth: {UserId} ({Email})", user.Id, email);

        return Ok(new AuthResponse { AccessToken = accessToken });
    }

    private void SetRefreshTokenCookie(string token, DateTime expiresAtUtc)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = !_environment.IsDevelopment(),
            SameSite = SameSiteMode.Strict,
            Expires = expiresAtUtc,
        };

        Response.Cookies.Append(RefreshTokenCookieName, token, cookieOptions);
    }

    private void ClearRefreshTokenCookie()
    {
        Response.Cookies.Delete(RefreshTokenCookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = !_environment.IsDevelopment(),
            SameSite = SameSiteMode.Strict,
        });
    }
}
