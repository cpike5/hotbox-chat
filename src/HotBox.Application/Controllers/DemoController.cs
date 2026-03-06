using HotBox.Application.Models;
using HotBox.Core.Interfaces;
using HotBox.Core.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace HotBox.Application.Controllers;

[ApiController]
[Route("api/demo")]
public class DemoController : ControllerBase
{
    private readonly IDemoUserService _demoUserService;
    private readonly ITokenService _tokenService;
    private readonly DemoModeOptions _demoOptions;
    private readonly ILogger<DemoController> _logger;

    public DemoController(
        IDemoUserService demoUserService,
        ITokenService tokenService,
        IOptions<DemoModeOptions> demoOptions,
        ILogger<DemoController> logger)
    {
        _demoUserService = demoUserService;
        _tokenService = tokenService;
        _demoOptions = demoOptions.Value;
        _logger = logger;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] DemoRegisterRequest request, CancellationToken ct)
    {
        if (!_demoOptions.Enabled)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        // All rate-limiting and capacity checks are inside CreateDemoUserAsync
        // (protected by a semaphore to prevent TOCTOU races)
        var user = await _demoUserService.CreateDemoUserAsync(
            request.Username, request.DisplayName, ipAddress, ct);

        if (user is null)
        {
            // Determine the specific rejection reason for the client
            if (await _demoUserService.IsIpCoolingDownAsync(ipAddress, ct))
                return StatusCode(429, new { error = "Please wait before creating another demo account." });

            var activeCount = await _demoUserService.GetActiveDemoUserCountAsync(ct);
            if (activeCount >= _demoOptions.MaxConcurrentUsers)
                return Conflict(new { error = "Demo capacity reached. Please try again later." });

            return BadRequest(new { error = "Failed to create demo account." });
        }

        var accessToken = await _tokenService.GenerateAccessTokenAsync(user, ct);

        _logger.LogInformation("Demo user registered: {UserId} ({DisplayName}) from IP {IpAddress}",
            user.Id, request.DisplayName, ipAddress);

        return Ok(new AuthResponse { AccessToken = accessToken });
    }

    [HttpGet("status")]
    [AllowAnonymous]
    public async Task<IActionResult> Status(CancellationToken ct)
    {
        var currentUsers = _demoOptions.Enabled
            ? await _demoUserService.GetActiveDemoUserCountAsync(ct)
            : 0;

        return Ok(new
        {
            enabled = _demoOptions.Enabled,
            currentUsers,
            maxUsers = _demoOptions.MaxConcurrentUsers,
            sessionTimeoutMinutes = _demoOptions.SessionTimeoutMinutes,
        });
    }
}
