using FluentAssertions;
using HotBox.Core.Entities;
using HotBox.Core.Enums;
using HotBox.Core.Options;
using HotBox.Infrastructure.Data;
using HotBox.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace HotBox.Infrastructure.Tests.Services;

/// <summary>
/// Tests for DemoUserService.
///
/// Infrastructure notes:
/// - Uses EF Core InMemory provider. ExecuteUpdate / ExecuteDelete bulk operations
///   (used by RecordActivityAsync and PurgeDemoUserAsync) are NOT supported by the
///   InMemory provider — those paths are covered by integration tests. Tests here
///   cover user creation, IP cooldown tracking, expiry queries, and count logic.
/// - Each test class instance gets its own named InMemory database so tests are
///   fully isolated from one another.
/// - The "Member" Identity role is seeded asynchronously in InitAsync(), called
///   from the static CreateAsync factory to work around xUnit's lack of async
///   constructor support.
/// </summary>
public class DemoUserServiceTests : IAsyncLifetime
{
    // Unique DB name per test class instance ensures test isolation.
    private readonly string _dbName = Guid.NewGuid().ToString();

    private ServiceProvider _rootProvider = null!;
    private ILogger<DemoUserService> _logger = null!;

    // -----------------------------------------------------------------------
    // IAsyncLifetime — async setup / teardown
    // -----------------------------------------------------------------------

    public async Task InitializeAsync()
    {
        _logger = Substitute.For<ILogger<DemoUserService>>();

        var services = new ServiceCollection();

        services.AddDbContext<HotBoxDbContext>(opt =>
            opt.UseInMemoryDatabase(_dbName));

        services.AddIdentityCore<AppUser>()
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<HotBoxDbContext>();

        _rootProvider = services.BuildServiceProvider();

        // Seed the "Member" role in a dedicated scope so it is persisted to the
        // shared InMemory database before any test creates a user.
        await using var scope = _rootProvider.CreateAsyncScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var roleExists = await roleManager.RoleExistsAsync("Member");
        if (!roleExists)
        {
            var result = await roleManager.CreateAsync(new IdentityRole<Guid>("Member"));
            result.Succeeded.Should().BeTrue("Member role must be seeded successfully");
        }
    }

    public async Task DisposeAsync()
    {
        if (_rootProvider is not null)
            await _rootProvider.DisposeAsync();
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private DemoUserService CreateSut(DemoModeOptions? options = null)
    {
        var opts = Options.Create(options ?? new DemoModeOptions
        {
            Enabled = true,
            MaxConcurrentUsers = 50,
            SessionTimeoutMinutes = 5,
            IpCooldownMinutes = 2,
        });
        return new DemoUserService(_rootProvider, opts, _logger);
    }

    /// <summary>
    /// Seeds a demo user directly into the shared InMemory database, bypassing
    /// DemoUserService so that the IP cooldown dictionary is not side-effected.
    /// </summary>
    private async Task<AppUser> SeedDemoUserAsync(Guid userId, DateTime lastSeenUtc)
    {
        await using var scope = _rootProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HotBoxDbContext>();

        var user = new AppUser
        {
            Id = userId,
            UserName = $"demo_seed_{userId:N}",
            NormalizedUserName = $"DEMO_SEED_{userId:N}".ToUpperInvariant(),
            Email = $"demo_{userId:N}@demo.local",
            NormalizedEmail = $"DEMO_{userId:N}@DEMO.LOCAL".ToUpperInvariant(),
            DisplayName = "Seeded Demo User",
            EmailConfirmed = true,
            IsDemo = true,
            Status = UserStatus.Offline,
            CreatedAtUtc = DateTime.UtcNow,
            LastSeenUtc = lastSeenUtc,
            SecurityStamp = Guid.NewGuid().ToString(),
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    // -----------------------------------------------------------------------
    // CreateDemoUserAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task CreateDemoUser_Success_ReturnsUserWithIsDemoTrue()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.CreateDemoUserAsync("alice", "Alice Demo", "10.0.0.1");

        // Assert
        result.Should().NotBeNull();
        result!.IsDemo.Should().BeTrue();
        result.DisplayName.Should().Be("Alice Demo");
        result.EmailConfirmed.Should().BeTrue();
    }

    [Fact]
    public async Task CreateDemoUser_Success_AssignsMemberRole()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.CreateDemoUserAsync("bob", "Bob Demo", "10.0.0.2");

        // Assert
        result.Should().NotBeNull();
        await using var scope = _rootProvider.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var roles = await userManager.GetRolesAsync(result!);
        roles.Should().Contain("Member");
    }

    [Fact]
    public async Task CreateDemoUser_Success_GeneratesUniquePrefixedUserName()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.CreateDemoUserAsync("charlie", "Charlie", "10.0.0.3");

        // Assert
        result.Should().NotBeNull();
        result!.UserName.Should().StartWith("demo_charlie_");
    }

