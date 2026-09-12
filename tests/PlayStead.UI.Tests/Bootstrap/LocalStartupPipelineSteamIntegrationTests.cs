using PlayStead.Core.Library;
using PlayStead.Core.Persistence;
using PlayStead.Core.Scanning;
using PlayStead.Data.Database;
using PlayStead.Data.Library;
using PlayStead.UI.Bootstrap;
using PlayStead.UI.Steam;

namespace PlayStead.UI.Tests.Bootstrap;

public sealed class LocalStartupPipelineSteamIntegrationTests :
    IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "PlayStead.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Initialize_loads_Steam_reference_from_cache_without_refreshing_it()
    {
        var context = CreateContext();

        var state = await context.Pipeline.InitializeAsync(
            CancellationToken.None);

        Assert.True(state.Health.IsHealthy);
        Assert.Equal(1, context.SteamRuntime.LoadCachedCount);
        Assert.Equal(0, context.SteamRuntime.RefreshStaleCount);
        Assert.Equal(0, context.SteamRuntime.RefreshAllCount);
    }

    [Fact]
    public async Task Background_local_refresh_also_runs_stale_only_Steam_reference_refresh()
    {
        var context = CreateContext();

        await context.Pipeline.InitializeAsync(
            CancellationToken.None);

        await context.Pipeline.RefreshAsync(
            CancellationToken.None);

        Assert.Equal(1, context.SteamRuntime.LoadCachedCount);
        Assert.Equal(1, context.SteamRuntime.RefreshStaleCount);
        Assert.Equal(0, context.SteamRuntime.RefreshAllCount);
    }

    private Context CreateContext()
    {
        Directory.CreateDirectory(_root);

        var options =
            new DatabaseOptions(
                Path.Combine(_root, "playstead.db"),
                Path.Combine(_root, "Backups"));

        ILibraryStore store =
            new SqliteLibraryStore(options);

        var steamRuntime =
            new RecordingSteamReferenceRuntime();

        var pipeline =
            new LocalStartupPipeline(
                new DatabaseInitializer(options),
                new DatabaseHealthChecker(options),
                store,
                new LocalScanCoordinator(
                    Array.Empty<ILocalLibrarySource>()),
                steamRuntime);

        return new Context(
            pipeline,
            steamRuntime);
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        if (Directory.Exists(_root))
        {
            Directory.Delete(
                _root,
                recursive: true);
        }
    }

    private sealed record Context(
        LocalStartupPipeline Pipeline,
        RecordingSteamReferenceRuntime SteamRuntime);

    private sealed class RecordingSteamReferenceRuntime :
        ISteamReferenceRuntime
    {
        private static readonly SteamReferenceSnapshot Empty =
            new(Array.Empty<SteamReferenceEntry>());

        public int LoadCachedCount { get; private set; }
        public int RefreshStaleCount { get; private set; }
        public int RefreshAllCount { get; private set; }

        public SteamReferenceSnapshot Current { get; private set; } =
            Empty;

        public Task<SteamReferenceSnapshot> LoadCachedAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LoadCachedCount++;
            Current = Empty;
            return Task.FromResult(Current);
        }

        public Task<SteamReferenceSnapshot> RefreshStaleAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RefreshStaleCount++;
            Current = Empty;
            return Task.FromResult(Current);
        }

        public Task<SteamReferenceSnapshot> RefreshAllAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RefreshAllCount++;
            Current = Empty;
            return Task.FromResult(Current);
        }
    }
}
