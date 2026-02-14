using System.Security.Claims;
using FluentAssertions;
using HotBox.Application.Controllers;
using HotBox.Application.Models;
using HotBox.Core.Entities;
using HotBox.Core.Enums;
using HotBox.Core.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace HotBox.Application.Tests.Controllers;

public class NotificationsControllerTests
{
    private readonly INotificationRepository _notificationRepository;
    private readonly ILogger<NotificationsController> _logger;
    private readonly NotificationsController _sut;

    public NotificationsControllerTests()
    {
        _notificationRepository = Substitute.For<INotificationRepository>();
        _logger = Substitute.For<ILogger<NotificationsController>>();
        _sut = new NotificationsController(_notificationRepository, _logger);
    }

    [Fact]
    public async Task GetNotifications_WithValidUser_ReturnsNotifications()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var senderId = Guid.NewGuid();
        var sender = new AppUser
        {
            Id = senderId,
            UserName = "sender@example.com",
            Email = "sender@example.com",
            DisplayName = "Sender Name"
        };

        var notifications = new List<Notification>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Type = NotificationType.Mention,
                RecipientId = userId,
                SenderId = senderId,
                Sender = sender,
                PayloadJson = "{\"senderDisplayName\":\"Sender Name\",\"messagePreview\":\"Test message\",\"sourceName\":\"test-channel\"}",
                SourceId = Guid.NewGuid(),
                SourceType = NotificationSourceType.Channel,
                CreatedAtUtc = DateTime.UtcNow,
                ReadAtUtc = null
            }
        };

        SetupUserContext(userId);
        _notificationRepository.GetByRecipientAsync(userId, null, 50, Arg.Any<CancellationToken>())
            .Returns(notifications);

        // Act
        var result = await _sut.GetNotifications();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        var response = okResult.Value as List<NotificationResponse>;
        response.Should().NotBeNull();
        response.Should().HaveCount(1);
        response![0].Type.Should().Be(NotificationType.Mention);
        response[0].SenderDisplayName.Should().Be("Sender Name");
        response[0].MessagePreview.Should().Be("Test message");
        response[0].SourceName.Should().Be("test-channel");
    }

    [Fact]
    public async Task GetNotifications_WithBeforeParameter_PassesToRepository()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var before = DateTime.UtcNow.AddHours(-1);

        SetupUserContext(userId);
        _notificationRepository.GetByRecipientAsync(userId, before, 50, Arg.Any<CancellationToken>())
            .Returns(new List<Notification>());

        // Act
        await _sut.GetNotifications(before: before);

        // Assert
        await _notificationRepository.Received(1).GetByRecipientAsync(userId, before, 50, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetNotifications_WithCustomLimit_PassesToRepository()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var limit = 25;

        SetupUserContext(userId);
        _notificationRepository.GetByRecipientAsync(userId, null, limit, Arg.Any<CancellationToken>())
            .Returns(new List<Notification>());

        // Act
        await _sut.GetNotifications(limit: limit);

        // Assert
        await _notificationRepository.Received(1).GetByRecipientAsync(userId, null, limit, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetNotifications_WithLimitLessThanOne_ReturnsBadRequest()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupUserContext(userId);

        // Act
        var result = await _sut.GetNotifications(limit: 0);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequest = (BadRequestObjectResult)result;
        badRequest.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task GetNotifications_WithLimitGreaterThan100_ReturnsBadRequest()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupUserContext(userId);

        // Act
        var result = await _sut.GetNotifications(limit: 101);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetNotifications_WithoutUserIdentity_ReturnsUnauthorized()
    {
        // Arrange
        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        // Act
        var result = await _sut.GetNotifications();

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetNotifications_WithInvalidUserId_ReturnsUnauthorized()
    {
        // Arrange
        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, "invalid-guid")
                }))
            }
        };

        // Act
        var result = await _sut.GetNotifications();

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetNotifications_WithMissingSender_UsesUnknownDisplayName()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var notifications = new List<Notification>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Type = NotificationType.Mention,
                RecipientId = userId,
                SenderId = Guid.NewGuid(),
                Sender = null!, // Missing sender
                PayloadJson = "{\"senderDisplayName\":\"Sender\",\"messagePreview\":\"Test\",\"sourceName\":\"channel\"}",
                SourceId = Guid.NewGuid(),
                SourceType = NotificationSourceType.Channel,
                CreatedAtUtc = DateTime.UtcNow
            }
        };

        SetupUserContext(userId);
        _notificationRepository.GetByRecipientAsync(userId, null, 50, Arg.Any<CancellationToken>())
            .Returns(notifications);

        // Act
        var result = await _sut.GetNotifications();

        // Assert
        var okResult = (OkObjectResult)result;
        var response = okResult.Value as List<NotificationResponse>;
        response![0].SenderDisplayName.Should().Be("Unknown");
    }

    [Fact]
    public async Task GetUnreadCount_WithValidUser_ReturnsCount()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupUserContext(userId);
        _notificationRepository.GetUnreadCountAsync(userId, Arg.Any<CancellationToken>())
            .Returns(5);

        // Act
        var result = await _sut.GetUnreadCount();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        okResult.Value.Should().Be(5);
    }

    [Fact]
    public async Task GetUnreadCount_WithoutUserIdentity_ReturnsUnauthorized()
    {
        // Arrange
        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        // Act
        var result = await _sut.GetUnreadCount();

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task MarkAllAsRead_WithValidUser_ReturnsNoContent()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupUserContext(userId);

        // Act
        var result = await _sut.MarkAllAsRead();

        // Assert
        result.Should().BeOfType<NoContentResult>();
        await _notificationRepository.Received(1).MarkAllAsReadAsync(userId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MarkAllAsRead_WithoutUserIdentity_ReturnsUnauthorized()
    {
        // Arrange
        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        // Act
        var result = await _sut.MarkAllAsRead();

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetPreferences_WithValidUser_ReturnsPreferences()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sourceId1 = Guid.NewGuid();
        var sourceId2 = Guid.NewGuid();

        var preferences = new List<UserNotificationPreference>
        {
            new()
            {
                UserId = userId,
                SourceType = NotificationSourceType.Channel,
                SourceId = sourceId1,
                IsMuted = true,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            },
            new()
            {
                UserId = userId,
                SourceType = NotificationSourceType.DirectMessage,
                SourceId = sourceId2,
                IsMuted = false,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            }
        };

        SetupUserContext(userId);
        _notificationRepository.GetPreferencesAsync(userId, Arg.Any<CancellationToken>())
            .Returns(preferences);

        // Act
        var result = await _sut.GetPreferences();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;

        // The controller returns anonymous objects via Select().ToList()
        okResult.Value.Should().NotBeNull();
        var response = okResult.Value as IEnumerable<object>;
        response.Should().NotBeNull();
        response!.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetPreferences_WithoutUserIdentity_ReturnsUnauthorized()
    {
        // Arrange
        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        // Act
        var result = await _sut.GetPreferences();

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task SetPreference_WithValidUser_ReturnsNoContent()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sourceId = Guid.NewGuid();
        var request = new SetPreferenceRequest
        {
            SourceType = NotificationSourceType.Channel,
            SourceId = sourceId,
            IsMuted = true
        };

        SetupUserContext(userId);

        // Act
        var result = await _sut.SetPreference(request);

        // Assert
        result.Should().BeOfType<NoContentResult>();
        await _notificationRepository.Received(1).SetMutePreferenceAsync(
            userId,
            NotificationSourceType.Channel,
            sourceId,
            true,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetPreference_WithUnmuteRequest_CallsRepositoryWithFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sourceId = Guid.NewGuid();
        var request = new SetPreferenceRequest
        {
            SourceType = NotificationSourceType.DirectMessage,
            SourceId = sourceId,
            IsMuted = false
        };

        SetupUserContext(userId);

        // Act
        await _sut.SetPreference(request);

        // Assert
        await _notificationRepository.Received(1).SetMutePreferenceAsync(
            userId,
            NotificationSourceType.DirectMessage,
            sourceId,
            false,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetPreference_WithoutUserIdentity_ReturnsUnauthorized()
    {
        // Arrange
        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        var request = new SetPreferenceRequest
        {
            SourceType = NotificationSourceType.Channel,
            SourceId = Guid.NewGuid(),
            IsMuted = true
        };

        // Act
        var result = await _sut.SetPreference(request);

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
    }

    private void SetupUserContext(Guid userId)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString())
        };

        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = principal
            }
        };
    }
}
