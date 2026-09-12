using PlayStead.Core.Library;
using PlayStead.Core.Persistence;
using PlayStead.Core.Scanning;
using PlayStead.Data.Database;
using PlayStead.UI.Steam;

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
    private readonly ISteamReferenceRuntime? _steamReferenceRuntime;

    public LocalStartupPipeline(
        DatabaseInitializer databaseInitializer,
        DatabaseHealthChecker databaseHealthChecker,
        ILibraryStore libraryStore,
        LocalScanCoordinator scanCoordinator)
    {
        ArgumentNullException.ThrowIfNull(databaseInitializer);
        ArgumentNullException.ThrowIfNull(databaseHealthChecker);
        ArgumentNullException.ThrowIfNull(libraryStore);
        ArgumentNullException.ThrowIfNull(scanCoordinator);

        _databaseInitializer = databaseInitializer;
        _databaseHealthChecker = databaseHealthChecker;
        _libraryStore = libraryStore;
        _scanCoordinator = scanCoordinator;
    }

    public LocalStartupPipeline(
        DatabaseInitializer databaseInitializer,
        DatabaseHealthChecker databaseHealthChecker,
        ILibraryStore libraryStore,
        LocalScanCoordinator scanCoordinator,
        ISteamReferenceRuntime steamReferenceRuntime)
        : this(
            databaseInitializer,
            databaseHealthChecker,
            libraryStore,
            scanCoordinator)
    {
        ArgumentNullException.ThrowIfNull(steamReferenceRuntime);

        _steamReferenceRuntime =
            steamReferenceRuntime;
    }

    public async Task<LocalStartupState> InitializeAsync(
        CancellationToken cancellationToken)
    {
        await _databaseInitializer.InitializeAsync(
            cancellationToken);

        var health =
            await _databaseHealthChecker.QuickCheckAsync(
                cancellationToken);

        var snapshot =
            await _libraryStore.LoadSnapshotAsync(
                cancellationToken);

        if (_steamReferenceRuntime is not null)
        {
            await _steamReferenceRuntime.LoadCachedAsync(
                cancellationToken);
        }

        return new LocalStartupState(
            health,
            snapshot);
    }

    public async Task<LibrarySnapshot> RefreshAsync(
        CancellationToken cancellationToken)
    {
        var results =
            await _scanCoordinator.ScanAllAsync(
                cancellationToken);

        foreach (var result in results)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await _libraryStore.ApplySourceScanAsync(
                result,
                cancellationToken);
        }

        if (_steamReferenceRuntime is not null)
        {
            await _steamReferenceRuntime.RefreshStaleAsync(
                cancellationToken);
        }

        return await _libraryStore.LoadSnapshotAsync(
            cancellationToken);
    }
}
