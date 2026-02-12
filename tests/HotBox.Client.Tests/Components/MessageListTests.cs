using Bunit;
using FluentAssertions;
using HotBox.Client.Components;
using HotBox.Client.Models;
using HotBox.Client.State;
using HotBox.Core.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace HotBox.Client.Tests.Components;

/// <summary>
/// Tests for MessageList component covering loading states, message rendering, and channel welcome.
/// </summary>
public class MessageListTests : TestContext
{
    private readonly ChannelState _channelState;

    public MessageListTests()
    {
        _channelState = new ChannelState();
        Services.AddSingleton(_channelState);
    }

    [Fact]
    public void Render_WhenLoadingMessages_ShowsSkeletonLoadingState()
    {
        // Arrange
        _channelState.SetLoadingMessages(true);

        // Act
        var cut = RenderComponent<MessageList>();

        // Assert
        var skeletonMessages = cut.FindAll(".skeleton-message");
        skeletonMessages.Should().HaveCount(6, "skeleton shows 6 placeholder messages");

        var skeletonAvatars = cut.FindAll(".skeleton-avatar");
        skeletonAvatars.Should().HaveCount(6, "each message has an avatar placeholder");
    }

    [Fact]
    public void Render_WhenActiveChannelSet_ShowsChannelWelcome()
    {
        // Arrange
        var channel = new ChannelResponse
        {
            Id = Guid.NewGuid(),
            Name = "general",
            Type = ChannelType.Text,
            Topic = "Welcome to the general channel!",
            SortOrder = 1
        };
        _channelState.SetActiveChannel(channel);
        _channelState.SetLoadingMessages(false);

        // Act
        var cut = RenderComponent<MessageList>();

        // Assert
        var welcome = cut.Find(".channel-welcome");
        welcome.Should().NotBeNull();

        var heading = cut.Find(".channel-welcome h2");
        heading.TextContent.Should().Be("# general");

        var description = cut.Find(".channel-welcome p");
        description.TextContent.Should().Contain("Welcome to the general channel!");
    }

    [Fact]
    public void Render_WhenChannelHasNoTopic_ShowsWelcomeWithoutTopic()
    {
        // Arrange
        var channel = new ChannelResponse
        {
            Id = Guid.NewGuid(),
            Name = "random",
            Type = ChannelType.Text,
            Topic = null,
            SortOrder = 1
        };
        _channelState.SetActiveChannel(channel);
        _channelState.SetLoadingMessages(false);

        // Act
        var cut = RenderComponent<MessageList>();

        // Assert
        var description = cut.Find(".channel-welcome p");
        description.TextContent.Should().Contain("This is the start of the #random channel.");
        description.TextContent.Should().NotContain("null");
    }

