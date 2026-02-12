using FluentAssertions;
using HotBox.Core.Entities;
using HotBox.Core.Enums;
using HotBox.Core.Interfaces;
using HotBox.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace HotBox.Infrastructure.Tests.Services;

public class MessageServiceTests
{
    private readonly IMessageRepository _messageRepository;
    private readonly IChannelRepository _channelRepository;
    private readonly ILogger<MessageService> _logger;
    private readonly MessageService _sut;

    public MessageServiceTests()
    {
        _messageRepository = Substitute.For<IMessageRepository>();
        _channelRepository = Substitute.For<IChannelRepository>();
        _logger = Substitute.For<ILogger<MessageService>>();
        _sut = new MessageService(_messageRepository, _channelRepository, _logger);
    }

    [Fact]
    public async Task SendAsync_WithValidData_CreatesMessage()
    {
        // Arrange
        var channelId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var content = "Hello, world!";

        var channel = new Channel
        {
            Id = channelId,
            Name = "test-channel",
            Type = ChannelType.Text,
            SortOrder = 1,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = Guid.NewGuid()
        };

        _channelRepository.GetByIdAsync(channelId, Arg.Any<CancellationToken>()).Returns(channel);
        _messageRepository.CreateAsync(Arg.Any<Message>(), Arg.Any<CancellationToken>())
            .Returns(args => args.ArgAt<Message>(0));

        // Act
        var result = await _sut.SendAsync(channelId, authorId, content);

        // Assert
        result.Should().NotBeNull();
        result.Content.Should().Be(content);
        result.ChannelId.Should().Be(channelId);
        result.AuthorId.Should().Be(authorId);
        result.CreatedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        await _messageRepository.Received(1).CreateAsync(Arg.Any<Message>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_WithEmptyContent_ThrowsArgumentException()
    {
        // Arrange
        var channelId = Guid.NewGuid();
        var authorId = Guid.NewGuid();

        // Act
        var act = () => _sut.SendAsync(channelId, authorId, "");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Message content cannot be empty.*");
    }

    [Fact]
    public async Task SendAsync_WithWhitespaceContent_ThrowsArgumentException()
    {
        // Arrange
        var channelId = Guid.NewGuid();
        var authorId = Guid.NewGuid();

        // Act
        var act = () => _sut.SendAsync(channelId, authorId, "   ");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Message content cannot be empty.*");
    }

    [Fact]
    public async Task SendAsync_WithNonExistentChannel_ThrowsKeyNotFoundException()
    {
        // Arrange
        var channelId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        _channelRepository.GetByIdAsync(channelId, Arg.Any<CancellationToken>()).Returns((Channel?)null);

        // Act
        var act = () => _sut.SendAsync(channelId, authorId, "message");

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage($"Channel {channelId} not found.");
    }

    [Fact]
    public async Task GetByChannelAsync_WithExistingChannel_ReturnsMessages()
    {
        // Arrange
        var channelId = Guid.NewGuid();
        var channel = new Channel
        {
            Id = channelId,
            Name = "test-channel",
            Type = ChannelType.Text,
            SortOrder = 1,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = Guid.NewGuid()
        };

        var messages = new List<Message>
        {
            new() { Id = Guid.NewGuid(), Content = "Message 1", ChannelId = channelId, AuthorId = Guid.NewGuid(), CreatedAtUtc = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Content = "Message 2", ChannelId = channelId, AuthorId = Guid.NewGuid(), CreatedAtUtc = DateTime.UtcNow }
        };

        _channelRepository.GetByIdAsync(channelId, Arg.Any<CancellationToken>()).Returns(channel);
        _messageRepository.GetByChannelAsync(channelId, null, 50, Arg.Any<CancellationToken>()).Returns(messages);

        // Act
        var result = await _sut.GetByChannelAsync(channelId);

        // Assert
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(m => m.ChannelId.Should().Be(channelId));
    }

    [Fact]
    public async Task GetByChannelAsync_WithNonExistentChannel_ThrowsKeyNotFoundException()
    {
        // Arrange
        var channelId = Guid.NewGuid();
        _channelRepository.GetByIdAsync(channelId, Arg.Any<CancellationToken>()).Returns((Channel?)null);

        // Act
        var act = () => _sut.GetByChannelAsync(channelId);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage($"Channel {channelId} not found.");
    }

    [Fact]
    public async Task GetByChannelAsync_WithBeforeParameter_PassesCorrectly()
    {
        // Arrange
        var channelId = Guid.NewGuid();
        var before = DateTime.UtcNow.AddHours(-1);
        var channel = new Channel
        {
            Id = channelId,
            Name = "test-channel",
            Type = ChannelType.Text,
            SortOrder = 1,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = Guid.NewGuid()
        };

        _channelRepository.GetByIdAsync(channelId, Arg.Any<CancellationToken>()).Returns(channel);
        _messageRepository.GetByChannelAsync(channelId, before, 50, Arg.Any<CancellationToken>())
            .Returns(new List<Message>());

        // Act
        await _sut.GetByChannelAsync(channelId, before);

        // Assert
        await _messageRepository.Received(1).GetByChannelAsync(channelId, before, 50, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetByChannelAsync_WithCustomLimit_PassesCorrectly()
    {
        // Arrange
        var channelId = Guid.NewGuid();
        var limit = 100;
        var channel = new Channel
        {
            Id = channelId,
            Name = "test-channel",
            Type = ChannelType.Text,
            SortOrder = 1,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = Guid.NewGuid()
        };

        _channelRepository.GetByIdAsync(channelId, Arg.Any<CancellationToken>()).Returns(channel);
        _messageRepository.GetByChannelAsync(channelId, null, limit, Arg.Any<CancellationToken>())
            .Returns(new List<Message>());

        // Act
        await _sut.GetByChannelAsync(channelId, limit: limit);

        // Assert
        await _messageRepository.Received(1).GetByChannelAsync(channelId, null, limit, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetByIdAsync_WithExistingId_ReturnsMessage()
    {
        // Arrange
        var messageId = Guid.NewGuid();
        var message = new Message
        {
            Id = messageId,
            Content = "Test message",
            ChannelId = Guid.NewGuid(),
            AuthorId = Guid.NewGuid(),
            CreatedAtUtc = DateTime.UtcNow
        };

        _messageRepository.GetByIdAsync(messageId, Arg.Any<CancellationToken>()).Returns(message);

        // Act
        var result = await _sut.GetByIdAsync(messageId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(messageId);
        result.Content.Should().Be("Test message");
    }

    [Fact]
    public async Task GetByIdAsync_WithNonExistentId_ReturnsNull()
    {
        // Arrange
        var messageId = Guid.NewGuid();
        _messageRepository.GetByIdAsync(messageId, Arg.Any<CancellationToken>()).Returns((Message?)null);

        // Act
        var result = await _sut.GetByIdAsync(messageId);

        // Assert
        result.Should().BeNull();
    }
}
