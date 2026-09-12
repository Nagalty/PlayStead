namespace PlayStead.Core.Sessions;

public interface IProcessSnapshotSource
{
    Task<IReadOnlyList<ProcessSnapshot>> CaptureAsync(
        CancellationToken cancellationToken);
}