    [Fact]
    public void Render_WithMessages_DisplaysMessageContent()
    {
        // Arrange
        var channel = new ChannelResponse
        {
            Id = Guid.NewGuid(),
            Name = "general",
            Type = ChannelType.Text,
            SortOrder = 1
        };
        _channelState.SetActiveChannel(channel);

        var messages = new List<MessageResponse>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Content = "Hello world!",
                AuthorDisplayName = "Alice",
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-5)
            },
            new()
            {
                Id = Guid.NewGuid(),
                Content = "How are you?",
                AuthorDisplayName = "Bob",
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-3)
            }
        };
        _channelState.SetMessages(messages);
        _channelState.SetLoadingMessages(false);

        // Act
        var cut = RenderComponent<MessageList>();

        // Assert
        var messageGroups = cut.FindAll(".message-group");
        messageGroups.Should().HaveCountGreaterOrEqualTo(2);

        var messageBodies = cut.FindAll(".msg-body");
        messageBodies.Should().HaveCount(2);
        messageBodies[0].TextContent.Should().Be("Hello world!");
        messageBodies[1].TextContent.Should().Be("How are you?");
    }

    [Fact]
    public void Render_WithNewAuthor_ShowsAvatarAndAuthorName()
    {
        // Arrange
        var channel = new ChannelResponse
        {
            Id = Guid.NewGuid(),
            Name = "general",
            Type = ChannelType.Text,
            SortOrder = 1
        };
        _channelState.SetActiveChannel(channel);

        var messages = new List<MessageResponse>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Content = "First message",
                AuthorDisplayName = "Alice",
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-10)
            }
        };
        _channelState.SetMessages(messages);
        _channelState.SetLoadingMessages(false);

        // Act
        var cut = RenderComponent<MessageList>();

        // Assert
        var newAuthorGroups = cut.FindAll(".message-group.new-author");
        newAuthorGroups.Should().HaveCount(1);

        var avatar = cut.Find(".msg-avatar");
        avatar.Should().NotBeNull();

        var author = cut.Find(".msg-author");
        author.TextContent.Should().Be("Alice");
    }

    [Fact]
    public void Render_WithSameAuthorConsecutiveMessages_HidesSecondAvatar()
    {
        // Arrange
        var channel = new ChannelResponse
        {
            Id = Guid.NewGuid(),
            Name = "general",
            Type = ChannelType.Text,
            SortOrder = 1
        };
        _channelState.SetActiveChannel(channel);

        var baseTime = DateTime.UtcNow.AddMinutes(-10);
        var messages = new List<MessageResponse>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Content = "First message",
                AuthorDisplayName = "Alice",
                CreatedAtUtc = baseTime
            },
            new()
            {
                Id = Guid.NewGuid(),
                Content = "Second message from same author",
                AuthorDisplayName = "Alice",
                CreatedAtUtc = baseTime.AddSeconds(30)
            }
        };
        _channelState.SetMessages(messages);
        _channelState.SetLoadingMessages(false);

        // Act
        var cut = RenderComponent<MessageList>();

        // Assert
        var newAuthorGroups = cut.FindAll(".message-group.new-author");
        newAuthorGroups.Should().HaveCount(1, "only the first message from Alice should have new-author class");

        var avatars = cut.FindAll(".msg-avatar");
        avatars.Should().HaveCount(1, "only the first message should show an avatar");
    }

    [Fact]
    public void Render_WhenLoadingOlderMessages_ShowsPaginationSpinner()
    {
        // Arrange
        var channel = new ChannelResponse
        {
            Id = Guid.NewGuid(),
            Name = "general",
            Type = ChannelType.Text,
            SortOrder = 1
        };
        _channelState.SetActiveChannel(channel);
        _channelState.SetLoadingMessages(false);
        _channelState.SetLoadingOlderMessages(true);

        // Act
        var cut = RenderComponent<MessageList>();

        // Assert
        var paginationLoader = cut.Find(".pagination-loader");
        paginationLoader.Should().NotBeNull();

        var spinner = cut.Find(".pagination-loader .spinner");
        spinner.Should().NotBeNull();
    }

    [Fact]
    public void Render_WhenNotLoadingOlderMessages_HidesPaginationSpinner()
    {
        // Arrange
        var channel = new ChannelResponse
        {
            Id = Guid.NewGuid(),
            Name = "general",
            Type = ChannelType.Text,
            SortOrder = 1
        };
        _channelState.SetActiveChannel(channel);
        _channelState.SetLoadingMessages(false);
        _channelState.SetLoadingOlderMessages(false);

        // Act
        var cut = RenderComponent<MessageList>();

        // Assert
        var paginationLoaders = cut.FindAll(".pagination-loader");
        paginationLoaders.Should().BeEmpty();
    }

    [Fact]
    public void StateChange_TriggersRerender()
    {
        // Arrange
        var channel = new ChannelResponse
        {
            Id = Guid.NewGuid(),
            Name = "general",
            Type = ChannelType.Text,
            SortOrder = 1
        };
        _channelState.SetActiveChannel(channel);
        _channelState.SetLoadingMessages(true);

        var cut = RenderComponent<MessageList>();

        var initialSkeletons = cut.FindAll(".skeleton-message");
        initialSkeletons.Should().HaveCount(6);

        // Act - change state on the renderer's dispatcher
        var messages = new List<MessageResponse>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Content = "Test message",
                AuthorDisplayName = "Alice",
                CreatedAtUtc = DateTime.UtcNow
            }
        };
        cut.InvokeAsync(() =>
        {
            _channelState.SetMessages(messages);
            _channelState.SetLoadingMessages(false);
        });

        // Assert - component should rerender
        var skeletonsAfter = cut.FindAll(".skeleton-message");
        skeletonsAfter.Should().BeEmpty("loading state is complete");

        var messageBodies = cut.FindAll(".msg-body");
        messageBodies.Should().HaveCount(1);
        messageBodies[0].TextContent.Should().Be("Test message");
    }
}
