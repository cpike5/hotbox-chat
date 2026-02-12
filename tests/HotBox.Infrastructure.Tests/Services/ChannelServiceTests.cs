using FluentAssertions;
using HotBox.Core.Entities;
using HotBox.Core.Enums;
using HotBox.Core.Interfaces;
using HotBox.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace HotBox.Infrastructure.Tests.Services;

public class ChannelServiceTests
{
    private readonly IChannelRepository _channelRepository;
    private readonly UserManager<AppUser> _userManager;
    private readonly ILogger<ChannelService> _logger;
    private readonly ChannelService _sut;

    public ChannelServiceTests()
    {
        _channelRepository = Substitute.For<IChannelRepository>();
        _userManager = Substitute.For<UserManager<AppUser>>(
            Substitute.For<IUserStore<AppUser>>(),
            null, null, null, null, null, null, null, null);
        _logger = Substitute.For<ILogger<ChannelService>>();
        _sut = new ChannelService(_channelRepository, _userManager, _logger);
    }

    [Fact]
    public async Task CreateAsync_WithValidData_CreatesChannel()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var channelName = "test-channel";
        var topic = "Test Topic";
        var channelType = ChannelType.Text;

        var user = new AppUser { Id = userId, DisplayName = "Test User" };
        _userManager.FindByIdAsync(userId.ToString()).Returns(user);
        _userManager.GetRolesAsync(user).Returns(new List<string> { "Admin" });
        _channelRepository.ExistsByNameAsync(channelName, Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(false);
        _channelRepository.GetMaxSortOrderAsync(Arg.Any<CancellationToken>()).Returns(5);
        _channelRepository.CreateAsync(Arg.Any<Channel>(), Arg.Any<CancellationToken>())
            .Returns(args => args.ArgAt<Channel>(0));

        // Act
        var result = await _sut.CreateAsync(userId, channelName, topic, channelType);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be(channelName);
        result.Topic.Should().Be(topic);
        result.Type.Should().Be(channelType);
        result.SortOrder.Should().Be(6);
        result.CreatedByUserId.Should().Be(userId);
        await _channelRepository.Received(1).CreateAsync(Arg.Any<Channel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_WithEmptyName_ThrowsArgumentException()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var act = () => _sut.CreateAsync(userId, "", null, ChannelType.Text);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Channel name cannot be empty.*");
    }

    [Fact]
    public async Task CreateAsync_WithNonAdminUser_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new AppUser { Id = userId, DisplayName = "Member" };
        _userManager.FindByIdAsync(userId.ToString()).Returns(user);
        _userManager.GetRolesAsync(user).Returns(new List<string> { "Member" });

        // Act
        var act = () => _sut.CreateAsync(userId, "test", null, ChannelType.Text);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Only Admin or Moderator roles can perform this operation.");
    }

    [Fact]
    public async Task CreateAsync_WithModeratorUser_CreatesChannel()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new AppUser { Id = userId, DisplayName = "Mod" };
        _userManager.FindByIdAsync(userId.ToString()).Returns(user);
        _userManager.GetRolesAsync(user).Returns(new List<string> { "Moderator" });
        _channelRepository.ExistsByNameAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(false);
        _channelRepository.GetMaxSortOrderAsync(Arg.Any<CancellationToken>()).Returns(0);
        _channelRepository.CreateAsync(Arg.Any<Channel>(), Arg.Any<CancellationToken>())
            .Returns(args => args.ArgAt<Channel>(0));

        // Act
        var result = await _sut.CreateAsync(userId, "test", null, ChannelType.Text);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateAsync_WithDuplicateName_ThrowsInvalidOperationException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new AppUser { Id = userId, DisplayName = "Admin" };
        _userManager.FindByIdAsync(userId.ToString()).Returns(user);
        _userManager.GetRolesAsync(user).Returns(new List<string> { "Admin" });
        _channelRepository.ExistsByNameAsync("existing", Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        var act = () => _sut.CreateAsync(userId, "existing", null, ChannelType.Text);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("A channel with the name 'existing' already exists.");
    }

    [Fact]
    public async Task UpdateAsync_WithValidData_UpdatesChannel()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var newName = "updated-name";
        var newTopic = "Updated Topic";

        var user = new AppUser { Id = userId, DisplayName = "Admin" };
        var channel = new Channel
        {
            Id = channelId,
            Name = "old-name",
            Topic = "Old Topic",
            Type = ChannelType.Text,
            SortOrder = 1,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = userId
        };

        _userManager.FindByIdAsync(userId.ToString()).Returns(user);
        _userManager.GetRolesAsync(user).Returns(new List<string> { "Admin" });
        _channelRepository.GetByIdAsync(channelId, Arg.Any<CancellationToken>()).Returns(channel);
        _channelRepository.ExistsByNameAsync(newName, channelId, Arg.Any<CancellationToken>()).Returns(false);

