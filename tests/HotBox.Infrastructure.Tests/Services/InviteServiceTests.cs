using FluentAssertions;
using HotBox.Core.Entities;
using HotBox.Infrastructure.Data;
using HotBox.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace HotBox.Infrastructure.Tests.Services;

public class InviteServiceTests
{
    private readonly HotBoxDbContext _context;
    private readonly ILogger<InviteService> _logger;
    private readonly InviteService _sut;

    public InviteServiceTests()
    {
        var options = new DbContextOptionsBuilder<HotBoxDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;

        _context = new HotBoxDbContext(options);
        _logger = Substitute.For<ILogger<InviteService>>();
        _sut = new InviteService(_context, _logger);
    }

    [Fact]
    public async Task GenerateAsync_CreatesInvite()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var result = await _sut.GenerateAsync(userId);

        // Assert
        result.Should().NotBeNull();
        result.Code.Should().NotBeNullOrEmpty();
        result.Code.Length.Should().Be(8); // 6 bytes Base64URL -> 8 chars
        result.CreatedByUserId.Should().Be(userId);
        result.UseCount.Should().Be(0);
        result.IsRevoked.Should().BeFalse();
        result.ExpiresAtUtc.Should().BeNull();
        result.MaxUses.Should().BeNull();
    }

    [Fact]
    public async Task GenerateAsync_WithExpiration_SetsExpiresAtUtc()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var expiresAt = DateTime.UtcNow.AddDays(7);

        // Act
        var result = await _sut.GenerateAsync(userId, expiresAt);

        // Assert
        result.ExpiresAtUtc.Should().BeCloseTo(expiresAt, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task GenerateAsync_WithMaxUses_SetsMaxUses()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var maxUses = 10;

        // Act
        var result = await _sut.GenerateAsync(userId, maxUses: maxUses);

        // Assert
        result.MaxUses.Should().Be(maxUses);
    }

    [Fact]
    public async Task ValidateAndConsumeAsync_WithValidCode_ConsumesInvite()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var invite = await _sut.GenerateAsync(userId);

        // Act
        var result = await _sut.ValidateAndConsumeAsync(invite.Code);

        // Assert
        result.Should().NotBeNull();
        result!.UseCount.Should().Be(1);
        result.Code.Should().Be(invite.Code);

        // Verify it's actually saved
        var savedInvite = await _context.Invites.FirstOrDefaultAsync(i => i.Code == invite.Code);
        savedInvite!.UseCount.Should().Be(1);
    }

    [Fact]
    public async Task ValidateAndConsumeAsync_WithNonExistentCode_ReturnsNull()
    {
        // Act
        var result = await _sut.ValidateAndConsumeAsync("invalid");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ValidateAndConsumeAsync_WithRevokedInvite_ReturnsNull()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var invite = await _sut.GenerateAsync(userId);
        await _sut.RevokeAsync(invite.Code);

        // Act
        var result = await _sut.ValidateAndConsumeAsync(invite.Code);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ValidateAndConsumeAsync_WithExpiredInvite_ReturnsNull()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var expiresAt = DateTime.UtcNow.AddSeconds(-1); // Expired 1 second ago
        var invite = await _sut.GenerateAsync(userId, expiresAt);

        // Wait a moment to ensure expiration
        await Task.Delay(100);

        // Act
        var result = await _sut.ValidateAndConsumeAsync(invite.Code);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ValidateAndConsumeAsync_WithMaxUsesReached_ReturnsNull()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var invite = await _sut.GenerateAsync(userId, maxUses: 1);

        // Consume once
        await _sut.ValidateAndConsumeAsync(invite.Code);

        // Act - try to consume again
        var result = await _sut.ValidateAndConsumeAsync(invite.Code);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ValidateAndConsumeAsync_IncrementsUseCount()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var invite = await _sut.GenerateAsync(userId, maxUses: 5);

        // Act - consume multiple times
        await _sut.ValidateAndConsumeAsync(invite.Code);
        await _sut.ValidateAndConsumeAsync(invite.Code);
        var result = await _sut.ValidateAndConsumeAsync(invite.Code);

        // Assert
        result!.UseCount.Should().Be(3);
    }

    [Fact]
    public async Task RevokeAsync_WithValidCode_RevokesInvite()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var invite = await _sut.GenerateAsync(userId);

        // Act
        var result = await _sut.RevokeAsync(invite.Code);

        // Assert
        result.Should().BeTrue();

        var savedInvite = await _context.Invites.FirstOrDefaultAsync(i => i.Code == invite.Code);
        savedInvite!.IsRevoked.Should().BeTrue();
    }

    [Fact]
    public async Task RevokeAsync_WithNonExistentCode_ReturnsFalse()
    {
        // Act
        var result = await _sut.RevokeAsync("invalid");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task RevokeAsync_WithAlreadyRevokedCode_ReturnsTrue()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var invite = await _sut.GenerateAsync(userId);
        await _sut.RevokeAsync(invite.Code);

        // Act - revoke again
        var result = await _sut.RevokeAsync(invite.Code);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllInvites()
    {
        // Arrange
        var userId1 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();
        await _sut.GenerateAsync(userId1);
        await _sut.GenerateAsync(userId2);

        // Act
        var result = await _sut.GetAllAsync();

        // Assert
        result.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsInvitesOrderedByCreatedAtUtcDescending()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var invite1 = await _sut.GenerateAsync(userId);
        await Task.Delay(10); // Small delay to ensure different timestamps
        var invite2 = await _sut.GenerateAsync(userId);

        // Act
        var result = await _sut.GetAllAsync();

        // Assert
        var orderedInvites = result.Where(i => i.Id == invite1.Id || i.Id == invite2.Id).ToList();
        orderedInvites.Should().HaveCount(2);
        orderedInvites.First().Id.Should().Be(invite2.Id); // Most recent first
        orderedInvites.Last().Id.Should().Be(invite1.Id);
    }
}
