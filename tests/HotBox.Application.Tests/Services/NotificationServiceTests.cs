using FluentAssertions;
using HotBox.Application.Hubs;
using HotBox.Application.Models;
using HotBox.Application.Services;
using HotBox.Core.Entities;
using HotBox.Core.Enums;
using HotBox.Core.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MockQueryable.NSubstitute;
using NSubstitute;

namespace HotBox.Application.Tests.Services;

public class NotificationServiceTests
{
    private readonly INotificationRepository _notificationRepository;
    private readonly IPresenceService _presenceService;
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly UserManager<AppUser> _userManager;
    private readonly ILogger<NotificationService> _logger;
    private readonly NotificationService _sut;

    public NotificationServiceTests()
    {
        _notificationRepository = Substitute.For<INotificationRepository>();
        _presenceService = Substitute.For<IPresenceService>();
        _hubContext = Substitute.For<IHubContext<ChatHub>>();
        _userManager = SubstituteUserManager();
        _logger = Substitute.For<ILogger<NotificationService>>();
        _sut = new NotificationService(
            _notificationRepository,
            _presenceService,
            _hubContext,
            _userManager,
            _logger);
    }

    [Fact]
    public async Task CreateAsync_WithSenderEqualsRecipient_DoesNotPersistOrSendSignalR()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        await _sut.CreateAsync(
            NotificationType.Mention,
            userId,
            userId,
            "Sender Name",
            "Test message",
            Guid.NewGuid(),
            NotificationSourceType.Channel,
            "test-channel");

