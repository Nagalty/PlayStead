namespace PlayStead.UI.Launching;

public interface IManualSessionLaunchSink
{
    void TrackLaunchedProcess(
        Guid gameId,
        int processId,
        DateTimeOffset startedAtUtc);
}
