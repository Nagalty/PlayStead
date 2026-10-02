using Microsoft.Data.Sqlite;
using PlayStead.Core.Library;
using PlayStead.Core.Persistence;
using PlayStead.Core.Scanning;
using PlayStead.Core.ProviderActivity;
using PlayStead.Core.ProviderGameMetadata;
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

    [Fact]
    public async Task Post_ready_enrichment_runs_off_caller_thread_and_is_single_flight()
    {
        Directory.CreateDirectory(_root);
        var options = new DatabaseOptions(
            Path.Combine(_root, "playstead.db"),
            Path.Combine(_root, "Backups"));
        var initializer = new DatabaseInitializer(options);
        await initializer.InitializeAsync(CancellationToken.None);
        var library = new SqliteLibraryStore(options);
        var pipeline = new LocalStartupPipeline(
            initializer,
            new DatabaseHealthChecker(options),
            library,
            new LocalScanCoordinator(Array.Empty<ILocalLibrarySource>()));
        var source = new BlockingMetadataSource();
        var metadataStore = new MemoryMetadataStore();
        var online = new ProviderGameMetadataOnlineReconciliationService(metadataStore, [source]);
        var progress = new List<ProviderGameMetadataProgress>();
        online.ProgressChanged += (_, value) => progress.Add(value);
        var runtime = new ApplicationRuntime(
            pipeline,
            new LibraryViewModel(library),
            new RecordingInvocationHandler(),
            new ProviderActivityReconciliationService(new MemoryActivityStore(), Array.Empty<IProviderActivityMetadataSource>()),
            library,
            new ProviderGameMetadataReconciliationService(metadataStore, Array.Empty<IProviderGameMetadataSource>()),
            online);

        var callerThread = Environment.CurrentManagedThreadId;
        runtime.StartPostReadyEnrichment(CancellationToken.None);
        runtime.StartPostReadyEnrichment(CancellationToken.None);

        await source.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.NotEqual(callerThread, source.WorkerThreadId);
        Assert.Equal(1, source.CallCount);
        Assert.False(source.Completed.Task.IsCompleted);

        source.Gate.TrySetResult(true);
        await source.Completed.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Contains(progress, value => value.Total == 25 && value.Completed == 0);
        Assert.Contains(progress, value => value.Total == 25 && value.Completed == 8);
        Assert.Contains(progress, value => value.Total == 25 && value.Completed == 25);
    }

    [Fact]
    public async Task Post_ready_cancellation_stops_background_work_without_user_exception()
    {
        Directory.CreateDirectory(_root);
        var options = new DatabaseOptions(Path.Combine(_root, "playstead.db"), Path.Combine(_root, "Backups"));
        var initializer = new DatabaseInitializer(options);
        await initializer.InitializeAsync(CancellationToken.None);
        var library = new SqliteLibraryStore(options);
        var pipeline = new LocalStartupPipeline(initializer, new DatabaseHealthChecker(options), library,
            new LocalScanCoordinator(Array.Empty<ILocalLibrarySource>()));
        var source = new BlockingMetadataSource();
        var metadataStore = new MemoryMetadataStore();
        var online = new ProviderGameMetadataOnlineReconciliationService(metadataStore, [source]);
        var runtime = new ApplicationRuntime(
            pipeline,
            new LibraryViewModel(library),
            new RecordingInvocationHandler(),
            new ProviderActivityReconciliationService(new MemoryActivityStore(), Array.Empty<IProviderActivityMetadataSource>()),
            library,
            new ProviderGameMetadataReconciliationService(metadataStore, Array.Empty<IProviderGameMetadataSource>()),
            online);
        using var cancellation = new CancellationTokenSource();

        runtime.StartPostReadyEnrichment(cancellation.Token);
        await source.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        cancellation.Cancel();

        await source.Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(2));
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

    private sealed class BlockingMetadataSource : IProviderGameMetadataSource, IProviderGameMetadataProgress
    {
        public ProviderKind Provider => ProviderKind.Steam;
        public TaskCompletionSource<bool> Started { get; } = NewSignal();
        public TaskCompletionSource<bool> Gate { get; } = NewSignal();
        public TaskCompletionSource<bool> Completed { get; } = NewSignal();
        public TaskCompletionSource<bool> Cancelled { get; } = NewSignal();
        public int CallCount { get; private set; }
        public int WorkerThreadId { get; private set; }
        public ProviderGameMetadataProgress Current { get; private set; } = new(false, 0, 0, 0, 0);
        public event EventHandler<ProviderGameMetadataProgress>? ProgressChanged;

        public async Task<IReadOnlyList<ProviderGameMetadataPatch>> GetAsync(
            LibrarySnapshot snapshot,
            CancellationToken cancellationToken)
        {
            CallCount++;
            WorkerThreadId = Environment.CurrentManagedThreadId;
            Publish(new ProviderGameMetadataProgress(true, 25, 0, 0, 0));
            Started.TrySetResult(true);
            try
            {
                await Gate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                Cancelled.TrySetResult(true);
                throw;
            }
            Publish(new ProviderGameMetadataProgress(true, 25, 8, 8, 0));
            Publish(new ProviderGameMetadataProgress(false, 25, 25, 25, 0));
            Completed.TrySetResult(true);
            return [];
        }

        private void Publish(ProviderGameMetadataProgress progress)
        {
            Current = progress;
            ProgressChanged?.Invoke(this, progress);
        }

        private static TaskCompletionSource<bool> NewSignal() =>
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class MemoryMetadataStore : IProviderGameMetadataStore
    {
        private readonly List<ProviderGameMetadata> _items = [];
        public Task<IReadOnlyList<ProviderGameMetadata>> GetAllAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ProviderGameMetadata>>(_items.ToArray());
        public Task<ProviderGameMetadata?> GetAsync(GameId gameId, ProviderKind provider, CancellationToken cancellationToken) =>
            Task.FromResult(_items.FirstOrDefault(item => item.GameId == gameId && item.Provider == provider));
        public Task UpsertAsync(ProviderGameMetadata metadata, CancellationToken cancellationToken)
        {
            _items.RemoveAll(item => item.GameId == metadata.GameId && item.Provider == metadata.Provider);
            _items.Add(metadata);
            return Task.CompletedTask;
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
