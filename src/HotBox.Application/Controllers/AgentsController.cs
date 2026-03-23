using System.Security.Claims;
using System.Security.Cryptography;
using HotBox.Application.Models;
using HotBox.Core.Entities;
using HotBox.Core.Enums;
using HotBox.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HotBox.Application.Authentication;

namespace HotBox.Application.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(AuthenticationSchemes = ApiKeyAuthenticationOptions.SchemeName)]
public class AgentsController : ControllerBase
{
    private readonly UserManager<AppUser> _userManager;
    private readonly HotBoxDbContext _dbContext;
    private readonly ILogger<AgentsController> _logger;

    public AgentsController(
        UserManager<AppUser> userManager,
        HotBoxDbContext dbContext,
        ILogger<AgentsController> logger)
    {
        _userManager = userManager;
        _dbContext = dbContext;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<AgentResponse>> CreateAgent([FromBody] CreateAgentRequest request)
    {
        var apiKeyIdStr = User.FindFirstValue("api_key_id");
        if (!Guid.TryParse(apiKeyIdStr, out var apiKeyId))
        {
            return Unauthorized("Invalid API key.");
        }

        var agent = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = $"agent-{Guid.NewGuid():N}@hotbox.local",
            Email = $"agent-{Guid.NewGuid():N}@hotbox.local",
            DisplayName = request.DisplayName,
            Bio = request.Bio,
            IsAgent = true,
            CreatedByApiKeyId = apiKeyId,
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow,
            LastSeenAt = DateTime.UtcNow,
            Status = UserStatus.Offline
        };

        // Generate a random password for the agent (they'll authenticate via API key)
        var randomPassword = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) + "Aa1!";
        var result = await _userManager.CreateAsync(agent, randomPassword);

        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            _logger.LogError("Failed to create agent: {Errors}", errors);
            return BadRequest(errors);
        }

        await _userManager.AddToRoleAsync(agent, nameof(UserRole.Member));

        _logger.LogInformation("Agent {AgentId} ({DisplayName}) created by API key {ApiKeyId}",
            agent.Id, agent.DisplayName, apiKeyId);

        return CreatedAtAction(nameof(ListAgents), new AgentResponse(
            agent.Id,
            agent.DisplayName,
            agent.Bio,
            agent.IsAgent,
            agent.CreatedAt));
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AgentResponse>>> ListAgents()
    {
        var apiKeyIdStr = User.FindFirstValue("api_key_id");
        if (!Guid.TryParse(apiKeyIdStr, out var apiKeyId))
        {
            return Unauthorized("Invalid API key.");
        }

        var agents = await _dbContext.Users
            .Where(u => u.IsAgent && u.CreatedByApiKeyId == apiKeyId)
            .Select(u => new AgentResponse(
                u.Id,
                u.DisplayName,
                u.Bio,
                u.IsAgent,
                u.CreatedAt))
            .ToListAsync();

        return Ok(agents);
    }
}
