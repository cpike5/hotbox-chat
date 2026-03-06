namespace HotBox.Core.Options;

public class DemoModeOptions
{
    public const string SectionName = "DemoMode";

    public bool Enabled { get; set; }

    public int MaxConcurrentUsers { get; set; } = 50;

    public int SessionTimeoutMinutes { get; set; } = 5;

    public int CleanupIntervalMinutes { get; set; } = 1;

    public int IpCooldownMinutes { get; set; } = 2;

    public string[] SeedChannels { get; set; } = ["General", "Games", "Music"];

    public TimeSpan SessionTimeout => TimeSpan.FromMinutes(SessionTimeoutMinutes);

    public TimeSpan CleanupInterval => TimeSpan.FromMinutes(CleanupIntervalMinutes);

    public TimeSpan IpCooldown => TimeSpan.FromMinutes(IpCooldownMinutes);
}
