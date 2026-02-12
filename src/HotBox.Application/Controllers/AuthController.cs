using HotBox.Core.Interfaces;
using HotBox.Core.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace HotBox.Application.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly ITokenService _tokenService;
    private readonly JwtOptions _jwtOptions;
    private readonly ILogger<AuthController> _logger;
    private readonly IWebHostEnvironment _environment;

    private const string RefreshTokenCookieName = "refreshToken";

    public AuthController(
        ITokenService tokenService,
        IOptions<JwtOptions> jwtOptions,
        ILogger<AuthController> logger,
        IWebHostEnvironment environment)
    {
        _tokenService = tokenService;
        _jwtOptions = jwtOptions.Value;
        _logger = logger;
        _environment = environment;
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
    [HttpPost("revoke")]
    public async Task<IActionResult> Revoke(CancellationToken ct)
    {
        var refreshTokenValue = Request.Cookies[RefreshTokenCookieName];

        if (string.IsNullOrWhiteSpace(refreshTokenValue))
        {
            _logger.LogDebug("Revoke called with no refresh token cookie present");
            return NoContent();
        }

        await _tokenService.RevokeRefreshTokenAsync(refreshTokenValue, ct);

        ClearRefreshTokenCookie();

        _logger.LogInformation("Refresh token revoked via logout");

        return NoContent();
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
