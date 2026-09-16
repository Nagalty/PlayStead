namespace PlayStead.Core.Scanning;

public interface ILocalIdentityResolutionCoordinator
{
    Task ResolveAfterScanAsync(
        SourceScanResult result,
        CancellationToken cancellationToken);
}
