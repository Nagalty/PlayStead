using Microsoft.Data.Sqlite;
using PlayStead.Core.Library;
using PlayStead.Core.Persistence;
using PlayStead.Core.Scanning;
using PlayStead.Core.ProviderActivity;
using PlayStead.Data.Database;
using PlayStead.Data.Library;
using PlayStead.Platform.SingleInstance;
using PlayStead.UI.Bootstrap;
using PlayStead.UI.Library;
using PlayStead.UI.SingleInstance;

namespace PlayStead.UI.Tests.Bootstrap;

public sealed class ApplicationRuntimeTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "PlayStead.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Initialize_exposes_cached_library_before_fresh_scan_then_refresh_replaces_it()
    {
        Directory.CreateDirectory(_root);

        var options = new DatabaseOptions(
            Path.Combine(_root, "playstead.db"),
            Path.Combine(_root, "Backups"));

        var initializer = new DatabaseInitializer(options);
        await initializer.InitializeAsync(CancellationToken.None);

        ILibraryStore store = new SqliteLibraryStore(options);

        var cachedObserved = new DateTimeOffset(
            2026, 9, 12, 8, 0, 0, TimeSpan.Zero);

        await store.ApplySourceScanAsync(
            SourceScanResult.Success(
                ProviderKind.Steam,
                cachedObserved,
                [
                    DiscoveredInstallation.Create(
                        ProviderKind.Steam,
                        "111",
                        "Cached Game",
                        @"G:\Games\Cached",
                        10,
                        cachedObserved)
                ]),
            CancellationToken.None);

        var freshObserved = cachedObserved.AddHours(1);

        var source = new StubSource(
            SourceScanResult.Success(
                ProviderKind.Steam,
                freshObserved,
                [
                    DiscoveredInstallation.Create(
                        ProviderKind.Steam,
                        "222",
                        "Fresh Game",
                        @"G:\Games\Fresh",
                        20,
                        freshObserved)
                ]));

        var pipeline = new LocalStartupPipeline(
            initializer,
            new DatabaseHealthChecker(options),
            store,
            new LocalScanCoordinator([source]));

        var viewModel = new LibraryViewModel(store);
        var handler = new RecordingInvocationHandler();

        var sut = new ApplicationRuntime(
            pipeline,
            viewModel,
            handler);

        var health = await sut.InitializeAsync(
            CancellationToken.None);

        Assert.True(health.IsHealthy);

        var cached = Assert.Single(viewModel.Items);
        Assert.Equal("Cached Game", cached.Title);
        Assert.Equal(0, source.ScanCount);

        await sut.RefreshAsync(CancellationToken.None);

        var fresh = Assert.Single(viewModel.Items);
        Assert.Equal("Fresh Game", fresh.Title);
        Assert.Equal(1, source.ScanCount);
    }

    [Fact]
    public async Task HandleInvocation_delegates_exactly_once()
    {
        Directory.CreateDirectory(_root);

        var options = new DatabaseOptions(
            Path.Combine(_root, "playstead.db"),
            Path.Combine(_root, "Backups"));

        var initializer = new DatabaseInitializer(options);
        ILibraryStore store = new SqliteLibraryStore(options);

        var pipeline = new LocalStartupPipeline(
            initializer,
            new DatabaseHealthChecker(options),
            store,
            new LocalScanCoordinator(Array.Empty<ILocalLibrarySource>()));

        var handler = new RecordingInvocationHandler();

        var sut = new ApplicationRuntime(
            pipeline,
            new LibraryViewModel(store),
            handler);

        var invocation = new AppInvocation(
            Activate: true,
            DeepLink: null);

        await sut.HandleInvocationAsync(
            invocation,
            CancellationToken.None);

        Assert.Equal(1, handler.Count);
        Assert.Equal(invocation, handler.LastInvocation);
    }

    [Fact]
    public async Task Initialize_and_refresh_reconcile_provider_activity()
    {
        Directory.CreateDirectory(_root);
        var options = new DatabaseOptions(
            Path.Combine(_root, "playstead.db"),
            Path.Combine(_root, "Backups"));
        var initializer = new DatabaseInitializer(options);
        await initializer.InitializeAsync(CancellationToken.None);
        ILibraryStore library = new SqliteLibraryStore(options);
        var observed = DateTimeOffset.UtcNow;
        await library.ApplySourceScanAsync(SourceScanResult.Success(
            ProviderKind.Steam,
            observed,
            [DiscoveredInstallation.Create(ProviderKind.Steam, "42", "Game", "G:\\Game", null, observed)]),
            CancellationToken.None);

        var activityStore = new MemoryActivityStore();
        var source = new RecordingActivitySource();
        var reconciliation = new ProviderActivityReconciliationService(activityStore, [source]);
        var runtime = new ApplicationRuntime(
            new LocalStartupPipeline(initializer, new DatabaseHealthChecker(options), library,
                new LocalScanCoordinator(Array.Empty<ILocalLibrarySource>())),
            new LibraryViewModel(library),
            new RecordingInvocationHandler(),
            reconciliation,
            library);

        await runtime.InitializeAsync(CancellationToken.None);
        Assert.Equal(1, source.Calls);
        Assert.Single(await activityStore.GetAllAsync(CancellationToken.None));

        await runtime.RefreshAsync(CancellationToken.None);
        Assert.Equal(2, source.Calls);
    }

    private sealed class StubSource(
        SourceScanResult result) : ILocalLibrarySource
    {
        public ProviderKind Provider => result.Provider;

        public int ScanCount { get; private set; }

        public Task<SourceScanResult> ScanAsync(
            CancellationToken cancellationToken)
        {
            ScanCount++;
            return Task.FromResult(result);
        }
    }

    private sealed class RecordingInvocationHandler : IAppInvocationHandler
    {
        public int Count { get; private set; }

        public AppInvocation? LastInvocation { get; private set; }

        public Task HandleAsync(
            AppInvocation invocation,
            CancellationToken cancellationToken)
        {
            Count++;
            LastInvocation = invocation;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingActivitySource : IProviderActivityMetadataSource
    {
        public ProviderKind Provider => ProviderKind.Steam;
        public int Calls { get; private set; }
        public Task<IReadOnlyList<ProviderActivityMetadata>> GetAsync(
            IReadOnlyCollection<GameInstallation> installations,
            CancellationToken cancellationToken)
        {
            Calls++;
            var installation = Assert.Single(installations);
            return Task.FromResult<IReadOnlyList<ProviderActivityMetadata>>([
                new ProviderActivityMetadata(installation.GameId, ProviderKind.Steam, installation.ExternalId,
                    null, DateTimeOffset.UtcNow.AddDays(-60), DateTimeOffset.UtcNow,
                    ProviderActivityAvailability.Complete)]);
        }
    }

    private sealed class MemoryActivityStore : IProviderActivityMetadataStore
    {
        private readonly List<ProviderActivityMetadata> _values = [];
        public Task<IReadOnlyList<ProviderActivityMetadata>> GetAllAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ProviderActivityMetadata>>(_values.ToArray());
        public Task UpsertAsync(ProviderActivityMetadata metadata, CancellationToken cancellationToken)
        {
            _values.RemoveAll(value => value.GameId == metadata.GameId && value.Provider == metadata.Provider);
            _values.Add(metadata);
            return Task.CompletedTask;
        }
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();

        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
