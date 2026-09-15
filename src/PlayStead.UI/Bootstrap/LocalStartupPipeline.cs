using PlayStead.Core.Library;
using PlayStead.Core.Persistence;
using PlayStead.Core.Scanning;
using PlayStead.Data.Database;
using PlayStead.UI.Steam;
using PlayStead.UI.Sessions;

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
    private readonly DiscoveryInventoryManager? _discoveryInventory;
    private readonly SemaphoreSlim _refreshSemaphore = new(1, 1);
    private readonly object _refreshRequestGate = new();
    private long _latestRefreshRequest;

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

    public LocalStartupPipeline(
        DatabaseInitializer databaseInitializer,
        DatabaseHealthChecker databaseHealthChecker,
        ILibraryStore libraryStore,
        LocalScanCoordinator scanCoordinator,
        ISteamReferenceRuntime steamReferenceRuntime,
        DiscoveryInventoryManager discoveryInventory)
        : this(databaseInitializer, databaseHealthChecker, libraryStore,
            scanCoordinator, steamReferenceRuntime)
    {
        _discoveryInventory = discoveryInventory
            ?? throw new ArgumentNullException(nameof(discoveryInventory));
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

        if (health.IsHealthy)
            _discoveryInventory?.Schedule(snapshot, cancellationToken);

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
        cancellationToken.ThrowIfCancellationRequested();
        long request;
        lock (_refreshRequestGate)
        {
            request = ++_latestRefreshRequest;
            _discoveryInventory?.MarkRefreshing();
        }

        await _refreshSemaphore.WaitAsync(cancellationToken);
        try
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

            var snapshot = await _libraryStore.LoadSnapshotAsync(
                cancellationToken);
            lock (_refreshRequestGate)
            {
                if (request == _latestRefreshRequest)
                    _discoveryInventory?.Schedule(snapshot, cancellationToken);
            }

            return snapshot;
        }
        finally
        {
            _refreshSemaphore.Release();
        }
    }
}
