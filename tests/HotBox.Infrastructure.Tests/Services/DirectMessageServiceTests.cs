using FluentAssertions;
using HotBox.Core.Entities;
using HotBox.Core.Interfaces;
using HotBox.Core.Models;
using HotBox.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace HotBox.Infrastructure.Tests.Services;

public class DirectMessageServiceTests
{
    private readonly IDirectMessageRepository _repository;
    private readonly UserManager<AppUser> _userManager;
    private readonly ILogger<DirectMessageService> _logger;
    private readonly DirectMessageService _sut;

    public DirectMessageServiceTests()
    {
        _repository = Substitute.For<IDirectMessageRepository>();
        _userManager = Substitute.For<UserManager<AppUser>>(
            Substitute.For<IUserStore<AppUser>>(),
            null, null, null, null, null, null, null, null);
        _logger = Substitute.For<ILogger<DirectMessageService>>();
        _sut = new DirectMessageService(_repository, _userManager, _logger);
    }

    [Fact]
    public async Task SendAsync_WithValidData_CreatesDirectMessage()
    {
        // Arrange
        var senderId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var content = "Hello!";

        var sender = new AppUser { Id = senderId, DisplayName = "Sender" };
        var recipient = new AppUser { Id = recipientId, DisplayName = "Recipient" };

        _userManager.FindByIdAsync(senderId.ToString()).Returns(sender);
        _userManager.FindByIdAsync(recipientId.ToString()).Returns(recipient);
        _repository.CreateAsync(Arg.Any<DirectMessage>(), Arg.Any<CancellationToken>())
            .Returns(args => args.ArgAt<DirectMessage>(0));

        // Act
        var result = await _sut.SendAsync(senderId, recipientId, content);

        // Assert
        result.Should().NotBeNull();
        result.Content.Should().Be(content);
        result.SenderId.Should().Be(senderId);
        result.RecipientId.Should().Be(recipientId);
        result.CreatedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        await _repository.Received(1).CreateAsync(Arg.Any<DirectMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_WithEmptyContent_ThrowsArgumentException()
    {
        // Arrange
        var senderId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();

        // Act
        var act = () => _sut.SendAsync(senderId, recipientId, "");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Message content cannot be empty.*");
    }

    [Fact]
    public async Task SendAsync_WithSameUserAsSenderAndRecipient_ThrowsArgumentException()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var act = () => _sut.SendAsync(userId, userId, "Hello");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Cannot send a direct message to yourself.");
    }

    [Fact]
    public async Task SendAsync_WithNonExistentSender_ThrowsKeyNotFoundException()
    {
        // Arrange
        var senderId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();

        _userManager.FindByIdAsync(senderId.ToString()).Returns((AppUser?)null);

        // Act
        var act = () => _sut.SendAsync(senderId, recipientId, "Hello");

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage($"Sender {senderId} not found.");
    }

    [Fact]
    public async Task SendAsync_WithNonExistentRecipient_ThrowsKeyNotFoundException()
    {
        // Arrange
        var senderId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var sender = new AppUser { Id = senderId, DisplayName = "Sender" };

        _userManager.FindByIdAsync(senderId.ToString()).Returns(sender);
        _userManager.FindByIdAsync(recipientId.ToString()).Returns((AppUser?)null);

        // Act
        var act = () => _sut.SendAsync(senderId, recipientId, "Hello");

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage($"Recipient {recipientId} not found.");
    }

    [Fact]
    public async Task GetConversationAsync_ReturnsMessages()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        var messages = new List<DirectMessage>
        {
            new() { Id = Guid.NewGuid(), Content = "Message 1", SenderId = userId, RecipientId = otherUserId, CreatedAtUtc = DateTime.UtcNow.AddMinutes(-5) },
            new() { Id = Guid.NewGuid(), Content = "Message 2", SenderId = otherUserId, RecipientId = userId, CreatedAtUtc = DateTime.UtcNow }
        };

        _repository.GetConversationAsync(userId, otherUserId, null, 50, Arg.Any<CancellationToken>())
            .Returns(messages);

        // Act
        var result = await _sut.GetConversationAsync(userId, otherUserId);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(m => m.SenderId == userId && m.RecipientId == otherUserId);
        result.Should().Contain(m => m.SenderId == otherUserId && m.RecipientId == userId);
    }

    [Fact]
    public async Task GetConversationAsync_WithBeforeParameter_PassesCorrectly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var before = DateTime.UtcNow.AddHours(-1);

        _repository.GetConversationAsync(userId, otherUserId, before, 50, Arg.Any<CancellationToken>())
            .Returns(new List<DirectMessage>());

        // Act
        await _sut.GetConversationAsync(userId, otherUserId, before);

        // Assert
        await _repository.Received(1).GetConversationAsync(userId, otherUserId, before, 50, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetConversationAsync_WithCustomLimit_PassesCorrectly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var limit = 100;

        _repository.GetConversationAsync(userId, otherUserId, null, limit, Arg.Any<CancellationToken>())
            .Returns(new List<DirectMessage>());

        // Act
        await _sut.GetConversationAsync(userId, otherUserId, limit: limit);

        // Assert
        await _repository.Received(1).GetConversationAsync(userId, otherUserId, null, limit, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetConversationsAsync_ReturnsConversationSummaries()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var otherUserId1 = Guid.NewGuid();
        var otherUserId2 = Guid.NewGuid();

        var summaries = new List<ConversationSummary>
        {
            new(otherUserId1, "User 1", DateTime.UtcNow.AddHours(-1)),
            new(otherUserId2, "User 2", DateTime.UtcNow)
        };

        _repository.GetConversationsAsync(userId, Arg.Any<CancellationToken>()).Returns(summaries);

        // Act
        var result = await _sut.GetConversationsAsync(userId);

        // Assert
        result.Should().HaveCount(2);
        result.Should().ContainSingle(s => s.UserId == otherUserId1 && s.DisplayName == "User 1");
        result.Should().ContainSingle(s => s.UserId == otherUserId2 && s.DisplayName == "User 2");
    }

    [Fact]
    public async Task GetConversationsAsync_WithNoConversations_ReturnsEmptyList()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _repository.GetConversationsAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new List<ConversationSummary>());

        // Act
        var result = await _sut.GetConversationsAsync(userId);

        // Assert
        result.Should().BeEmpty();
    }
}
