using Bunit;
using FluentAssertions;
using HotBox.Client.Components;
using HotBox.Client.Models;
using HotBox.Client.Services;
using HotBox.Client.State;
using HotBox.Core.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using NSubstitute;
using System.Net;

namespace HotBox.Client.Tests.Components;

/// <summary>
/// Tests for SearchOverlay component covering visibility, search input, and results rendering.
/// </summary>
public class SearchOverlayTests : TestContext
{
    private readonly SearchState _searchState;
    private readonly ChannelState _channelState;
    private readonly ApiClient _apiClient;

    public SearchOverlayTests()
    {
        _searchState = new SearchState();
        _channelState = new ChannelState();

        var authState = new AuthState();
        var httpClient = new HttpClient(new MockHttpMessageHandler())
        {
            BaseAddress = new Uri("http://localhost")
        };
        var logger = new NullLogger<ApiClient>();
        _apiClient = new ApiClient(httpClient, authState, logger);

        Services.AddSingleton(_searchState);
        Services.AddSingleton(_channelState);
        Services.AddSingleton(_apiClient);

        // Mock JSRuntime for searchInterop calls
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Render_WhenClosed_DoesNotShowOverlay()
    {
        // Arrange
        _searchState.Close();

        // Act
        var cut = RenderComponent<SearchOverlay>();

        // Assert
        var overlays = cut.FindAll(".search-overlay-backdrop");
        overlays.Should().BeEmpty("overlay should be hidden when closed");
    }

    [Fact]
    public void Render_WhenOpen_ShowsOverlay()
    {
        // Arrange
        _searchState.Open();

        // Act
        var cut = RenderComponent<SearchOverlay>();

        // Assert
        var backdrop = cut.Find(".search-overlay-backdrop");
        backdrop.Should().NotBeNull();

        var overlay = cut.Find(".search-overlay");
        overlay.Should().NotBeNull();
    }

    [Fact]
    public void Render_ShowsSearchInput()
    {
        // Arrange
        _searchState.Open();

        // Act
        var cut = RenderComponent<SearchOverlay>();

        // Assert
        var input = cut.Find("input.search-input");
        input.Should().NotBeNull();
        input.GetAttribute("placeholder").Should().Contain("Search messages");
    }

    [Fact]
    public void Render_ShowsSearchInputWrapper()
    {
        // Arrange
        _searchState.Open();

        // Act
        var cut = RenderComponent<SearchOverlay>();

        // Assert
        var wrapper = cut.Find(".search-input-wrapper");
        wrapper.Should().NotBeNull();
        wrapper.QuerySelector("svg").Should().NotBeNull("search wrapper should contain an SVG icon");
    }

    [Fact]
    public void Render_ShowsEscapeHint()
    {
        // Arrange
        _searchState.Open();

        // Act
        var cut = RenderComponent<SearchOverlay>();

        // Assert
        var hint = cut.Find(".search-shortcut-hint");
        hint.Should().NotBeNull();
        hint.QuerySelector("kbd").Should().NotBeNull();
        hint.TextContent.Should().Contain("ESC");
    }

    [Fact]
    public void Render_ShowsChannelFilter()
    {
        // Arrange
        _searchState.Open();
        var channels = new List<ChannelResponse>
        {
            new() { Id = Guid.NewGuid(), Name = "general", Type = ChannelType.Text, SortOrder = 1 },
            new() { Id = Guid.NewGuid(), Name = "random", Type = ChannelType.Text, SortOrder = 2 }
        };
        _channelState.SetChannels(channels);

        // Act
        var cut = RenderComponent<SearchOverlay>();

        // Assert
        var select = cut.Find("select.search-channel-filter");
        select.Should().NotBeNull();

        var options = select.QuerySelectorAll("option");
        options.Should().HaveCountGreaterOrEqualTo(3, "All channels option + 2 channels");

        var allChannelsOption = options[0];
        allChannelsOption.TextContent.Should().Be("All channels");
    }

    [Fact]
    public void Render_WhenQueryEmpty_ShowsEmptyState()
    {
        // Arrange
        _searchState.Open();
        _searchState.SetQuery("");

        // Act
        var cut = RenderComponent<SearchOverlay>();

        // Assert
        var emptyState = cut.Find(".search-empty-state");
        emptyState.Should().NotBeNull();
        emptyState.TextContent.Should().Contain("Type to search messages");
    }

    [Fact]
    public void Render_WhenSearching_ShowsSpinner()
    {
        // Arrange
        _searchState.Open();
        _searchState.SetQuery("test");
        _searchState.SetSearching(true);

        // Act
        var cut = RenderComponent<SearchOverlay>();

        // Assert
        var spinner = cut.Find(".search-spinner .spinner");
        spinner.Should().NotBeNull();
    }

    [Fact]
    public void Render_WithResults_DisplaysResultItems()
    {
        // Arrange
        _searchState.Open();
        _searchState.SetQuery("test");
        var results = new List<SearchResultItemModel>
        {
            new()
            {
                MessageId = Guid.NewGuid(),
                ChannelId = Guid.NewGuid(),
                ChannelName = "general",
                Snippet = "This is a <mark>test</mark> message",
                AuthorDisplayName = "Alice",
                CreatedAtUtc = DateTime.UtcNow.AddHours(-1)
            },
            new()
            {
                MessageId = Guid.NewGuid(),
                ChannelId = Guid.NewGuid(),
                ChannelName = "random",
                Snippet = "Another <mark>test</mark>",
                AuthorDisplayName = "Bob",
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-30)
            }
        };
        _searchState.SetResults(results, 2, null);
        _searchState.SetSearching(false);

        // Act
        var cut = RenderComponent<SearchOverlay>();

        // Assert
        var resultItems = cut.FindAll(".search-result-item");
        resultItems.Should().HaveCount(2);

        var firstResult = resultItems[0];
        firstResult.TextContent.Should().Contain("Alice");
        firstResult.TextContent.Should().Contain("general");
    }

    [Fact]
    public void Render_WithResults_ShowsTotalEstimate()
    {
        // Arrange
        _searchState.Open();
        _searchState.SetQuery("test");
        var results = new List<SearchResultItemModel>
        {
            new()
            {
                MessageId = Guid.NewGuid(),
                ChannelId = Guid.NewGuid(),
                ChannelName = "general",
                Snippet = "<mark>Test</mark>",
                AuthorDisplayName = "Alice",
                CreatedAtUtc = DateTime.UtcNow
            }
        };
        _searchState.SetResults(results, 42, null);

        // Act
        var cut = RenderComponent<SearchOverlay>();

        // Assert
        var resultCount = cut.Find(".search-result-count");
        resultCount.Should().NotBeNull();
        resultCount.TextContent.Should().Contain("42 results");
    }

    [Fact]
    public void StateChange_TriggersRerender()
    {
        // Arrange
        _searchState.Close();
        var cut = RenderComponent<SearchOverlay>();

        var overlaysBefore = cut.FindAll(".search-overlay-backdrop");
        overlaysBefore.Should().BeEmpty();

        // Act - open search
        _searchState.Open();

        // Assert - overlay should now be visible
        var overlaysAfter = cut.FindAll(".search-overlay-backdrop");
        overlaysAfter.Should().HaveCount(1, "overlay should be visible after opening");
    }

    // Mock HttpMessageHandler for ApiClient
    private class MockHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]")
            });
        }
    }
}
