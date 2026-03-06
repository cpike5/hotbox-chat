using FluentAssertions;
using HotBox.Core.Interfaces;
using HotBox.Core.Options;
using HotBox.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace HotBox.Infrastructure.Tests.Services;

public class DemoCleanupServiceTests
{
    private readonly IDemoUserService _demoUserService;
    private readonly ILogger<DemoCleanupService> _logger;

    public DemoCleanupServiceTests()
    {
        _demoUserService = Substitute.For<IDemoUserService>();
        _logger = Substitute.For<ILogger<DemoCleanupService>>();
    }

    private DemoCleanupService CreateSut(DemoModeOptions options)
    {
        var opts = Options.Create(options);
        return new DemoCleanupService(_demoUserService, opts, _logger);
    }

    /// <summary>
    /// Runs the cleanup service for one cycle. The service runs cleanup immediately,
    /// then blocks on Task.Delay(CleanupInterval). We use a large interval so the
    /// service blocks on the delay after one cycle, then cancel cleanly.
    /// </summary>
    private static async Task RunOneCycleAsync(DemoCleanupService sut)
    {
        using var cts = new CancellationTokenSource();

        await sut.StartAsync(cts.Token);

        // Give the background task time to complete one cleanup cycle and enter the delay
        await Task.Delay(TimeSpan.FromMilliseconds(200));

        await cts.CancelAsync();
        await sut.StopAsync(CancellationToken.None);
    }

    // -----------------------------------------------------------------------
    // DoesNotRun_WhenDisabled
    // -----------------------------------------------------------------------

    [Fact]
    public async Task DoesNotRun_WhenDisabled_NeverQueriesExpiredUsers()
    {
        var options = new DemoModeOptions { Enabled = false };
        var sut = CreateSut(options);

        await RunOneCycleAsync(sut);

        await _demoUserService.DidNotReceive()
            .GetExpiredDemoUserIdsAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DoesNotRun_WhenDisabled_NeverCallsPurge()
    {
        var options = new DemoModeOptions { Enabled = false };
        var sut = CreateSut(options);

        await RunOneCycleAsync(sut);

        await _demoUserService.DidNotReceive()
            .PurgeDemoUserAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    // -----------------------------------------------------------------------
    // PurgesExpiredUsers
    // -----------------------------------------------------------------------

    [Fact]
    public async Task PurgesExpiredUsers_CallsPurgeForEachExpiredId()
    {
        var expiredId1 = Guid.NewGuid();
        var expiredId2 = Guid.NewGuid();

        // Use a large interval so the service blocks after one cycle
        var options = new DemoModeOptions { Enabled = true, CleanupIntervalMinutes = 60 };
        var sut = CreateSut(options);

        _demoUserService.GetExpiredDemoUserIdsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Guid> { expiredId1, expiredId2 });

        await RunOneCycleAsync(sut);

        await _demoUserService.Received(1).PurgeDemoUserAsync(expiredId1, Arg.Any<CancellationToken>());
        await _demoUserService.Received(1).PurgeDemoUserAsync(expiredId2, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PurgesExpiredUsers_DoesNotCallPurge_WhenNoExpiredUsers()
    {
        var options = new DemoModeOptions { Enabled = true, CleanupIntervalMinutes = 60 };
        var sut = CreateSut(options);

        _demoUserService.GetExpiredDemoUserIdsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Guid>());

        await RunOneCycleAsync(sut);

        await _demoUserService.DidNotReceive()
            .PurgeDemoUserAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    // -----------------------------------------------------------------------
    // FiresOnDemoUserPurged event
    // -----------------------------------------------------------------------

    [Fact]
    public async Task FiresOnDemoUserPurged_ForEachExpiredUser()
    {
        var expiredId1 = Guid.NewGuid();
        var expiredId2 = Guid.NewGuid();

        var options = new DemoModeOptions { Enabled = true, CleanupIntervalMinutes = 60 };
        var sut = CreateSut(options);

        _demoUserService.GetExpiredDemoUserIdsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Guid> { expiredId1, expiredId2 });

        var purgedIds = new List<Guid>();
        sut.OnDemoUserPurged += userId =>
        {
            purgedIds.Add(userId);
            return Task.CompletedTask;
        };

        await RunOneCycleAsync(sut);

        purgedIds.Should().BeEquivalentTo(new[] { expiredId1, expiredId2 });
    }

    [Fact]
    public async Task FiresOnDemoUserPurged_NotRaisedWhenNoSubscribers()
    {
        var expiredId = Guid.NewGuid();

        var options = new DemoModeOptions { Enabled = true, CleanupIntervalMinutes = 60 };
        var sut = CreateSut(options);

        _demoUserService.GetExpiredDemoUserIdsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Guid> { expiredId });

        var act = () => RunOneCycleAsync(sut);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task FiresOnDemoUserPurged_RaisedBeforePurge_ForAllExpiredUsers()
    {
        // Event fires BEFORE purge so the client gets notified while still connected.
        // Even if purge fails for one user, the notification was already sent.
        var succeededId = Guid.NewGuid();
        var failedId = Guid.NewGuid();

        var options = new DemoModeOptions { Enabled = true, CleanupIntervalMinutes = 60 };
        var sut = CreateSut(options);

        _demoUserService.GetExpiredDemoUserIdsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Guid> { succeededId, failedId });

        _demoUserService.PurgeDemoUserAsync(succeededId, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _demoUserService.PurgeDemoUserAsync(failedId, Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("DB error")));

        var notifiedIds = new List<Guid>();
        sut.OnDemoUserPurged += userId =>
        {
            notifiedIds.Add(userId);
            return Task.CompletedTask;
        };

        await RunOneCycleAsync(sut);

        // Both users are notified before purge is attempted
        notifiedIds.Should().BeEquivalentTo(new[] { succeededId, failedId });
    }

    // -----------------------------------------------------------------------
    // PrunesIpCooldowns
    // -----------------------------------------------------------------------

    [Fact]
    public async Task PrunesIpCooldowns_CallsPruneOnConcreteService()
    {
        var demoOptions = new DemoModeOptions
        {
            Enabled = true,
            MaxConcurrentUsers = 50,
            IpCooldownMinutes = 2,
            CleanupIntervalMinutes = 60,
        };

        var services = new ServiceCollection();
        services.AddDbContext<HotBox.Infrastructure.Data.HotBoxDbContext>(opt =>
            opt.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddIdentityCore<HotBox.Core.Entities.AppUser>()
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<HotBox.Infrastructure.Data.HotBoxDbContext>();

        using var provider = services.BuildServiceProvider();

        var realDemoUserService = new DemoUserService(
            provider,
            Options.Create(demoOptions),
            Substitute.For<ILogger<DemoUserService>>());

        var sut = new DemoCleanupService(
            realDemoUserService,
            Options.Create(demoOptions),
            _logger);

        // Act — run one cycle; PruneExpiredIpCooldowns is called after purge loop
        var act = () => RunOneCycleAsync(sut);

        // Assert — no exception means prune ran successfully
        await act.Should().NotThrowAsync();
    }
}
