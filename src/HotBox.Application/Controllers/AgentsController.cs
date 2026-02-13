using System.ComponentModel.DataAnnotations;
using HotBox.Core.Entities;
using HotBox.Core.Enums;
using HotBox.Core.Interfaces;
using HotBox.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotBox.Application.Controllers;

[ApiController]
[Route("api/admin/agents")]
[Authorize(AuthenticationSchemes = "ApiKey")]
public class AgentsController : ControllerBase
{
    private readonly UserManager<AppUser> _userManager;
    private readonly ITokenService _tokenService;
    private readonly HotBoxDbContext _dbContext;
    private readonly ILogger<AgentsController> _logger;

    public AgentsController(
        UserManager<AppUser> userManager,
        ITokenService tokenService,
        HotBoxDbContext dbContext,
        ILogger<AgentsController> logger)
    {
        _userManager = userManager;
        _tokenService = tokenService;
        _dbContext = dbContext;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> CreateAgent(
        [FromBody] CreateAgentRequest request,
        CancellationToken ct)
    {
        var apiKeyIdClaim = User.FindFirst("api_key_id")?.Value;
        if (string.IsNullOrWhiteSpace(apiKeyIdClaim) || !Guid.TryParse(apiKeyIdClaim, out var apiKeyId))
        {
            return Unauthorized(new { error = "Unable to determine API key identity." });
        }

        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = request.Email,
            Email = request.Email,
            DisplayName = request.DisplayName,
            IsAgent = true,
            CreatedByApiKeyId = apiKeyId,
            CreatedAtUtc = DateTime.UtcNow,
            Status = UserStatus.Offline,
        };

        var result = await _userManager.CreateAsync(user);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            _logger.LogWarning(
                "Agent account creation failed for email {Email}: {Errors}",
                request.Email, errors);
            return BadRequest(new { error = errors });
        }

        await _userManager.AddToRoleAsync(user, "Member");

        var accessToken = await _tokenService.GenerateAccessTokenAsync(user, ct);

        _logger.LogInformation(
            "Agent account {UserId} ({Email}) created by API key {ApiKeyId}",
            user.Id, request.Email, apiKeyId);

        return CreatedAtAction(nameof(ListAgents), null, new CreateAgentResponse
        {
            UserId = user.Id,
            DisplayName = user.DisplayName,
            Email = user.Email ?? string.Empty,
            AccessToken = accessToken,
        });
    }

    [HttpGet]
    public async Task<IActionResult> ListAgents(CancellationToken ct)
    {
        var apiKeyIdClaim = User.FindFirst("api_key_id")?.Value;
        if (string.IsNullOrWhiteSpace(apiKeyIdClaim) || !Guid.TryParse(apiKeyIdClaim, out var apiKeyId))
        {
            return Unauthorized(new { error = "Unable to determine API key identity." });
        }

        var agents = await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.IsAgent && u.CreatedByApiKeyId == apiKeyId)
            .OrderBy(u => u.DisplayName)
            .Select(u => new AgentResponse
            {
                Id = u.Id,
                DisplayName = u.DisplayName,
                Email = u.Email ?? string.Empty,
                CreatedAtUtc = u.CreatedAtUtc,
            })
            .ToListAsync(ct);

        return Ok(agents);
    }
}

// --- Agent Models ---

public class CreateAgentRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string DisplayName { get; set; } = string.Empty;
}

public class CreateAgentResponse
{
    public Guid UserId { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string AccessToken { get; set; } = string.Empty;
}

public class AgentResponse
{
    public Guid Id { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }
}
