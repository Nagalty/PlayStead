using PlayStead.Core.Library;
using PlayStead.Core.Media;
using PlayStead.Core.Persistence;
using PlayStead.Core.Scanning;
using PlayStead.Core.Sessions;
using PlayStead.Core.Steam;
using PlayStead.UI.Library;
using PlayStead.UI.Sessions;
using PlayStead.UI.Steam;

namespace PlayStead.UI.Tests.Library;

public sealed class LibraryMediaConcurrencyTests
{
    [Fact]
    public async Task Concurrent_requests_for_same_game_share_one_in_flight_resolution()
    {
        var resolver = new BlockingResolver();
        var viewModel = CreateLibrary(
            resolver,
            gameCount: 1);

        var item = Assert.Single(
            viewModel.Items);

        Task first = Task.CompletedTask;
        Task second = Task.CompletedTask;

        try
        {
            first =
                viewModel.EnsureCoverAsync(
                    item,
                    CancellationToken.None);

            await resolver.FirstCallStarted;

            second =
                viewModel.EnsureCoverAsync(
                    item,
                    CancellationToken.None);

            Assert.Equal(
                1,
                resolver.Calls);
        }
        finally
        {
            resolver.Release();

            await Task.WhenAll(
                first,
                second);
        }

        Assert.True(
            item.HasCover);

        Assert.Equal(
            BlockingResolver.Path,
            item.CoverPath);
    }

    [Fact]
    public async Task Remote_cover_resolution_never_exceeds_four_concurrent_operations()
    {
        var resolver = new BlockingResolver();
        var viewModel = CreateLibrary(
            resolver,
            gameCount: 8);

        var tasks =
            viewModel.Items
                .Select(
                    item =>
                        viewModel.EnsureCoverAsync(
                            item,
                            CancellationToken.None))
                .ToArray();

        try
        {
            Assert.True(
                resolver.MaxConcurrent <= 4,
                $"Expected at most 4 concurrent remote resolutions, observed {resolver.MaxConcurrent}.");
        }
        finally
        {
            resolver.Release();

            await Task.WhenAll(
                tasks);
        }

        Assert.Equal(
            8,
            resolver.Calls);

        Assert.Equal(
            4,
            resolver.MaxConcurrent);
    }

    private static LibraryViewModel CreateLibrary(
        BlockingResolver resolver,
        int gameCount)
    {
        var now =
            new DateTimeOffset(
                2026,
                9,
                15,
                8,
                30,
                0,
                TimeSpan.Zero);

        var games =
            new List<LogicalGame>();

        var installations =
            new List<GameInstallation>();

        for (var index = 0;
             index < gameCount;
             index++)
        {
            var gameId =
                GameId.New();

            games.Add(
                new LogicalGame(
                    gameId,
                    $"Game {index + 1}",
                    IsHidden: false,
                    CreatedAtUtc: now,
                    UpdatedAtUtc: now));

            installations.Add(
                new GameInstallation(
                    InstallationId.New(),
                    gameId,
                    ProviderKind.Steam,
                    (1_000_000 + index).ToString(),
                    $@"C:\Games\Game{index + 1}",
                    InstalledSizeBytes: null,
                    IsPreferred: true,
                    IsPresent: true,
                    LastSeenUtc: now));
        }

        var snapshot =
            new LibrarySnapshot(
                games,
                installations);

        var viewModel =
            new LibraryViewModel(
                new StubLibraryStore(
                    snapshot),
                new RecordingSteamReferenceRuntime(),
                new SessionMonitor(
                    new FakeSessionRuntime(
                        now),
                    SessionMonitorOptions.Default),
                resolver);

        viewModel
            .RefreshAsync(
                CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        Assert.Equal(
            gameCount,
            viewModel.Items.Count);

        Assert.Equal(
            0,
            resolver.Calls);

        return viewModel;
    }

    private sealed class BlockingResolver :
        IGameMediaResolver
    {
        public const string Path =
            @"C:\Media\cover.jpg";

        private readonly TaskCompletionSource _release =
            new(
                TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource _firstCallStarted =
            new(
                TaskCreationOptions.RunContinuationsAsynchronously);

        private int _calls;
        private int _active;
        private int _maxConcurrent;

        public int Calls =>
            Volatile.Read(
                ref _calls);

        public int MaxConcurrent =>
            Volatile.Read(
                ref _maxConcurrent);

        public Task FirstCallStarted =>
            _firstCallStarted.Task;

        public string? TryGetCachedPath(
            GameMediaIdentity identity,
            GameMediaAssetType assetType) =>
            null;

        public async Task<string?> ResolveAndCacheAsync(
            GameMediaIdentity identity,
            GameMediaAssetType assetType,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(
                ref _calls);

            var active =
                Interlocked.Increment(
                    ref _active);

            UpdateMaxConcurrent(
                active);

            _firstCallStarted.TrySetResult();

            try
            {
                await _release.Task.WaitAsync(
                    cancellationToken);

                return Path;
            }
            finally
            {
                Interlocked.Decrement(
                    ref _active);
            }
        }

        public void Release() =>
            _release.TrySetResult();

        private void UpdateMaxConcurrent(
            int candidate)
        {
            while (true)
            {
                var current =
                    Volatile.Read(
                        ref _maxConcurrent);

                if (candidate <= current)
                {
                    return;
                }

                if (Interlocked.CompareExchange(
                        ref _maxConcurrent,
                        candidate,
                        current) == current)
                {
                    return;
                }
            }
        }
    }

    private sealed class RecordingSteamReferenceRuntime :
        ISteamReferenceRuntime
    {
        private static readonly SteamReferenceSnapshot Empty =
            new(
                Array.Empty<SteamReferenceEntry>());

        public SteamReferenceSnapshot Current { get; private set; } =
            Empty;

        public Task<SteamReferenceSnapshot> LoadCachedAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Current = Empty;
            return Task.FromResult(Current);
        }

        public Task<SteamReferenceSnapshot> RefreshStaleAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Current = Empty;
            return Task.FromResult(Current);
        }

        public Task<SteamReferenceSnapshot> RefreshAllAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Current = Empty;
            return Task.FromResult(Current);
        }
    }

    private sealed class FakeSessionRuntime(
        DateTimeOffset observedAtUtc) :
        ISessionRuntime
    {
        public Task<SessionRuntimeSnapshot> RefreshAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(
                new SessionRuntimeSnapshot(
                    observedAtUtc,
                    []));
        }

        public Task CorrectSessionAsync(
            SessionCorrectionRequest correction,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            throw new InvalidOperationException(
                "Library media concurrency tests must not correct sessions.");
        }
    }

    private sealed class StubLibraryStore(
        LibrarySnapshot snapshot) :
        ILibraryStore
    {
        public Task ApplySourceScanAsync(
            SourceScanResult result,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<LibrarySnapshot> LoadSnapshotAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(
                snapshot);
        }
    }
}