    [Fact]
    public async Task CreateDemoUser_Success_SetsLastSeenUtcToNow()
    {
        // Arrange
        var sut = CreateSut();
        var before = DateTime.UtcNow;

        // Act
        var result = await sut.CreateDemoUserAsync("dave", "Dave", "10.0.0.4");

        // Assert
        result.Should().NotBeNull();
        result!.LastSeenUtc.Should().BeOnOrAfter(before);
        result.LastSeenUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task CreateDemoUser_ReturnsNull_WhenCapacityFull()
    {
        // Arrange — MaxConcurrentUsers = 0 means capacity is always full
        var sut = CreateSut(new DemoModeOptions
        {
            Enabled = true,
            MaxConcurrentUsers = 0,
        });

        // Act
        var result = await sut.CreateDemoUserAsync("blocked", "Blocked", "10.0.0.99");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateDemoUser_RecordsIpCooldownAfterCreation()
    {
        // Arrange
        var sut = CreateSut();
        const string ip = "10.0.0.5";

        // Act
        await sut.CreateDemoUserAsync("eve", "Eve", ip);

        // Assert — same IP is now in cooldown
        var isCoolingDown = await sut.IsIpCoolingDownAsync(ip);
        isCoolingDown.Should().BeTrue();
    }

    // -----------------------------------------------------------------------
    // IsIpCoolingDownAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task IsIpCoolingDown_ReturnsFalse_WhenNoRecord()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.IsIpCoolingDownAsync("172.16.0.1");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsIpCoolingDown_ReturnsTrue_WithinCooldown()
    {
        // Arrange — cooldown of 2 minutes; registration just happened
        var sut = CreateSut(new DemoModeOptions
        {
            Enabled = true,
            MaxConcurrentUsers = 50,
            IpCooldownMinutes = 2,
        });
        const string ip = "172.16.0.2";

        // Register a user from this IP to plant the cooldown entry
        await sut.CreateDemoUserAsync("frank", "Frank", ip);

        // Act — immediately check (well within the 2-minute window)
        var result = await sut.IsIpCoolingDownAsync(ip);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsIpCoolingDown_ReturnsFalse_AfterCooldownExpires()
    {
        // Arrange — zero-minute cooldown so it expires immediately
        var sut = CreateSut(new DemoModeOptions
        {
            Enabled = true,
            MaxConcurrentUsers = 50,
            IpCooldownMinutes = 0,
        });
        const string ip = "172.16.0.3";

        await sut.CreateDemoUserAsync("grace", "Grace", ip);

        // Act — cooldown of 0 minutes has already elapsed
        var result = await sut.IsIpCoolingDownAsync(ip);

        // Assert
        result.Should().BeFalse();
    }

    // -----------------------------------------------------------------------
    // GetActiveDemoUserCountAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetActiveDemoUserCount_ReturnsOnlyDemoUsers()
    {
        // Arrange — one demo user seeded directly and one non-demo user
        var sut = CreateSut();

        await SeedDemoUserAsync(Guid.NewGuid(), DateTime.UtcNow);

        await using var scope = _rootProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HotBoxDbContext>();
        db.Users.Add(new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = "regular_user",
            NormalizedUserName = "REGULAR_USER",
            Email = "regular@example.com",
            NormalizedEmail = "REGULAR@EXAMPLE.COM",
            DisplayName = "Regular",
            EmailConfirmed = true,
            IsDemo = false,
            Status = UserStatus.Offline,
            CreatedAtUtc = DateTime.UtcNow,
            LastSeenUtc = DateTime.UtcNow,
            SecurityStamp = Guid.NewGuid().ToString(),
        });
        await db.SaveChangesAsync();

        // Act
        var count = await sut.GetActiveDemoUserCountAsync();

        // Assert
        count.Should().Be(1);
    }

    // -----------------------------------------------------------------------
    // GetExpiredDemoUserIdsAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetExpiredDemoUserIds_ReturnsExpiredUsers()
    {
        // Arrange — user whose last activity was 10 min ago (past 5-min timeout)
        var sut = CreateSut(new DemoModeOptions
        {
            Enabled = true,
            MaxConcurrentUsers = 50,
            SessionTimeoutMinutes = 5,
        });

        var expiredId = Guid.NewGuid();
        await SeedDemoUserAsync(expiredId, DateTime.UtcNow.AddMinutes(-10));

        // Act
        var result = await sut.GetExpiredDemoUserIdsAsync();

        // Assert
        result.Should().Contain(expiredId);
    }

    [Fact]
    public async Task GetExpiredDemoUserIds_ExcludesActiveUsers()
    {
        // Arrange — user active 1 minute ago (within 5-minute timeout)
        var sut = CreateSut(new DemoModeOptions
        {
            Enabled = true,
            MaxConcurrentUsers = 50,
            SessionTimeoutMinutes = 5,
        });

        var activeId = Guid.NewGuid();
        await SeedDemoUserAsync(activeId, DateTime.UtcNow.AddMinutes(-1));

        // Act
        var result = await sut.GetExpiredDemoUserIdsAsync();

        // Assert
        result.Should().NotContain(activeId);
    }

    [Fact]
    public async Task GetExpiredDemoUserIds_ExcludesNonDemoUsers()
    {
        // Arrange — non-demo user with a very old LastSeenUtc
        var sut = CreateSut(new DemoModeOptions
        {
            Enabled = true,
            MaxConcurrentUsers = 50,
            SessionTimeoutMinutes = 5,
        });

        var regularId = Guid.NewGuid();
        await using var scope = _rootProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HotBoxDbContext>();
        db.Users.Add(new AppUser
        {
            Id = regularId,
            UserName = "old_regular",
            NormalizedUserName = "OLD_REGULAR",
            Email = "old@example.com",
            NormalizedEmail = "OLD@EXAMPLE.COM",
            DisplayName = "Old Regular",
            EmailConfirmed = true,
            IsDemo = false,
            Status = UserStatus.Offline,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-30),
            LastSeenUtc = DateTime.UtcNow.AddDays(-30),
            SecurityStamp = Guid.NewGuid().ToString(),
        });
        await db.SaveChangesAsync();

        // Act
        var result = await sut.GetExpiredDemoUserIdsAsync();

        // Assert
        result.Should().NotContain(regularId);
    }

    // -----------------------------------------------------------------------
    // RecordActivityAsync
    //
    // RecordActivityAsync uses ExecuteUpdateAsync (bulk UPDATE), which the EF
    // InMemory provider does not support. These tests verify the interface
    // contract at the boundary (correct method signature, no unexpected throws
    // for benign inputs) and document the InMemory limitation explicitly.
    // Full DB round-trip verification lives in integration tests.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task RecordActivity_ThrowsExpectedInMemoryLimitation_ForDemoUser()
    {
        // Arrange
        var sut = CreateSut();
        var userId = Guid.NewGuid();
        await SeedDemoUserAsync(userId, DateTime.UtcNow.AddMinutes(-10));

        // Act — EF InMemory cannot translate ExecuteUpdateAsync; document the known
        // limitation rather than hiding it behind a misleading "NotThrow" assertion.
        var act = () => sut.RecordActivityAsync(userId);

        // Assert — InvalidOperationException is the expected InMemory provider error;
        // this test documents the boundary and will become NotThrowAsync once the
        // test suite switches to a SQLite-in-memory provider.
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*could not be translated*");
    }
}
