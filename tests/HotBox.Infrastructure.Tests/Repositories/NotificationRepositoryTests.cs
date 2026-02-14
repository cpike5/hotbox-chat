using FluentAssertions;
using HotBox.Core.Entities;
using HotBox.Core.Enums;
using HotBox.Infrastructure.Data;
using HotBox.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace HotBox.Infrastructure.Tests.Repositories;

public class NotificationRepositoryTests : IDisposable
{
    private readonly HotBoxDbContext _context;
    private readonly NotificationRepository _sut;

    public NotificationRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<HotBoxDbContext>()
            .UseInMemoryDatabase($"NotificationRepositoryTests_{Guid.NewGuid()}")
            .Options;

        _context = new HotBoxDbContext(options);

        _sut = new NotificationRepository(_context);
    }

    [Fact]
    public async Task CreateAsync_PersistsNotificationAndReturnsWithId()
    {
        // Arrange
        var sender = await CreateUserAsync("sender@example.com", "Sender");
        var recipient = await CreateUserAsync("recipient@example.com", "Recipient");

        var notification = new Notification
        {
            Type = NotificationType.Mention,
            RecipientId = recipient.Id,
            SenderId = sender.Id,
            PayloadJson = "{\"test\":\"data\"}",
            SourceId = Guid.NewGuid(),
            SourceType = NotificationSourceType.Channel,
            CreatedAtUtc = DateTime.UtcNow
        };

        // Act
        var result = await _sut.CreateAsync(notification);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().NotBe(Guid.Empty);
        result.Type.Should().Be(NotificationType.Mention);
        result.RecipientId.Should().Be(recipient.Id);
        result.SenderId.Should().Be(sender.Id);

        // Verify persisted in database
        var fromDb = await _context.Notifications.FindAsync(result.Id);
        fromDb.Should().NotBeNull();
        fromDb!.PayloadJson.Should().Be("{\"test\":\"data\"}");
    }

    [Fact]
    public async Task GetByRecipientAsync_ReturnsNotificationsOrderedByCreatedAtDesc()
    {
        // Arrange
        var sender = await CreateUserAsync("sender@example.com", "Sender");
        var recipient = await CreateUserAsync("recipient@example.com", "Recipient");

        var now = DateTime.UtcNow;
        var notification1 = await CreateNotificationAsync(sender.Id, recipient.Id, now.AddMinutes(-10));
        var notification2 = await CreateNotificationAsync(sender.Id, recipient.Id, now.AddMinutes(-5));
        var notification3 = await CreateNotificationAsync(sender.Id, recipient.Id, now);

        // Act
        var result = await _sut.GetByRecipientAsync(recipient.Id);

        // Assert
        result.Should().HaveCount(3);
        result[0].Id.Should().Be(notification3.Id); // Most recent first
        result[1].Id.Should().Be(notification2.Id);
        result[2].Id.Should().Be(notification1.Id);
    }

    [Fact]
    public async Task GetByRecipientAsync_WithBeforeParameter_ReturnsOnlyEarlierNotifications()
    {
        // Arrange
        var sender = await CreateUserAsync("sender@example.com", "Sender");
        var recipient = await CreateUserAsync("recipient@example.com", "Recipient");

        var now = DateTime.UtcNow;
        var notification1 = await CreateNotificationAsync(sender.Id, recipient.Id, now.AddMinutes(-10));
        var notification2 = await CreateNotificationAsync(sender.Id, recipient.Id, now.AddMinutes(-5));
        var notification3 = await CreateNotificationAsync(sender.Id, recipient.Id, now);

        // Act
        var result = await _sut.GetByRecipientAsync(recipient.Id, before: now.AddMinutes(-3));

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(n => n.Id == notification1.Id);
        result.Should().Contain(n => n.Id == notification2.Id);
        result.Should().NotContain(n => n.Id == notification3.Id);
    }

    [Fact]
    public async Task GetByRecipientAsync_RespectsLimitParameter()
    {
        // Arrange
        var sender = await CreateUserAsync("sender@example.com", "Sender");
        var recipient = await CreateUserAsync("recipient@example.com", "Recipient");

        for (int i = 0; i < 10; i++)
        {
            await CreateNotificationAsync(sender.Id, recipient.Id, DateTime.UtcNow.AddMinutes(-i));
        }

        // Act
        var result = await _sut.GetByRecipientAsync(recipient.Id, limit: 5);

        // Assert
        result.Should().HaveCount(5);
    }

    [Fact]
    public async Task GetByRecipientAsync_IncludesSenderNavigationProperty()
    {
        // Arrange
        var sender = await CreateUserAsync("sender@example.com", "Sender Name");
        var recipient = await CreateUserAsync("recipient@example.com", "Recipient");

        await CreateNotificationAsync(sender.Id, recipient.Id, DateTime.UtcNow);

        // Act
        var result = await _sut.GetByRecipientAsync(recipient.Id);

        // Assert
        result.Should().HaveCount(1);
        result[0].Sender.Should().NotBeNull();
        result[0].Sender.DisplayName.Should().Be("Sender Name");
    }

    [Fact]
    public async Task GetByRecipientAsync_WithNoNotifications_ReturnsEmptyList()
    {
        // Arrange
        var recipient = await CreateUserAsync("recipient@example.com", "Recipient");

        // Act
        var result = await _sut.GetByRecipientAsync(recipient.Id);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUnreadCountAsync_ReturnsCorrectCount()
    {
        // Arrange
        var sender = await CreateUserAsync("sender@example.com", "Sender");
        var recipient = await CreateUserAsync("recipient@example.com", "Recipient");

        await CreateNotificationAsync(sender.Id, recipient.Id, DateTime.UtcNow, readAt: null);
        await CreateNotificationAsync(sender.Id, recipient.Id, DateTime.UtcNow, readAt: null);
        await CreateNotificationAsync(sender.Id, recipient.Id, DateTime.UtcNow, readAt: DateTime.UtcNow);

        // Act
        var result = await _sut.GetUnreadCountAsync(recipient.Id);

        // Assert
        result.Should().Be(2);
    }

    [Fact]
    public async Task GetUnreadCountAsync_WithNoUnreadNotifications_ReturnsZero()
    {
        // Arrange
        var sender = await CreateUserAsync("sender@example.com", "Sender");
        var recipient = await CreateUserAsync("recipient@example.com", "Recipient");

        await CreateNotificationAsync(sender.Id, recipient.Id, DateTime.UtcNow, readAt: DateTime.UtcNow);

        // Act
        var result = await _sut.GetUnreadCountAsync(recipient.Id);

        // Assert
        result.Should().Be(0);
    }

    [Fact(Skip = "ExecuteUpdateAsync not supported by InMemory provider")]
    public async Task MarkAllAsReadAsync_SetsReadAtUtcOnUnreadNotifications()
    {
        // Arrange
        var sender = await CreateUserAsync("sender@example.com", "Sender");
        var recipient = await CreateUserAsync("recipient@example.com", "Recipient");

        var notification1 = await CreateNotificationAsync(sender.Id, recipient.Id, DateTime.UtcNow, readAt: null);
        var notification2 = await CreateNotificationAsync(sender.Id, recipient.Id, DateTime.UtcNow, readAt: null);
        var notification3 = await CreateNotificationAsync(sender.Id, recipient.Id, DateTime.UtcNow, readAt: DateTime.UtcNow.AddMinutes(-5));

        // Act
        await _sut.MarkAllAsReadAsync(recipient.Id);

        // Assert
        var updated1 = await _context.Notifications.FindAsync(notification1.Id);
        var updated2 = await _context.Notifications.FindAsync(notification2.Id);
        var updated3 = await _context.Notifications.FindAsync(notification3.Id);

        updated1!.ReadAtUtc.Should().NotBeNull();
        updated1.ReadAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));

        updated2!.ReadAtUtc.Should().NotBeNull();
        updated2.ReadAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));

        // Already read notification should keep its original timestamp
        updated3!.ReadAtUtc.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(-5), TimeSpan.FromSeconds(1));
    }

    [Fact(Skip = "ExecuteUpdateAsync not supported by InMemory provider")]
    public async Task MarkAllAsReadAsync_WithNoUnreadNotifications_DoesNotThrow()
    {
        // Arrange
        var recipient = await CreateUserAsync("recipient@example.com", "Recipient");

        // Act
        var act = () => _sut.MarkAllAsReadAsync(recipient.Id);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task IsSourceMutedAsync_WithMutedSource_ReturnsTrue()
    {
        // Arrange
        var user = await CreateUserAsync("user@example.com", "User");
        var sourceId = Guid.NewGuid();

        await CreatePreferenceAsync(user.Id, NotificationSourceType.Channel, sourceId, isMuted: true);

        // Act
        var result = await _sut.IsSourceMutedAsync(user.Id, NotificationSourceType.Channel, sourceId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsSourceMutedAsync_WithUnmutedSource_ReturnsFalse()
    {
        // Arrange
        var user = await CreateUserAsync("user@example.com", "User");
        var sourceId = Guid.NewGuid();

        await CreatePreferenceAsync(user.Id, NotificationSourceType.Channel, sourceId, isMuted: false);

        // Act
        var result = await _sut.IsSourceMutedAsync(user.Id, NotificationSourceType.Channel, sourceId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsSourceMutedAsync_WithNoPreference_ReturnsFalse()
    {
        // Arrange
        var user = await CreateUserAsync("user@example.com", "User");
        var sourceId = Guid.NewGuid();

        // Act
        var result = await _sut.IsSourceMutedAsync(user.Id, NotificationSourceType.Channel, sourceId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetPreferencesAsync_ReturnsAllPreferencesForUser()
    {
        // Arrange
        var user = await CreateUserAsync("user@example.com", "User");
        var source1 = Guid.NewGuid();
        var source2 = Guid.NewGuid();

        await CreatePreferenceAsync(user.Id, NotificationSourceType.Channel, source1, isMuted: true);
        await CreatePreferenceAsync(user.Id, NotificationSourceType.DirectMessage, source2, isMuted: false);

        // Act
        var result = await _sut.GetPreferencesAsync(user.Id);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(p => p.SourceId == source1 && p.IsMuted);
        result.Should().Contain(p => p.SourceId == source2 && !p.IsMuted);
    }

    [Fact]
    public async Task GetPreferencesAsync_WithNoPreferences_ReturnsEmptyList()
    {
        // Arrange
        var user = await CreateUserAsync("user@example.com", "User");

        // Act
        var result = await _sut.GetPreferencesAsync(user.Id);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SetMutePreferenceAsync_WithNoExistingPreference_CreatesNew()
    {
        // Arrange
        var user = await CreateUserAsync("user@example.com", "User");
        var sourceId = Guid.NewGuid();

        // Act
        await _sut.SetMutePreferenceAsync(user.Id, NotificationSourceType.Channel, sourceId, isMuted: true);

        // Assert
        var preferences = await _context.UserNotificationPreferences
            .Where(p => p.UserId == user.Id && p.SourceId == sourceId)
            .ToListAsync();

        preferences.Should().HaveCount(1);
        preferences[0].IsMuted.Should().BeTrue();
        preferences[0].SourceType.Should().Be(NotificationSourceType.Channel);
        preferences[0].CreatedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        preferences[0].UpdatedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task SetMutePreferenceAsync_WithExistingPreference_UpdatesExisting()
    {
        // Arrange
        var user = await CreateUserAsync("user@example.com", "User");
        var sourceId = Guid.NewGuid();

        var originalPreference = await CreatePreferenceAsync(user.Id, NotificationSourceType.Channel, sourceId, isMuted: false);
        var originalCreatedAt = originalPreference.CreatedAtUtc;

        // Small delay to ensure UpdatedAtUtc is different
        await Task.Delay(10);

        // Act
        await _sut.SetMutePreferenceAsync(user.Id, NotificationSourceType.Channel, sourceId, isMuted: true);

        // Assert
        var preferences = await _context.UserNotificationPreferences
            .Where(p => p.UserId == user.Id && p.SourceId == sourceId)
            .ToListAsync();

        preferences.Should().HaveCount(1);
        preferences[0].Id.Should().Be(originalPreference.Id);
        preferences[0].IsMuted.Should().BeTrue();
        preferences[0].CreatedAtUtc.Should().Be(originalCreatedAt);
        preferences[0].UpdatedAtUtc.Should().BeAfter(originalCreatedAt);
    }

    [Fact]
    public async Task SetMutePreferenceAsync_WithUnmute_UpdatesToFalse()
    {
        // Arrange
        var user = await CreateUserAsync("user@example.com", "User");
        var sourceId = Guid.NewGuid();

        await CreatePreferenceAsync(user.Id, NotificationSourceType.Channel, sourceId, isMuted: true);

        // Act
        await _sut.SetMutePreferenceAsync(user.Id, NotificationSourceType.Channel, sourceId, isMuted: false);

        // Assert
        var preference = await _context.UserNotificationPreferences
            .FirstOrDefaultAsync(p => p.UserId == user.Id && p.SourceId == sourceId);

        preference.Should().NotBeNull();
        preference!.IsMuted.Should().BeFalse();
    }

    private async Task<AppUser> CreateUserAsync(string email, string displayName)
    {
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            DisplayName = displayName,
            NormalizedUserName = email.ToUpperInvariant(),
            NormalizedEmail = email.ToUpperInvariant()
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    private async Task<Notification> CreateNotificationAsync(
        Guid senderId,
        Guid recipientId,
        DateTime createdAt,
        DateTime? readAt = null)
    {
        var notification = new Notification
        {
            Type = NotificationType.Mention,
            RecipientId = recipientId,
            SenderId = senderId,
            PayloadJson = "{\"test\":\"data\"}",
            SourceId = Guid.NewGuid(),
            SourceType = NotificationSourceType.Channel,
            CreatedAtUtc = createdAt,
            ReadAtUtc = readAt
        };

        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync();
        return notification;
    }

    private async Task<UserNotificationPreference> CreatePreferenceAsync(
        Guid userId,
        NotificationSourceType sourceType,
        Guid sourceId,
        bool isMuted)
    {
        var preference = new UserNotificationPreference
        {
            UserId = userId,
            SourceType = sourceType,
            SourceId = sourceId,
            IsMuted = isMuted,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        _context.UserNotificationPreferences.Add(preference);
        await _context.SaveChangesAsync();
        return preference;
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
