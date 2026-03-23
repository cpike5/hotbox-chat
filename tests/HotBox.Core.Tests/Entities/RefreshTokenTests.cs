using HotBox.Core.Entities;

namespace HotBox.Core.Tests.Entities;

public class RefreshTokenTests
{
    [Fact]
    public void IsExpired_WhenPastExpiry_ReturnsTrue()
    {
        var token = new RefreshToken
        {
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1),
        };

        token.IsExpired.Should().BeTrue();
    }

    [Fact]
    public void IsExpired_WhenBeforeExpiry_ReturnsFalse()
    {
        var token = new RefreshToken
        {
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
        };

        token.IsExpired.Should().BeFalse();
    }

    [Fact]
    public void IsRevoked_WhenRevokedAtSet_ReturnsTrue()
    {
        var token = new RefreshToken
        {
            RevokedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
        };

        token.IsRevoked.Should().BeTrue();
    }

    [Fact]
    public void IsActive_WhenNotRevokedAndNotExpired_ReturnsTrue()
    {
        var token = new RefreshToken
        {
            ExpiresAt = DateTime.UtcNow.AddDays(7),
        };

        token.IsActive.Should().BeTrue();
    }

    [Fact]
    public void IsActive_WhenRevoked_ReturnsFalse()
    {
        var token = new RefreshToken
        {
            RevokedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
        };

        token.IsActive.Should().BeFalse();
    }
}
