namespace PlayStead.Core.Sessions;

public interface IProcessCaptureObserver
{
    Task ObserveAsync(ProcessCaptureResult capture, DateTimeOffset observedAtUtc, CancellationToken cancellationToken);
    void MarkCaptureGap();
}
