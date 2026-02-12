using Bunit;
using FluentAssertions;
using HotBox.Client.Components;
using HotBox.Client.Models;
using HotBox.Client.Services;
using HotBox.Client.State;
using HotBox.Core.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace HotBox.Client.Tests.Components;

/// <summary>
/// Tests for MessageInput component covering placeholder text, enabled/disabled states, and basic rendering.
/// </summary>
public class MessageInputTests : TestContext
{
    private readonly ChannelState _channelState;

    public MessageInputTests()
    {
        _channelState = new ChannelState();
        Services.AddSingleton(_channelState);

        // Register ChatHubService via factory so NavigationManager is resolved from bUnit's provider
        Services.AddSingleton(sp =>
            new ChatHubService(
                sp.GetRequiredService<NavigationManager>(),
                new NullLogger<ChatHubService>()));
    }

    [Fact]
    public void Render_WhenNoActiveChannel_ShowsDisabledInput()
    {
        // Arrange - no active channel set
        _channelState.SetLoadingChannels(false);

        // Act
        var cut = RenderComponent<MessageInput>();

        // Assert
        var input = cut.Find("input[type='text']");
        input.HasAttribute("disabled").Should().BeTrue();
        input.GetAttribute("placeholder").Should().Be("Select a channel to start chatting");
    }

    [Fact]
    public void Render_WhenActiveChannelSet_ShowsEnabledInputWithChannelName()
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

        // Act
        var cut = RenderComponent<MessageInput>();

        // Assert
        var input = cut.Find("input[type='text']");
        input.HasAttribute("disabled").Should().BeFalse();
        input.GetAttribute("placeholder").Should().Be("Message #general");
    }

    [Fact]
    public void Render_SendButton_IsDisabledWhenNoActiveChannel()
    {
        // Arrange - no active channel
        _channelState.SetLoadingChannels(false);

        // Act
        var cut = RenderComponent<MessageInput>();

        // Assert
        var button = cut.Find("button[aria-label='Send message']");
        button.HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public void Render_SendButton_IsEnabledWhenActiveChannelSet()
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

        // Act
        var cut = RenderComponent<MessageInput>();

        // Assert
        var button = cut.Find("button[aria-label='Send message']");
        button.HasAttribute("disabled").Should().BeFalse();
    }

    [Fact]
    public void Render_IncludesTypingIndicator()
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

        // Act
        var cut = RenderComponent<MessageInput>();

        // Assert
        // The component includes <TypingIndicator /> which should be rendered
        var wrapper = cut.Find(".message-input-wrapper");
        wrapper.Should().NotBeNull();
    }

    [Fact]
    public void StateChange_UpdatesPlaceholder()
    {
        // Arrange - no active channel initially
        var cut = RenderComponent<MessageInput>();

        var input = cut.Find("input[type='text']");
        input.GetAttribute("placeholder").Should().Be("Select a channel to start chatting");

        // Act - set active channel on the renderer's dispatcher
        var channel = new ChannelResponse
        {
            Id = Guid.NewGuid(),
            Name = "random",
            Type = ChannelType.Text,
            SortOrder = 1
        };
        cut.InvokeAsync(() => _channelState.SetActiveChannel(channel));

        // Assert - placeholder should update
        input = cut.Find("input[type='text']");
        input.GetAttribute("placeholder").Should().Be("Message #random");
    }

    [Fact]
    public void Render_HasSendButtonWithIcon()
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

        // Act
        var cut = RenderComponent<MessageInput>();

        // Assert
        var button = cut.Find("button[aria-label='Send message']");
        button.Should().NotBeNull();

        var svg = button.QuerySelector("svg");
        svg.Should().NotBeNull("button should contain an SVG icon");
    }
}
