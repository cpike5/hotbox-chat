using Bunit;
using FluentAssertions;
using HotBox.Client.Components;
using HotBox.Client.Models;
using HotBox.Client.State;
using HotBox.Core.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace HotBox.Client.Tests.Components;

/// <summary>
/// Tests for ChannelList component covering skeleton loading, channel rendering, and navigation.
/// </summary>
public class ChannelListTests : TestContext
{
    private readonly ChannelState _channelState;

    public ChannelListTests()
    {
        _channelState = new ChannelState();
        Services.AddSingleton(_channelState);
    }

    [Fact]
    public void Render_WhenLoadingChannels_ShowsSkeletonLoadingState()
    {
        // Arrange
        _channelState.SetLoadingChannels(true);

        // Act
        var cut = RenderComponent<ChannelList>();

        // Assert
        var skeletonElements = cut.FindAll(".skeleton-channel");
        skeletonElements.Should().HaveCount(5, "skeleton shows 5 placeholder channels");

        var skeletonHashes = cut.FindAll(".skeleton-channel-hash");
        skeletonHashes.Should().HaveCount(5, "each skeleton channel has a hash icon");

        var skeletonNames = cut.FindAll(".skeleton-channel-name");
        skeletonNames.Should().HaveCount(5, "each skeleton channel has a name placeholder");
    }

    [Fact]
    public void Render_WhenChannelsLoaded_ShowsChannelTabs()
    {
        // Arrange
        var channels = new List<ChannelResponse>
        {
            new() { Id = Guid.NewGuid(), Name = "general", Type = ChannelType.Text, SortOrder = 1 },
            new() { Id = Guid.NewGuid(), Name = "random", Type = ChannelType.Text, SortOrder = 2 },
            new() { Id = Guid.NewGuid(), Name = "dev", Type = ChannelType.Text, SortOrder = 3 }
        };

        _channelState.SetChannels(channels);
        _channelState.SetLoadingChannels(false);

        // Act
        var cut = RenderComponent<ChannelList>();

        // Assert
        var channelButtons = cut.FindAll(".channel-tab");
        channelButtons.Should().HaveCount(3, "all text channels are rendered");

        channelButtons[0].TextContent.Should().Contain("general");
        channelButtons[1].TextContent.Should().Contain("random");
        channelButtons[2].TextContent.Should().Contain("dev");
    }

    [Fact]
    public void Render_WhenChannelIsActive_AppliesActiveClass()
    {
        // Arrange
        var activeChannelId = Guid.NewGuid();
        var channels = new List<ChannelResponse>
        {
            new() { Id = activeChannelId, Name = "general", Type = ChannelType.Text, SortOrder = 1 },
            new() { Id = Guid.NewGuid(), Name = "random", Type = ChannelType.Text, SortOrder = 2 }
        };

        _channelState.SetChannels(channels);
        _channelState.SetActiveChannel(channels[0]);
        _channelState.SetLoadingChannels(false);

        // Act
        var cut = RenderComponent<ChannelList>();

        // Assert
        var activeButton = cut.Find(".channel-tab.active");
        activeButton.TextContent.Should().Contain("general");

        var allButtons = cut.FindAll(".channel-tab");
        var inactiveButtons = allButtons.Where(b => !b.ClassList.Contains("active")).ToList();
        inactiveButtons.Should().HaveCount(1, "only one channel should be inactive");
    }

    [Fact]
    public void Render_WithVoiceChannels_OnlyShowsTextChannels()
    {
        // Arrange
        var channels = new List<ChannelResponse>
        {
            new() { Id = Guid.NewGuid(), Name = "general", Type = ChannelType.Text, SortOrder = 1 },
            new() { Id = Guid.NewGuid(), Name = "voice-lounge", Type = ChannelType.Voice, SortOrder = 2 },
            new() { Id = Guid.NewGuid(), Name = "random", Type = ChannelType.Text, SortOrder = 3 }
        };

        _channelState.SetChannels(channels);
        _channelState.SetLoadingChannels(false);

        // Act
        var cut = RenderComponent<ChannelList>();

        // Assert
        var channelButtons = cut.FindAll(".channel-tab");
        channelButtons.Should().HaveCount(2, "only text channels are shown");

        var channelTexts = channelButtons.Select(b => b.TextContent.Trim()).ToList();
        channelTexts.Should().NotContain(t => t.Contains("voice-lounge"));
    }

    [Fact]
    public void ClickChannel_NavigatesToChannelRoute()
    {
        // Arrange
        var channelId = Guid.NewGuid();
        var channels = new List<ChannelResponse>
        {
            new() { Id = channelId, Name = "general", Type = ChannelType.Text, SortOrder = 1 }
        };

        _channelState.SetChannels(channels);
        _channelState.SetLoadingChannels(false);

        var cut = RenderComponent<ChannelList>();
        var navManager = Services.GetRequiredService<NavigationManager>();

        // Act
        var button = cut.Find(".channel-tab");
        button.Click();

        // Assert
        navManager.Uri.Should().EndWith($"/channels/{channelId}");
    }

    [Fact]
    public void ClickChannel_SetsActiveChannel()
    {
        // Arrange
        var channelId = Guid.NewGuid();
        var channels = new List<ChannelResponse>
        {
            new() { Id = channelId, Name = "general", Type = ChannelType.Text, SortOrder = 1 }
        };

        _channelState.SetChannels(channels);
        _channelState.SetLoadingChannels(false);

        var cut = RenderComponent<ChannelList>();

        // Act
        var button = cut.Find(".channel-tab");
        button.Click();

        // Assert
        _channelState.ActiveChannel.Should().NotBeNull();
        _channelState.ActiveChannel!.Id.Should().Be(channelId);
        _channelState.ActiveChannel.Name.Should().Be("general");
    }

    [Fact]
    public void Render_WhenNoChannels_ShowsEmptyState()
    {
        // Arrange
        _channelState.SetChannels(new List<ChannelResponse>());
        _channelState.SetLoadingChannels(false);

        // Act
        var cut = RenderComponent<ChannelList>();

        // Assert
        var channelButtons = cut.FindAll(".channel-tab");
        channelButtons.Should().BeEmpty("no channels are available");
    }

    [Fact]
    public void StateChange_TriggersRerender()
    {
        // Arrange
        _channelState.SetLoadingChannels(true);
        var cut = RenderComponent<ChannelList>();

        var initialSkeletons = cut.FindAll(".skeleton-channel");
        initialSkeletons.Should().HaveCount(5);

        // Act - change state on the renderer's dispatcher
        var channels = new List<ChannelResponse>
        {
            new() { Id = Guid.NewGuid(), Name = "general", Type = ChannelType.Text, SortOrder = 1 }
        };
        cut.InvokeAsync(() =>
        {
            _channelState.SetChannels(channels);
            _channelState.SetLoadingChannels(false);
        });

        // Assert - component should rerender
        var skeletonsAfter = cut.FindAll(".skeleton-channel");
        skeletonsAfter.Should().BeEmpty("loading state is complete");

        var channelButtons = cut.FindAll(".channel-tab");
        channelButtons.Should().HaveCount(1, "channel tabs should now be visible");
    }
}
