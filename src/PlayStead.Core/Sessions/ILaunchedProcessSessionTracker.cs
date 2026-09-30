namespace PlayStead.Core.Sessions;

public interface ILaunchedProcessSessionTracker
{
    void TrackLaunchedProcess(Guid gameId, int processId, DateTimeOffset startedAtUtc);
}
