using PlayStead.Core.Library;
using PlayStead.Core.Persistence;
using PlayStead.Core.Scanning;
using PlayStead.Data.Database;

namespace PlayStead.UI.Bootstrap;

public sealed record LocalStartupState(
    DatabaseHealthResult Health,
    LibrarySnapshot Snapshot);

public sealed class LocalStartupPipeline
{
    private readonly DatabaseInitializer _databaseInitializer;
    private readonly DatabaseHealthChecker _databaseHealthChecker;
    private readonly ILibraryStore _libraryStore;
    private readonly LocalScanCoordinator _scanCoordinator;

    public LocalStartupPipeline(
        DatabaseInitializer databaseInitializer,
        DatabaseHealthChecker databaseHealthChecker,
        ILibraryStore libraryStore,
        LocalScanCoordinator scanCoordinator)
    {
        _databaseInitializer = databaseInitializer;
        _databaseHealthChecker = databaseHealthChecker;
        _libraryStore = libraryStore;
        _scanCoordinator = scanCoordinator;
    }

    public async Task<LocalStartupState> InitializeAsync(
        CancellationToken cancellationToken)
    {
        await _databaseInitializer.InitializeAsync(cancellationToken);

        var health = await _databaseHealthChecker.QuickCheckAsync(
            cancellationToken);

        var snapshot = await _libraryStore.LoadSnapshotAsync(
            cancellationToken);

        return new LocalStartupState(
            health,
            snapshot);
    }

    public async Task<LibrarySnapshot> RefreshAsync(
        CancellationToken cancellationToken)
    {
        var results = await _scanCoordinator.ScanAllAsync(
            cancellationToken);

        foreach (var result in results)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await _libraryStore.ApplySourceScanAsync(
                result,
                cancellationToken);
        }

        return await _libraryStore.LoadSnapshotAsync(
            cancellationToken);
    }
}
