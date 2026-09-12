namespace PlayStead.Core.Sessions;

public sealed record SessionMonitorOptions(
    TimeSpan PollInterval)
{
    public static SessionMonitorOptions Default { get; } =
        new(TimeSpan.FromSeconds(2));
}
