using PlayStead.Core.Library;

namespace PlayStead.Core.Scanning;

public interface ILocalLibrarySource
{
    ProviderKind Provider { get; }

    Task<SourceScanResult> ScanAsync(
        CancellationToken cancellationToken);
}
