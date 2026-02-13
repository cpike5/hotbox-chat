using System.Security.Claims;
using FluentAssertions;
using HotBox.Application.Middleware;
using HotBox.Core.Entities;
using HotBox.Core.Interfaces;
using HotBox.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace HotBox.Application.Tests.Middleware;

public class AgentPresenceMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WithAgentClaim_TouchesAgentPresence()
    {
        // Arrange
        var presenceService = Substitute.For<IPresenceService>();
        var dbContext = CreateDbContext();
        var userId = Guid.NewGuid();
        var httpContext = BuildHttpContext("/api/channels", userId, "true", "Agent One");
        var nextCalled = false;
        var middleware = new AgentPresenceMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        // Act
        await middleware.InvokeAsync(httpContext, presenceService, dbContext);

        // Assert
        await presenceService.Received(1).TouchAgentActivityAsync(userId, "Agent One");
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_WithoutAgentClaim_UsesDatabaseFallback()
    {
        // Arrange
        var presenceService = Substitute.For<IPresenceService>();
        var dbContext = CreateDbContext();
        var userId = Guid.NewGuid();
        dbContext.Users.Add(new AppUser
        {
            Id = userId,
            UserName = "agent@example.com",
            Email = "agent@example.com",
            DisplayName = "Legacy Agent",
            IsAgent = true,
        });
        await dbContext.SaveChangesAsync();

        var httpContext = BuildHttpContext("/api/channels", userId, isAgentClaim: null, displayName: null);
        var middleware = new AgentPresenceMiddleware(_ => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(httpContext, presenceService, dbContext);

        // Assert
        await presenceService.Received(1).TouchAgentActivityAsync(userId, "Legacy Agent");
    }

    [Fact]
    public async Task InvokeAsync_WithHumanClaim_DoesNotTouchPresence()
    {
        // Arrange
        var presenceService = Substitute.For<IPresenceService>();
        var dbContext = CreateDbContext();
        var userId = Guid.NewGuid();
        var httpContext = BuildHttpContext("/api/channels", userId, "false", "Human User");
        var middleware = new AgentPresenceMiddleware(_ => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(httpContext, presenceService, dbContext);

        // Assert
        await presenceService.DidNotReceiveWithAnyArgs().TouchAgentActivityAsync(default, default!);
    }

    private static HotBoxDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HotBoxDbContext>()
            .UseInMemoryDatabase($"AgentPresenceMiddlewareTests_{Guid.NewGuid()}")
            .Options;

        return new HotBoxDbContext(options);
    }

    private static HttpContext BuildHttpContext(
        string path,
        Guid userId,
        string? isAgentClaim,
        string? displayName)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
        };

        if (isAgentClaim is not null)
        {
            claims.Add(new Claim("is_agent", isAgentClaim));
        }

        if (displayName is not null)
        {
            claims.Add(new Claim("display_name", displayName));
        }

        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var context = new DefaultHttpContext
        {
            User = principal,
        };
        context.Request.Path = path;
        return context;
    }
}
