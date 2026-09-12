using PlayStead.Core.Library;
using PlayStead.Core.Scanning;

namespace PlayStead.Core.Persistence;

public interface ILibraryStore
{
    Task ApplySourceScanAsync(
        SourceScanResult result,
        CancellationToken cancellationToken);

    Task<LibrarySnapshot> LoadSnapshotAsync(
        CancellationToken cancellationToken);
}