        // Act
        await _sut.UpdateAsync(userId, channelId, newName, newTopic);

        // Assert
        channel.Name.Should().Be(newName);
        channel.Topic.Should().Be(newTopic);
        await _channelRepository.Received(1).UpdateAsync(channel, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_WithEmptyName_ThrowsArgumentException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var channelId = Guid.NewGuid();

        // Act
        var act = () => _sut.UpdateAsync(userId, channelId, "", null);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Channel name cannot be empty.*");
    }

    [Fact]
    public async Task UpdateAsync_WithNonExistentChannel_ThrowsKeyNotFoundException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var user = new AppUser { Id = userId, DisplayName = "Admin" };

        _userManager.FindByIdAsync(userId.ToString()).Returns(user);
        _userManager.GetRolesAsync(user).Returns(new List<string> { "Admin" });
        _channelRepository.GetByIdAsync(channelId, Arg.Any<CancellationToken>()).Returns((Channel?)null);

        // Act
        var act = () => _sut.UpdateAsync(userId, channelId, "new-name", null);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage($"Channel {channelId} not found.");
    }

    [Fact]
    public async Task DeleteAsync_WithAdminUser_DeletesChannel()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var user = new AppUser { Id = userId, DisplayName = "Admin" };
        var channel = new Channel
        {
            Id = channelId,
            Name = "to-delete",
            Type = ChannelType.Text,
            SortOrder = 1,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = userId
        };

        _userManager.FindByIdAsync(userId.ToString()).Returns(user);
        _userManager.GetRolesAsync(user).Returns(new List<string> { "Admin" });
        _channelRepository.GetByIdAsync(channelId, Arg.Any<CancellationToken>()).Returns(channel);

        // Act
        await _sut.DeleteAsync(userId, channelId);

        // Assert
        await _channelRepository.Received(1).DeleteAsync(channelId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_WithModeratorUser_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var user = new AppUser { Id = userId, DisplayName = "Mod" };

        _userManager.FindByIdAsync(userId.ToString()).Returns(user);
        _userManager.GetRolesAsync(user).Returns(new List<string> { "Moderator" });

        // Act
        var act = () => _sut.DeleteAsync(userId, channelId);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Only Admin role can perform this operation.");
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllChannels()
    {
        // Arrange
        var channels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "channel1", Type = ChannelType.Text, SortOrder = 1, CreatedAtUtc = DateTime.UtcNow, CreatedByUserId = Guid.NewGuid() },
            new() { Id = Guid.NewGuid(), Name = "channel2", Type = ChannelType.Voice, SortOrder = 2, CreatedAtUtc = DateTime.UtcNow, CreatedByUserId = Guid.NewGuid() }
        };
        _channelRepository.GetAllAsync(Arg.Any<CancellationToken>()).Returns(channels);

        // Act
        var result = await _sut.GetAllAsync();

        // Assert
        result.Should().HaveCount(2);
        result.Should().BeEquivalentTo(channels);
    }

    [Fact]
    public async Task GetByIdAsync_WithExistingId_ReturnsChannel()
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
        _channelRepository.GetByIdAsync(channelId, Arg.Any<CancellationToken>()).Returns(channel);

        // Act
        var result = await _sut.GetByIdAsync(channelId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(channelId);
        result.Name.Should().Be("test-channel");
    }

    [Fact]
    public async Task GetByIdAsync_WithNonExistentId_ReturnsNull()
    {
        // Arrange
        var channelId = Guid.NewGuid();
        _channelRepository.GetByIdAsync(channelId, Arg.Any<CancellationToken>()).Returns((Channel?)null);

        // Act
        var result = await _sut.GetByIdAsync(channelId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByTypeAsync_ReturnsChannelsOfType()
    {
        // Arrange
        var textChannels = new List<Channel>
        {
            new() { Id = Guid.NewGuid(), Name = "text1", Type = ChannelType.Text, SortOrder = 1, CreatedAtUtc = DateTime.UtcNow, CreatedByUserId = Guid.NewGuid() },
            new() { Id = Guid.NewGuid(), Name = "text2", Type = ChannelType.Text, SortOrder = 2, CreatedAtUtc = DateTime.UtcNow, CreatedByUserId = Guid.NewGuid() }
        };
        _channelRepository.GetByTypeAsync(ChannelType.Text, Arg.Any<CancellationToken>()).Returns(textChannels);

        // Act
        var result = await _sut.GetByTypeAsync(ChannelType.Text);

        // Assert
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(c => c.Type.Should().Be(ChannelType.Text));
    }
}
