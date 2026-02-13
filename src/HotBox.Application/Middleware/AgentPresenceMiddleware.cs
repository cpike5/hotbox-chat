using System.Security.Claims;
using HotBox.Core.Interfaces;
using HotBox.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HotBox.Application.Middleware;

public sealed class AgentPresenceMiddleware
{
    private readonly RequestDelegate _next;

    public AgentPresenceMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IPresenceService presenceService,
        HotBoxDbContext dbContext)
    {
        if (context.Request.Path.StartsWithSegments("/api")
            && context.User?.Identity?.IsAuthenticated == true)
        {
            await TouchAgentPresenceAsync(context, presenceService, dbContext);
        }

        await _next(context);
    }

    private static async Task TouchAgentPresenceAsync(
        HttpContext context,
        IPresenceService presenceService,
        HotBoxDbContext dbContext)
    {
        var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return;
        }

        var isAgentClaim = context.User.FindFirst("is_agent")?.Value;
        if (bool.TryParse(isAgentClaim, out var isAgentFromClaim))
        {
            if (!isAgentFromClaim)
            {
                return;
            }

            var displayName = context.User.FindFirst("display_name")?.Value ?? "Unknown";
            await presenceService.TouchAgentActivityAsync(userId, displayName);
            return;
        }

        var user = await dbContext.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.DisplayName, u.IsAgent })
            .FirstOrDefaultAsync(context.RequestAborted);

        if (user?.IsAgent == true)
        {
            await presenceService.TouchAgentActivityAsync(userId, user.DisplayName);
        }
    }
}
