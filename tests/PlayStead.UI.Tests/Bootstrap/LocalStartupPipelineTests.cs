using PlayStead.Core.Library;
using PlayStead.Core.Persistence;
using PlayStead.Core.Scanning;
using PlayStead.Data.Database;
using PlayStead.Data.Library;
using PlayStead.UI.Bootstrap;

namespace PlayStead.UI.Tests.Bootstrap;

public sealed class LocalStartupPipelineTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "PlayStead.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Initialize_runs_canonical_sync_and_continues_when_sync_reports_offline()
    {
        Directory.CreateDirectory(_root);
        var options = new DatabaseOptions(Path.Combine(_root, "playstead.db"), Path.Combine(_root, "Backups"));
        var sync = new RecordingCatalogSyncService(false);
        var sut = new LocalStartupPipeline(new DatabaseInitializer(options), new DatabaseHealthChecker(options), new SqliteLibraryStore(options), new LocalScanCoordinator([]), sync);

        var state = await sut.InitializeAsync(CancellationToken.None);

        Assert.True(state.Health.IsHealthy);
        Assert.Equal(1, sync.CallCount);
    }

    [Fact]
    public async Task Initialize_propagates_canonical_sync_cancellation()
    {
        Directory.CreateDirectory(_root);
        var options = new DatabaseOptions(Path.Combine(_root, "playstead.db"), Path.Combine(_root, "Backups"));
        var sync = new RecordingCatalogSyncService(false);
        var sut = new LocalStartupPipeline(new DatabaseInitializer(options), new DatabaseHealthChecker(options), new SqliteLibraryStore(options), new LocalScanCoordinator([]), sync);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sut.InitializeAsync(cancellation.Token));
        Assert.Equal(0, sync.CallCount);
    }

    [Fact]
    public async Task Initialize_runs_manual_reconciliation_after_canonical_sync_and_continues_when_it_reports_no_matches()
    {
        Directory.CreateDirectory(_root);
        var options = new DatabaseOptions(Path.Combine(_root, "playstead.db"), Path.Combine(_root, "Backups"));
        var order = new List<string>();
        var sync = new RecordingCatalogSyncService(false, () => order.Add("sync"));
        var reconciliation = new RecordingManualReconciliationService(() => order.Add("reconcile"));
        var sut = new LocalStartupPipeline(
            new DatabaseInitializer(options), new DatabaseHealthChecker(options),
            new SqliteLibraryStore(options), new LocalScanCoordinator([]), sync, reconciliation);

        var state = await sut.InitializeAsync(CancellationToken.None);

        Assert.True(state.Health.IsHealthy);
        Assert.Equal(["sync", "reconcile"], order);
        Assert.Equal(1, reconciliation.CallCount);
    }

    [Fact]
    public async Task Initialize_then_refresh_exposes_cached_snapshot_before_applying_local_scan()
    {
        Directory.CreateDirectory(_root);

        var options = new DatabaseOptions(
            Path.Combine(_root, "playstead.db"),
            Path.Combine(_root, "Backups"));

        ILibraryStore store = new SqliteLibraryStore(options);

        var observed = new DateTimeOffset(
            2026, 9, 12, 9, 0, 0, TimeSpan.Zero);

        var source = new StubSource(
            SourceScanResult.Success(
                ProviderKind.Steam,
                observed,
                [
                    DiscoveredInstallation.Create(
                        ProviderKind.Steam,
                        "730",
                        "Counter-Strike 2",
                        @"G:\SteamLibrary\steamapps\common\Counter-Strike Global Offensive",
                        42_000_000_000,
                        observed)
                ]));

        var sut = new LocalStartupPipeline(
            new DatabaseInitializer(options),
            new DatabaseHealthChecker(options),
            store,
            new LocalScanCoordinator([source]));

        var initial = await sut.InitializeAsync(
            CancellationToken.None);

        Assert.True(initial.Health.IsHealthy);
        Assert.Empty(initial.Snapshot.Games);
        Assert.Empty(initial.Snapshot.Installations);

        var refreshed = await sut.RefreshAsync(
            CancellationToken.None);

        var game = Assert.Single(refreshed.Games);
        var installation = Assert.Single(refreshed.Installations);

        Assert.Equal("Counter-Strike 2", game.Title);
        Assert.Equal(game.Id, installation.GameId);
        Assert.True(installation.IsPresent);
    }

    private sealed class StubSource(
        SourceScanResult result) : ILocalLibrarySource
    {
        public ProviderKind Provider => result.Provider;

        public Task<SourceScanResult> ScanAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult(result);
    }

    private sealed class RecordingCatalogSyncService(bool result, Action? onCall = null) : ICanonicalCatalogSyncService
    {
        public int CallCount { get; private set; }
        public Task<bool> SyncAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            onCall?.Invoke();
            return Task.FromResult(result);
        }
    }

    private sealed class RecordingManualReconciliationService(Action? onCall = null) : IManualMetadataReconciliationService
    {
#pragma warning disable CS0067
        public event EventHandler? Changed
        {
            add { }
            remove { }
        }
#pragma warning restore CS0067
        public int CallCount { get; private set; }
        public Task<ManualMetadataReconciliationResult> ReconcileAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            onCall?.Invoke();
            return Task.FromResult(new ManualMetadataReconciliationResult(0, 0, 0));
        }
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
