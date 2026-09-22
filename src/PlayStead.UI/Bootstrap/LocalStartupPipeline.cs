using PlayStead.Core.Library;
using PlayStead.Core.Persistence;
using PlayStead.Core.Scanning;
using PlayStead.Data.Database;
using PlayStead.Data.Catalog;
using PlayStead.Providers.Steam;
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
    private readonly CatalogDatabaseInitializer? _catalogDatabaseInitializer;
    private readonly ILocalIdentityResolutionCoordinator?
        _identityResolutionCoordinator;
    private readonly NotificationRetentionStartup?
        _notificationRetentionStartup;
    private readonly SteamLocalCatalogBootstrapper?
        _steamLocalCatalogBootstrapper;
    private readonly StartupProgressState? _startupProgress;
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
        NotificationRetentionStartup notificationRetentionStartup)
        : this(databaseInitializer, databaseHealthChecker, libraryStore, scanCoordinator)
    {
        _notificationRetentionStartup = notificationRetentionStartup
            ?? throw new ArgumentNullException(nameof(notificationRetentionStartup));
    }

    public LocalStartupPipeline(
        DatabaseInitializer databaseInitializer,
        DatabaseHealthChecker databaseHealthChecker,
        ILibraryStore libraryStore,
        LocalScanCoordinator scanCoordinator,
        ISteamReferenceRuntime steamReferenceRuntime,
        DiscoveryInventoryManager discoveryInventory,
        CatalogDatabaseInitializer catalogDatabaseInitializer,
        ILocalIdentityResolutionCoordinator identityResolutionCoordinator,
        NotificationRetentionStartup notificationRetentionStartup,
        SteamLocalCatalogBootstrapper steamLocalCatalogBootstrapper,
        StartupProgressState startupProgress)
        : this(databaseInitializer, databaseHealthChecker, libraryStore, scanCoordinator,
            steamReferenceRuntime, discoveryInventory, catalogDatabaseInitializer,
            identityResolutionCoordinator, notificationRetentionStartup)
    {
        _steamLocalCatalogBootstrapper = steamLocalCatalogBootstrapper
            ?? throw new ArgumentNullException(nameof(steamLocalCatalogBootstrapper));
        _startupProgress = startupProgress ?? throw new ArgumentNullException(nameof(startupProgress));
    }

    public LocalStartupPipeline(
        DatabaseInitializer databaseInitializer,
        DatabaseHealthChecker databaseHealthChecker,
        ILibraryStore libraryStore,
        LocalScanCoordinator scanCoordinator,
        ILocalIdentityResolutionCoordinator identityResolutionCoordinator)
        : this(
            databaseInitializer,
            databaseHealthChecker,
            libraryStore,
            scanCoordinator)
    {
        _identityResolutionCoordinator = identityResolutionCoordinator
            ?? throw new ArgumentNullException(
                nameof(identityResolutionCoordinator));
    }

    public LocalStartupPipeline(
        DatabaseInitializer databaseInitializer,
        DatabaseHealthChecker databaseHealthChecker,
        ILibraryStore libraryStore,
        LocalScanCoordinator scanCoordinator,
        ISteamReferenceRuntime steamReferenceRuntime,
        DiscoveryInventoryManager discoveryInventory,
        CatalogDatabaseInitializer catalogDatabaseInitializer,
        ILocalIdentityResolutionCoordinator identityResolutionCoordinator,
        NotificationRetentionStartup notificationRetentionStartup)
        : this(
            databaseInitializer,
            databaseHealthChecker,
            libraryStore,
            scanCoordinator,
            steamReferenceRuntime,
            discoveryInventory,
            catalogDatabaseInitializer,
            identityResolutionCoordinator)
    {
        _notificationRetentionStartup = notificationRetentionStartup
            ?? throw new ArgumentNullException(nameof(notificationRetentionStartup));
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

    public LocalStartupPipeline(
        DatabaseInitializer databaseInitializer,
        DatabaseHealthChecker databaseHealthChecker,
        ILibraryStore libraryStore,
        LocalScanCoordinator scanCoordinator,
        ISteamReferenceRuntime steamReferenceRuntime,
        DiscoveryInventoryManager discoveryInventory,
        CatalogDatabaseInitializer catalogDatabaseInitializer)
        : this(databaseInitializer, databaseHealthChecker, libraryStore, scanCoordinator,
            steamReferenceRuntime, discoveryInventory)
    {
        _catalogDatabaseInitializer = catalogDatabaseInitializer
            ?? throw new ArgumentNullException(nameof(catalogDatabaseInitializer));
    }

    public LocalStartupPipeline(
        DatabaseInitializer databaseInitializer,
        DatabaseHealthChecker databaseHealthChecker,
        ILibraryStore libraryStore,
        LocalScanCoordinator scanCoordinator,
        ISteamReferenceRuntime steamReferenceRuntime,
        DiscoveryInventoryManager discoveryInventory,
        CatalogDatabaseInitializer catalogDatabaseInitializer,
        ILocalIdentityResolutionCoordinator identityResolutionCoordinator)
        : this(
            databaseInitializer,
            databaseHealthChecker,
            libraryStore,
            scanCoordinator,
            steamReferenceRuntime,
            discoveryInventory,
            catalogDatabaseInitializer)
    {
        _identityResolutionCoordinator = identityResolutionCoordinator
            ?? throw new ArgumentNullException(
                nameof(identityResolutionCoordinator));
    }

    public async Task<LocalStartupState> InitializeAsync(
        CancellationToken cancellationToken)
    {
        System.Diagnostics.Trace.WriteLine("[STARTUP] BEGIN DatabaseInitializer.InitializeAsync");
        await _databaseInitializer.InitializeAsync(
            cancellationToken);
        System.Diagnostics.Trace.WriteLine("[STARTUP] END DatabaseInitializer.InitializeAsync");

        if (_catalogDatabaseInitializer is not null)
        {
            System.Diagnostics.Trace.WriteLine("[STARTUP] BEGIN CatalogDatabaseInitializer.InitializeAsync");
            await _catalogDatabaseInitializer.InitializeAsync(cancellationToken);
            System.Diagnostics.Trace.WriteLine("[STARTUP] END CatalogDatabaseInitializer.InitializeAsync");
        }

        if (_notificationRetentionStartup is not null)
        {
            System.Diagnostics.Trace.WriteLine("[STARTUP] BEGIN NotificationRetentionStartup.InitializeAsync");
            await _notificationRetentionStartup.InitializeAsync(cancellationToken);
            System.Diagnostics.Trace.WriteLine("[STARTUP] END NotificationRetentionStartup.InitializeAsync");
        }

        System.Diagnostics.Trace.WriteLine("[STARTUP] BEGIN DatabaseHealthChecker.QuickCheckAsync");
        var health =
            await _databaseHealthChecker.QuickCheckAsync(
                cancellationToken);
        System.Diagnostics.Trace.WriteLine("[STARTUP] END DatabaseHealthChecker.QuickCheckAsync");

        System.Diagnostics.Trace.WriteLine("[STARTUP] BEGIN ILibraryStore.LoadSnapshotAsync");
        var snapshot =
            await _libraryStore.LoadSnapshotAsync(
                cancellationToken);
        System.Diagnostics.Trace.WriteLine("[STARTUP] END ILibraryStore.LoadSnapshotAsync");

        if (health.IsHealthy && _discoveryInventory is { } discoveryInventory)
        {
            discoveryInventory.Schedule(snapshot, cancellationToken);
            await discoveryInventory.AwaitIdleAsync(cancellationToken);
            var contexts = discoveryInventory.GetCurrentContexts();
        }

        if (_steamReferenceRuntime is not null)
        {
            System.Diagnostics.Trace.WriteLine("[STARTUP] BEGIN ISteamReferenceRuntime.LoadCachedAsync");
            await _steamReferenceRuntime.LoadCachedAsync(
                cancellationToken);
            System.Diagnostics.Trace.WriteLine("[STARTUP] END ISteamReferenceRuntime.LoadCachedAsync");
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

                if (_identityResolutionCoordinator is not null)
                {
                    try
                    {
                        await _identityResolutionCoordinator
                            .ResolveAfterScanAsync(
                                result,
                                cancellationToken);
                    }
                    catch (OperationCanceledException)
                        when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch
                    {
                        // Identity resolution is a best-effort sidecar.
                    }
                }
            }

            if (_steamLocalCatalogBootstrapper is not null)
            {
                StartupForensicTrace.Write("SteamCatalogBootstrap.Begin");
                try
                {
                    var bootstrapSnapshot =
                        await _libraryStore.LoadSnapshotAsync(cancellationToken);
                    var progressContext = SynchronizationContext.Current;
                    await RunSteamCatalogBootstrapAsync(
                        _steamLocalCatalogBootstrapper,
                        bootstrapSnapshot,
                        DateTimeOffset.UtcNow,
                        _startupProgress,
                        progressContext,
                        cancellationToken);
                }
                catch (OperationCanceledException)
                    when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    System.Diagnostics.Trace.TraceWarning(
                        "Local Steam catalog bootstrap failed; library scan remains available. {0}",
                        exception);
                }
                finally
                {
                    StartupForensicTrace.Write("SteamCatalogBootstrap.End");
                }
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

    internal static Task RunSteamCatalogBootstrapAsync(
        SteamLocalCatalogBootstrapper bootstrapper,
        LibrarySnapshot snapshot,
        DateTimeOffset observedAtUtc,
        StartupProgressState? startupProgress,
        SynchronizationContext? progressContext,
        CancellationToken cancellationToken) =>
        Task.Run(
            () => bootstrapper.RunAsync(
                snapshot,
                observedAtUtc,
                cancellationToken,
                progress => ReportSteamCatalogProgress(
                    progressContext,
                    startupProgress,
                    progress)),
            cancellationToken);

    private static void ReportSteamCatalogProgress(
        SynchronizationContext? progressContext,
        StartupProgressState? startupProgress,
        SteamCatalogBootstrapProgress progress)
    {
        if (startupProgress is null)
            return;

        void Apply() => startupProgress.Report(
            StartupStage.EnrichingCatalog,
            "Enrichissement du catalogue Steam",
            progress.Current,
            progress.Total);

        if (progressContext is null || ReferenceEquals(SynchronizationContext.Current, progressContext))
        {
            Apply();
            return;
        }

        progressContext.Send(_ => Apply(), null);
    }
}
