using Microsoft.Extensions.DependencyInjection;
using PlayStead.Core.Identity;
using PlayStead.Core.Library;
using PlayStead.Core.Persistence;
using PlayStead.Core.Scanning;
using PlayStead.Data.Database;
using PlayStead.Platform.Paths;
using PlayStead.UI.Bootstrap;

namespace PlayStead.UI.Tests.Bootstrap;

public sealed class IdentityResolutionStartupTests : IDisposable
{
    private static readonly DateTimeOffset ObservedAt =
        DateTimeOffset.Parse("2026-09-16T18:00:00Z");

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "PlayStead.Tests",
        "IdentityStartup",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Refresh_persists_library_before_running_identity_sidecar()
    {
        var order = new List<string>();
        var result = SteamScan();
        var store = new RecordingLibraryStore(order);
        var sidecar = new RecordingSidecar(order);
        var sut = CreatePipeline(store, sidecar, result);

        await sut.RefreshAsync(CancellationToken.None);

        Assert.Equal(["persist", "sidecar", "snapshot"], order);
        Assert.Same(result, Assert.Single(sidecar.Results));
    }

    [Fact]
    public async Task Identity_sidecar_io_failure_does_not_fail_successful_refresh()
    {
        var store = new RecordingLibraryStore([]);
        var sut = CreatePipeline(
            store,
            new ThrowingSidecar(new IOException("identity failed")),
            SteamScan());

        var snapshot = await sut.RefreshAsync(CancellationToken.None);

        Assert.Same(store.Snapshot, snapshot);
        Assert.Equal(1, store.ApplyCount);
    }

    [Fact]
    public async Task Identity_sidecar_cancellation_from_caller_is_propagated()
    {
        using var cancellation = new CancellationTokenSource();
        var store = new RecordingLibraryStore([], cancellation.Cancel);
        var sut = CreatePipeline(
            store,
            new CancellationSidecar(),
            SteamScan());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => sut.RefreshAsync(cancellation.Token));
    }

    [Fact]
    public void Production_host_resolves_identity_sidecar_services()
    {
        var layout = UserDataLayout.FromRoot(_root);
        layout.EnsureDirectoriesExist();
        using var host = PlaySteadHost.Build(layout);

        Assert.NotNull(host.Services.GetRequiredService<IGameIdentityResolver>());
        Assert.NotNull(host.Services.GetRequiredService<IIdentityResolutionStore>());
        Assert.NotNull(host.Services.GetRequiredService<ILocalIdentityReconciler>());
        Assert.NotNull(host.Services.GetRequiredService<ILibraryGameLookup>());
        Assert.NotNull(host.Services.GetRequiredService<ILocalIdentityResolutionCoordinator>());
    }

    private LocalStartupPipeline CreatePipeline(
        ILibraryStore store,
        ILocalIdentityResolutionCoordinator sidecar,
        SourceScanResult result)
    {
        Directory.CreateDirectory(_root);
        var options = new DatabaseOptions(
            Path.Combine(_root, "playstead.db"),
            Path.Combine(_root, "Backups"));

        return new LocalStartupPipeline(
            new DatabaseInitializer(options),
            new DatabaseHealthChecker(options),
            store,
            new LocalScanCoordinator([new StubSource(result)]),
            sidecar);
    }

    private static SourceScanResult SteamScan() =>
        SourceScanResult.Success(
            ProviderKind.Steam,
            ObservedAt,
            [
                DiscoveredInstallation.Create(
                    ProviderKind.Steam,
                    "1874880",
                    "Arma Reforger",
                    @"G:\Games\Arma Reforger",
                    42,
                    ObservedAt)
            ]);

    private sealed class StubSource(
        SourceScanResult result) : ILocalLibrarySource
    {
        public ProviderKind Provider => result.Provider;

        public Task<SourceScanResult> ScanAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult(result);
    }

    private sealed class RecordingLibraryStore(
        List<string> order,
        Action? afterApply = null) : ILibraryStore
    {
        public LibrarySnapshot Snapshot { get; } = new([], []);
        public int ApplyCount { get; private set; }

        public Task ApplySourceScanAsync(
            SourceScanResult result,
            CancellationToken cancellationToken)
        {
            ApplyCount++;
            order.Add("persist");
            afterApply?.Invoke();
            return Task.CompletedTask;
        }

        public Task<LibrarySnapshot> LoadSnapshotAsync(
            CancellationToken cancellationToken)
        {
            order.Add("snapshot");
            return Task.FromResult(Snapshot);
        }
    }

    private sealed class RecordingSidecar(
        List<string> order) : ILocalIdentityResolutionCoordinator
    {
        public List<SourceScanResult> Results { get; } = [];

        public Task ResolveAfterScanAsync(
            SourceScanResult result,
            CancellationToken cancellationToken)
        {
            order.Add("sidecar");
            Results.Add(result);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingSidecar(
        Exception exception) : ILocalIdentityResolutionCoordinator
    {
        public Task ResolveAfterScanAsync(
            SourceScanResult result,
            CancellationToken cancellationToken) =>
            Task.FromException(exception);
    }

    private sealed class CancellationSidecar
        : ILocalIdentityResolutionCoordinator
    {
        public Task ResolveAfterScanAsync(
            SourceScanResult result,
            CancellationToken cancellationToken) =>
            Task.FromException(
                new OperationCanceledException(cancellationToken));
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
