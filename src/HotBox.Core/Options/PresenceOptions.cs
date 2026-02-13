namespace HotBox.Core.Options;

public class PresenceOptions
{
    public const string SectionName = "Presence";

    public TimeSpan GracePeriod { get; set; } = TimeSpan.FromSeconds(30);

    public TimeSpan IdleTimeout { get; set; } = TimeSpan.FromMinutes(5);

    public TimeSpan AgentInactivityTimeout { get; set; } = TimeSpan.FromMinutes(5);
}
