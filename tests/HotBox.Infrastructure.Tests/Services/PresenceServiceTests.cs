using FluentAssertions;
using HotBox.Core.Enums;
using HotBox.Core.Options;
using HotBox.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace HotBox.Infrastructure.Tests.Services;

public class PresenceServiceTests : IDisposable
{
    private readonly ILogger<PresenceService> _logger;
    private readonly PresenceService _sut;

    public PresenceServiceTests()
    {
        _logger = Substitute.For<ILogger<PresenceService>>();
        _sut = new PresenceService(_logger);
    }

    [Fact]
    public async Task SetOnlineAsync_MarksUserAsOnline()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var connectionId = "conn-1";
        var displayName = "Test User";

        // Act
        await _sut.SetOnlineAsync(userId, connectionId, displayName);

        // Assert
        var status = _sut.GetStatus(userId);
        status.Should().Be(UserStatus.Online);
    }

    [Fact]
    public async Task SetOnlineAsync_RaisesStatusChangedEvent()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var connectionId = "conn-1";
        var displayName = "Test User";
        Guid? eventUserId = null;
        string? eventDisplayName = null;
        UserStatus? eventStatus = null;

        _sut.OnUserStatusChanged += (uid, name, status, _) =>
        {
            eventUserId = uid;
            eventDisplayName = name;
            eventStatus = status;
        };

        // Act
        await _sut.SetOnlineAsync(userId, connectionId, displayName);

        // Assert
        eventUserId.Should().Be(userId);
        eventDisplayName.Should().Be(displayName);
        eventStatus.Should().Be(UserStatus.Online);
    }

    [Fact]
    public async Task SetOnlineAsync_WithMultipleConnections_KeepsUserOnline()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var displayName = "Test User";

        // Act
        await _sut.SetOnlineAsync(userId, "conn-1", displayName);
        await _sut.SetOnlineAsync(userId, "conn-2", displayName);

        // Assert
        var status = _sut.GetStatus(userId);
        status.Should().Be(UserStatus.Online);
    }

    [Fact]
    public async Task SetIdleAsync_MarksUserAsIdle()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await _sut.SetOnlineAsync(userId, "conn-1", "Test User");

        // Act
        await _sut.SetIdleAsync(userId);

        // Assert
        var status = _sut.GetStatus(userId);
        status.Should().Be(UserStatus.Idle);
    }

    [Fact]
    public async Task SetIdleAsync_WithOfflineUser_DoesNothing()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        await _sut.SetIdleAsync(userId);

        // Assert
        var status = _sut.GetStatus(userId);
        status.Should().Be(UserStatus.Offline);
    }

    [Fact]
    public async Task SetDoNotDisturbAsync_MarksUserAsDoNotDisturb()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await _sut.SetOnlineAsync(userId, "conn-1", "Test User");

        // Act
        await _sut.SetDoNotDisturbAsync(userId);

        // Assert
        var status = _sut.GetStatus(userId);
        status.Should().Be(UserStatus.DoNotDisturb);
    }

    [Fact]
    public async Task SetOfflineAsync_MarksUserAsOffline()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await _sut.SetOnlineAsync(userId, "conn-1", "Test User");

        // Act
        await _sut.SetOfflineAsync(userId);

        // Assert
        var status = _sut.GetStatus(userId);
        status.Should().Be(UserStatus.Offline);
    }

    [Fact]
    public async Task SetOfflineAsync_RaisesStatusChangedEvent()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await _sut.SetOnlineAsync(userId, "conn-1", "Test User");

        UserStatus? eventStatus = null;
        _sut.OnUserStatusChanged += (_, _, status, _) => eventStatus = status;

        // Act
        await _sut.SetOfflineAsync(userId);

        // Assert
        eventStatus.Should().Be(UserStatus.Offline);
    }

    [Fact]
    public void GetStatus_WithOfflineUser_ReturnsOffline()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var status = _sut.GetStatus(userId);

        // Assert
        status.Should().Be(UserStatus.Offline);
    }

    [Fact]
    public async Task GetAllOnlineUsers_ReturnsOnlineUsers()
    {
        // Arrange
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();
        await _sut.SetOnlineAsync(user1, "conn-1", "User 1");
        await _sut.SetOnlineAsync(user2, "conn-2", "User 2");

        // Act
        var result = _sut.GetAllOnlineUsers();

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(u => u.UserId == user1 && u.DisplayName == "User 1" && u.Status == UserStatus.Online);
        result.Should().Contain(u => u.UserId == user2 && u.DisplayName == "User 2" && u.Status == UserStatus.Online);
    }

    [Fact]
    public async Task GetAllOnlineUsers_ExcludesOfflineUsers()
    {
        // Arrange
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();
        await _sut.SetOnlineAsync(user1, "conn-1", "User 1");
        await _sut.SetOnlineAsync(user2, "conn-2", "User 2");
        await _sut.SetOfflineAsync(user2);

        // Act
        var result = _sut.GetAllOnlineUsers();

        // Assert
        result.Should().HaveCount(1);
        result.Should().Contain(u => u.UserId == user1);
        result.Should().NotContain(u => u.UserId == user2);
    }

    [Fact]
    public async Task RemoveConnection_WithLastConnection_ReturnsTrue()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var connectionId = "conn-1";
        await _sut.SetOnlineAsync(userId, connectionId, "Test User");

        // Act
        var result = _sut.RemoveConnection(userId, connectionId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task RemoveConnection_WithMultipleConnections_ReturnsFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await _sut.SetOnlineAsync(userId, "conn-1", "Test User");
        await _sut.SetOnlineAsync(userId, "conn-2", "Test User");

        // Act
        var result = _sut.RemoveConnection(userId, "conn-1");

        // Assert
        result.Should().BeFalse();
        _sut.GetStatus(userId).Should().Be(UserStatus.Online);
    }

    [Fact]
    public async Task RemoveConnection_WithNonExistentConnection_ReturnsTrue()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var result = _sut.RemoveConnection(userId, "non-existent");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task RecordHeartbeat_UpdatesLastHeartbeat()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await _sut.SetOnlineAsync(userId, "conn-1", "Test User");
        await _sut.SetIdleAsync(userId);

        // Act
        _sut.RecordHeartbeat(userId);

        // Assert
        // Heartbeat should bring user back to Online from Idle
        var status = _sut.GetStatus(userId);
        status.Should().Be(UserStatus.Online);
    }

    [Fact]
    public void RecordHeartbeat_WithOfflineUser_DoesNothing()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        _sut.RecordHeartbeat(userId);

        // Assert
        var status = _sut.GetStatus(userId);
        status.Should().Be(UserStatus.Offline);
    }

    [Fact]
    public async Task SetIdleAsync_WithDoNotDisturbUser_DoesNotChangeStatus()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await _sut.SetOnlineAsync(userId, "conn-1", "Test User");
        await _sut.SetDoNotDisturbAsync(userId);

        // Act
        await _sut.SetIdleAsync(userId);

        // Assert
        var status = _sut.GetStatus(userId);
        status.Should().Be(UserStatus.DoNotDisturb); // Should remain DoNotDisturb
    }

    [Fact]
    public async Task SetOnlineAsync_CancelsGracePeriod()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await _sut.SetOnlineAsync(userId, "conn-1", "Test User");
        _sut.RemoveConnection(userId, "conn-1"); // Start grace period

        // Act - reconnect before grace period expires
        await _sut.SetOnlineAsync(userId, "conn-2", "Test User");

        // Small delay to ensure any pending operations complete
        await Task.Delay(100);

        // Assert - should still be online, grace period cancelled
        var status = _sut.GetStatus(userId);
        status.Should().Be(UserStatus.Online);
    }

    [Fact]
    public async Task TouchAgentActivityAsync_MarksAgentOnline()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var displayName = "Agent One";

        // Act
        await _sut.TouchAgentActivityAsync(userId, displayName);

        // Assert
        _sut.GetStatus(userId).Should().Be(UserStatus.Online);
        _sut.GetAllOnlineUsers().Should().Contain(u =>
            u.UserId == userId
            && u.DisplayName == displayName
            && u.Status == UserStatus.Online
            && u.IsAgent);
    }

    [Fact]
    public async Task TouchAgentActivityAsync_TransitionsToOfflineAfterInactivityTimeout()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var local = new PresenceService(
            _logger,
            Options.Create(new PresenceOptions
            {
                GracePeriod = TimeSpan.FromMilliseconds(50),
                IdleTimeout = TimeSpan.FromMinutes(5),
                AgentInactivityTimeout = TimeSpan.FromMilliseconds(100),
            }));

        try
        {
            await local.TouchAgentActivityAsync(userId, "Agent Timeout Test");

            // Act
            await Task.Delay(250);

            // Assert
            local.GetStatus(userId).Should().Be(UserStatus.Offline);
        }
        finally
        {
            local.Dispose();
        }
    }

    public void Dispose()
    {
        _sut.Dispose();
    }
}