        // Assert
        await _notificationRepository.DidNotReceiveWithAnyArgs()
            .CreateAsync(default!, default);
        _hubContext.Clients.DidNotReceiveWithAnyArgs()
            .User(default!);
    }

    [Fact]
    public async Task CreateAsync_WithValidData_PersistsNotification()
    {
        // Arrange
        var senderId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var sourceId = Guid.NewGuid();
        var senderDisplayName = "Sender Name";
        var messagePreview = "Test message content";
        var sourceName = "test-channel";

        _presenceService.GetStatus(recipientId).Returns(UserStatus.Online);
        _notificationRepository.IsSourceMutedAsync(recipientId, NotificationSourceType.Channel, sourceId, Arg.Any<CancellationToken>())
            .Returns(false);
        _notificationRepository.CreateAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                var notification = args.ArgAt<Notification>(0);
                notification.GetType().GetProperty("Id")!.SetValue(notification, Guid.NewGuid());
                return notification;
            });

        // Act
        await _sut.CreateAsync(
            NotificationType.Mention,
            senderId,
            recipientId,
            senderDisplayName,
            messagePreview,
            sourceId,
            NotificationSourceType.Channel,
            sourceName);

        // Assert
        await _notificationRepository.Received(1).CreateAsync(
            Arg.Is<Notification>(n =>
                n.Type == NotificationType.Mention &&
                n.SenderId == senderId &&
                n.RecipientId == recipientId &&
                n.SourceId == sourceId &&
                n.SourceType == NotificationSourceType.Channel &&
                n.PayloadJson.Contains(senderDisplayName) &&
                n.PayloadJson.Contains(messagePreview) &&
                n.PayloadJson.Contains(sourceName) &&
                n.CreatedAtUtc != default),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_WithDoNotDisturbStatus_PersistsButDoesNotSendSignalR()
    {
        // Arrange
        var senderId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var sourceId = Guid.NewGuid();

        _presenceService.GetStatus(recipientId).Returns(UserStatus.DoNotDisturb);
        _notificationRepository.CreateAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>())
            .Returns(args => args.ArgAt<Notification>(0));

        // Act
        await _sut.CreateAsync(
            NotificationType.Mention,
            senderId,
            recipientId,
            "Sender",
            "Message",
            sourceId,
            NotificationSourceType.Channel,
            "channel");

        // Assert
        await _notificationRepository.Received(1).CreateAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>());
        _hubContext.Clients.DidNotReceiveWithAnyArgs().User(default!);
    }

    [Fact]
    public async Task CreateAsync_WithMutedSource_PersistsButDoesNotSendSignalR()
    {
        // Arrange
        var senderId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var sourceId = Guid.NewGuid();

        _presenceService.GetStatus(recipientId).Returns(UserStatus.Online);
        _notificationRepository.IsSourceMutedAsync(recipientId, NotificationSourceType.Channel, sourceId, Arg.Any<CancellationToken>())
            .Returns(true);
        _notificationRepository.CreateAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>())
            .Returns(args => args.ArgAt<Notification>(0));

        // Act
        await _sut.CreateAsync(
            NotificationType.Mention,
            senderId,
            recipientId,
            "Sender",
            "Message",
            sourceId,
            NotificationSourceType.Channel,
            "channel");

        // Assert
        await _notificationRepository.Received(1).CreateAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>());
        _hubContext.Clients.DidNotReceiveWithAnyArgs().User(default!);
    }

    [Fact]
    public async Task CreateAsync_WithAllChecksPassed_SendsSignalRToRecipient()
    {
        // Arrange
        var senderId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var sourceId = Guid.NewGuid();
        var notificationId = Guid.NewGuid();
        var senderDisplayName = "Sender Name";
        var messagePreview = "Test message";
        var sourceName = "test-channel";

        _presenceService.GetStatus(recipientId).Returns(UserStatus.Online);
        _notificationRepository.IsSourceMutedAsync(recipientId, NotificationSourceType.Channel, sourceId, Arg.Any<CancellationToken>())
            .Returns(false);
        _notificationRepository.CreateAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                var notification = args.ArgAt<Notification>(0);
                notification.GetType().GetProperty("Id")!.SetValue(notification, notificationId);
                return notification;
            });

        var mockClientProxy = Substitute.For<IClientProxy>();
        _hubContext.Clients.User(recipientId.ToString()).Returns(mockClientProxy);

        // Act
        await _sut.CreateAsync(
            NotificationType.Mention,
            senderId,
            recipientId,
            senderDisplayName,
            messagePreview,
            sourceId,
            NotificationSourceType.Channel,
            sourceName);

        // Assert
        _hubContext.Clients.Received(1).User(recipientId.ToString());
        await mockClientProxy.Received(1).SendCoreAsync(
            "ReceiveNotification",
            Arg.Any<object[]>(),
            Arg.Any<CancellationToken>());

        // Verify the notification content
        var calls = mockClientProxy.ReceivedCalls().ToList();
        var sendCall = calls.First(c => c.GetMethodInfo().Name == "SendCoreAsync");
        var args = sendCall.GetArguments()[1] as object[];
        args.Should().NotBeNull();
        args!.Length.Should().Be(1);
        var response = args[0] as NotificationResponse;
        response.Should().NotBeNull();
        response!.Id.Should().Be(notificationId);
        response.Type.Should().Be(NotificationType.Mention);
        response.SenderId.Should().Be(senderId);
        response.SenderDisplayName.Should().Be(senderDisplayName);
        response.MessagePreview.Should().Be(messagePreview);
        response.SourceId.Should().Be(sourceId);
        response.SourceType.Should().Be(NotificationSourceType.Channel);
        response.SourceName.Should().Be(sourceName);
        response.ReadAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task ProcessMentionNotificationsAsync_WithNoMentions_DoesNotCreateNotifications()
    {
        // Arrange
        var content = "This message has no mentions at all";

        // Act
        await _sut.ProcessMentionNotificationsAsync(
            Guid.NewGuid(),
            "Sender",
            Guid.NewGuid(),
            "channel",
            content);

        // Assert
        await _notificationRepository.DidNotReceiveWithAnyArgs()
            .CreateAsync(default!, default);
    }

    [Fact]
    public async Task ProcessMentionNotificationsAsync_WithMentions_CreatesNotificationsForEachUser()
    {
        // Arrange
        var senderId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var user1Id = Guid.NewGuid();
        var user2Id = Guid.NewGuid();
        var content = "Hey @alice and @bob, check this out!";

        var users = new List<AppUser>
        {
            new() { Id = user1Id, UserName = "alice", DisplayName = "Alice" },
            new() { Id = user2Id, UserName = "bob", DisplayName = "Bob" }
        };

        SetupUserManagerQueryable(users);

        _presenceService.GetStatus(Arg.Any<Guid>()).Returns(UserStatus.Online);
        _notificationRepository.IsSourceMutedAsync(Arg.Any<Guid>(), Arg.Any<NotificationSourceType>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(false);
        _notificationRepository.CreateAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>())
            .Returns(args => args.ArgAt<Notification>(0));

        // Act
        await _sut.ProcessMentionNotificationsAsync(
            senderId,
            "Sender Name",
            channelId,
            "test-channel",
            content);

        // Assert - Wait briefly for async calls to complete
        await Task.Delay(50);
        await _notificationRepository.Received(2).CreateAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessMentionNotificationsAsync_WithSelfMention_DoesNotNotifySender()
    {
        // Arrange
        var senderId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var content = "I'm @sender mentioning myself";

        var users = new List<AppUser>
        {
            new() { Id = senderId, UserName = "sender", DisplayName = "Sender Name" }
        };

        SetupUserManagerQueryable(users);

        _presenceService.GetStatus(Arg.Any<Guid>()).Returns(UserStatus.Online);

        // Act
        await _sut.ProcessMentionNotificationsAsync(
            senderId,
            "Sender Name",
            channelId,
            "test-channel",
            content);

        // Assert - Self-mention should be skipped by CreateAsync
        await Task.Delay(50);
        await _notificationRepository.DidNotReceiveWithAnyArgs().CreateAsync(default!, default);
    }

    [Fact]
    public async Task ProcessMentionNotificationsAsync_WithLongMessage_TruncatesPreview()
    {
        // Arrange
        var senderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var longContent = new string('a', 150) + " @alice";

        var users = new List<AppUser>
        {
            new() { Id = userId, UserName = "alice", DisplayName = "Alice" }
        };

        SetupUserManagerQueryable(users);

        _presenceService.GetStatus(userId).Returns(UserStatus.Online);
        _notificationRepository.IsSourceMutedAsync(userId, NotificationSourceType.Channel, channelId, Arg.Any<CancellationToken>())
            .Returns(false);

        Notification? capturedNotification = null;
        _notificationRepository.CreateAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                capturedNotification = args.ArgAt<Notification>(0);
                return capturedNotification;
            });

        var mockClientProxy = Substitute.For<IClientProxy>();
        _hubContext.Clients.User(userId.ToString()).Returns(mockClientProxy);

        // Act
        await _sut.ProcessMentionNotificationsAsync(
            senderId,
            "Sender Name",
            channelId,
            "test-channel",
            longContent);

        // Assert
        await Task.Delay(50);
        capturedNotification.Should().NotBeNull();
        capturedNotification!.PayloadJson.Should().Contain("messagePreview");

        // The preview should be truncated to 100 characters
        await mockClientProxy.Received().SendCoreAsync(
            "ReceiveNotification",
            Arg.Any<object[]>(),
            Arg.Any<CancellationToken>());

        var calls = mockClientProxy.ReceivedCalls().ToList();
        var sendCall = calls.First(c => c.GetMethodInfo().Name == "SendCoreAsync");
        var args = sendCall.GetArguments()[1] as object[];
        var response = args![0] as NotificationResponse;
        response!.MessagePreview.Length.Should().Be(100);
    }

    [Fact]
    public async Task ProcessMentionNotificationsAsync_WithDuplicateMentions_CreatesOnlyOneNotification()
    {
        // Arrange
        var senderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var content = "Hey @alice, @alice, and @ALICE!"; // Multiple mentions of same user, different case

        var users = new List<AppUser>
        {
            new() { Id = userId, UserName = "alice", DisplayName = "Alice" }
        };

        SetupUserManagerQueryable(users);

        _presenceService.GetStatus(userId).Returns(UserStatus.Online);
        _notificationRepository.IsSourceMutedAsync(userId, NotificationSourceType.Channel, channelId, Arg.Any<CancellationToken>())
            .Returns(false);
        _notificationRepository.CreateAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>())
            .Returns(args => args.ArgAt<Notification>(0));

        // Act
        await _sut.ProcessMentionNotificationsAsync(
            senderId,
            "Sender Name",
            channelId,
            "test-channel",
            content);

        // Assert
        await Task.Delay(50);
        await _notificationRepository.Received(1).CreateAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>());
    }

    private static UserManager<AppUser> SubstituteUserManager()
    {
        var store = Substitute.For<IUserStore<AppUser>>();
        var userManager = Substitute.For<UserManager<AppUser>>(
            store,
            null, null, null, null, null, null, null, null);
        return userManager;
    }

    private void SetupUserManagerQueryable(List<AppUser> users)
    {
        var mockSet = users.AsQueryable().BuildMockDbSet();
        _userManager.Users.Returns(mockSet);
    }
}
