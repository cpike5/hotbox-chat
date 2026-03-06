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
    /// Event raised before a demo user is purged. Program.cs wires this to
    /// IHubContext to send DemoSessionExpired notifications via SignalR.
    /// Fired before purge so the client receives notification while still connected.
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
                // Notify BEFORE purge so the client receives the SignalR message
                // while the user still exists and the connection is active
                if (OnDemoUserPurged is not null)
                {
                    await OnDemoUserPurged.Invoke(userId);
                }

                await _demoUserService.PurgeDemoUserAsync(userId, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to purge expired demo user {UserId}", userId);
            }
        }

        // Prune stale IP cooldown entries to prevent memory leaks
        _demoUserService.PruneExpiredIpCooldowns();
    }
}
