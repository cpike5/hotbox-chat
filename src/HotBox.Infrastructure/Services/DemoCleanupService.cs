using HotBox.Core.Interfaces;
using HotBox.Core.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HotBox.Infrastructure.Services;

public class DemoCleanupService : BackgroundService
{
    private readonly IDemoUserService _demoUserService;
    private readonly DemoModeOptions _options;
    private readonly ILogger<DemoCleanupService> _logger;

    /// <summary>
    /// Event raised when a demo user is purged. Program.cs wires this to
    /// IHubContext to send DemoSessionExpired notifications via SignalR.
    /// </summary>
    public event Func<Guid, Task>? OnDemoUserPurged;

    public DemoCleanupService(
        IDemoUserService demoUserService,
        IOptions<DemoModeOptions> options,
        ILogger<DemoCleanupService> logger)
    {
        _demoUserService = demoUserService;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Demo mode is disabled, cleanup service will not run");
            return;
        }

        _logger.LogInformation(
            "Demo cleanup service started with interval {IntervalMinutes}m and timeout {TimeoutMinutes}m",
            _options.CleanupIntervalMinutes,
            _options.SessionTimeoutMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunCleanupAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during demo user cleanup cycle");
            }

            try
            {
                await Task.Delay(_options.CleanupInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("Demo cleanup service stopped");
    }

    private async Task RunCleanupAsync(CancellationToken ct)
    {
        var expiredUserIds = await _demoUserService.GetExpiredDemoUserIdsAsync(ct);

        if (expiredUserIds.Count == 0)
        {
            return;
        }

        _logger.LogInformation("Found {Count} expired demo users to purge", expiredUserIds.Count);

        foreach (var userId in expiredUserIds)
        {
            try
            {
                await _demoUserService.PurgeDemoUserAsync(userId, ct);

                if (OnDemoUserPurged is not null)
                {
                    await OnDemoUserPurged.Invoke(userId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to purge expired demo user {UserId}", userId);
            }
        }

        // Prune stale IP cooldown entries to prevent memory leaks
        if (_demoUserService is DemoUserService concreteService)
        {
            concreteService.PruneExpiredIpCooldowns();
        }
    }
}
