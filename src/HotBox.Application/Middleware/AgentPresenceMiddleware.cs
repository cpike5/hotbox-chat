using System.Security.Claims;
using HotBox.Infrastructure.Services;

namespace HotBox.Application.Middleware;

public class AgentPresenceMiddleware
{
    private readonly RequestDelegate _next;
    private readonly PresenceService _presenceService;

    public AgentPresenceMiddleware(RequestDelegate next, PresenceService presenceService)
    {
        _next = next;
        _presenceService = presenceService;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var isAgent = context.User.FindFirstValue("is_agent");
            if (isAgent == "true")
            {
                var userIdStr = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (Guid.TryParse(userIdStr, out var userId))
                {
                    var displayName = context.User.FindFirstValue("api_key_name") ?? "Agent";
                    await _presenceService.TouchAgentActivityAsync(userId, displayName);
                }
            }
        }

        await _next(context);
    }
}
