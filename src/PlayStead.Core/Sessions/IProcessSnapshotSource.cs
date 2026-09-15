namespace PlayStead.Core.Sessions;

public interface IProcessSnapshotSource
{
    Task<IReadOnlyList<ProcessSnapshot>> CaptureAsync(
        CancellationToken cancellationToken);

    async Task<ProcessCaptureResult> CaptureWithQualityAsync(
        CancellationToken cancellationToken)
        => new(await CaptureAsync(cancellationToken), true);
}
