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

public sealed class LibraryMediaResolverIntegrationTests
{
    [Fact]
    public async Task RefreshAsync_applies_cached_Steam_cover_using_exact_external_id_without_remote_resolution()
    {
        var now = new DateTimeOffset(
            2026, 9, 15, 0, 30, 0, TimeSpan.Zero);

        var gameId = GameId.New();

        var snapshot = new LibrarySnapshot(
            [
                new LogicalGame(
                    gameId,
                    "Arma Reforger",
                    IsHidden: false,
                    CreatedAtUtc: now,
                    UpdatedAtUtc: now)
            ],
            [
                new GameInstallation(
                    InstallationId.New(),
                    gameId,
                    ProviderKind.Steam,
                    "1874880",
                    @"G:\SteamLibrary\steamapps\common\Arma Reforger",
                    42_000_000_000,
                    IsPreferred: true,
                    IsPresent: true,
                    LastSeenUtc: now)
            ]);

        var resolver = new RecordingGameMediaResolver(
            @"C:\Media\steam\1874880\cover.jpg");

        var viewModel = new LibraryViewModel(
            new StubLibraryStore(snapshot),
            new RecordingSteamReferenceRuntime(),
            new SessionMonitor(
                new FakeSessionRuntime(now),
                SessionMonitorOptions.Default),
            resolver);

        await viewModel.RefreshAsync(
            CancellationToken.None);

        var item = Assert.Single(
            viewModel.Items);

        Assert.Equal(
            @"C:\Media\steam\1874880\cover.jpg",
            item.CoverPath);

        Assert.Equal(2, resolver.TryGetCachedPathCalls);
        Assert.Equal(0, resolver.ResolveAndCacheCalls);

        Assert.NotNull(resolver.LastIdentity);
        Assert.Equal(
            ProviderKind.Steam,
            resolver.LastIdentity.Provider);
        Assert.Equal(
            "1874880",
            resolver.LastIdentity.ProviderGameId);
        Assert.Equal(
            "Arma Reforger",
            resolver.LastIdentity.CanonicalTitle);
        Assert.Equal(
            2,
            resolver.CachedAssetTypes.Count);
        Assert.Contains(
            GameMediaAssetType.Cover,
            resolver.CachedAssetTypes);
        Assert.Contains(
            GameMediaAssetType.Logo,
            resolver.CachedAssetTypes);
    }

    [Fact]
    public async Task EnsureCoverAsync_resolves_exact_Steam_identity_and_applies_returned_cover()
    {
        var resolver = new RecordingGameMediaResolver(null)
        {
            ResolvedPath = @"C:\Media\steam\1874880\cover.jpg"
        };
        var (viewModel, item) = await CreateLoadedLibraryAsync(resolver);
        using var cancellation = new CancellationTokenSource();

        await viewModel.EnsureCoverAsync(item, cancellation.Token);

        Assert.Equal(1, resolver.ResolveAndCacheCalls);
        Assert.NotNull(resolver.LastResolvedIdentity);
        Assert.Equal(ProviderKind.Steam, resolver.LastResolvedIdentity.Provider);
        Assert.Equal("1874880", resolver.LastResolvedIdentity.ProviderGameId);
        Assert.Equal("Arma Reforger", resolver.LastResolvedIdentity.CanonicalTitle);
        Assert.Equal(GameMediaAssetType.Cover, resolver.LastResolvedAssetType);
        Assert.Equal(cancellation.Token, resolver.LastResolveToken);
        Assert.Equal(resolver.ResolvedPath, item.CoverPath);
        Assert.True(item.HasCover);
    }

    [Fact]
    public async Task EnsureCoverAsync_keeps_existing_cover_without_resolving()
    {
        var resolver = new RecordingGameMediaResolver(null);
        var (viewModel, item) = await CreateLoadedLibraryAsync(resolver);
        item.SetCoverPath(@"C:\Media\existing.jpg");

        await viewModel.EnsureCoverAsync(item, CancellationToken.None);

        Assert.Equal(0, resolver.ResolveAndCacheCalls);
        Assert.Equal(@"C:\Media\existing.jpg", item.CoverPath);
    }

    [Fact]
    public async Task EnsureCoverAsync_allows_manual_identity_bridge_to_resolve()
    {
        var resolver = new RecordingGameMediaResolver(null)
        {
            ResolvedPath = @"C:\Media\manual\cover.jpg"
        };
        var (viewModel, item) = await CreateLoadedLibraryAsync(resolver, ProviderKind.Manual);

        await viewModel.EnsureCoverAsync(item, CancellationToken.None);

        Assert.Equal(1, resolver.ResolveAndCacheCalls);
        Assert.Equal(resolver.ResolvedPath, item.CoverPath);
        Assert.True(item.HasCover);
        Assert.Equal(ProviderKind.Manual, resolver.LastResolvedIdentity?.Provider);
        Assert.Equal($"manual:{item.GameId.Value:D}", resolver.LastResolvedIdentity?.ProviderGameId);
    }

    [Fact]
    public async Task EnsureCoverAsync_resolves_exact_Gog_product_identity()
    {
        var resolver = new RecordingGameMediaResolver(null)
        {
            ResolvedPath = @"C:\Media\gog\1495134320\cover.webp"
        };
        var (viewModel, item) = await CreateLoadedLibraryAsync(resolver, ProviderKind.Gog);

        await viewModel.EnsureCoverAsync(item, CancellationToken.None);

        Assert.Equal(resolver.ResolvedPath, item.CoverPath);
        Assert.Equal(ProviderKind.Gog, resolver.LastResolvedIdentity?.Provider);
        Assert.Equal("1495134320", resolver.LastResolvedIdentity?.ProviderGameId);
    }

    [Fact]
    public async Task EnsureLogoAsync_resolves_exact_Gog_product_identity()
    {
        var resolver = new RecordingGameMediaResolver(null)
        {
            ResolvedPath = @"C:\Media\gog\1495134320\logo.webp"
        };
        var (viewModel, item) = await CreateLoadedLibraryAsync(resolver, ProviderKind.Gog);

        await viewModel.EnsureLogoAsync(item, CancellationToken.None);

        Assert.Equal(resolver.ResolvedPath, item.LogoPath);
        Assert.True(item.HasLogo);
        Assert.Equal(ProviderKind.Gog, resolver.LastResolvedIdentity?.Provider);
        Assert.Equal("1495134320", resolver.LastResolvedIdentity?.ProviderGameId);
        Assert.Equal(GameMediaAssetType.Logo, resolver.LastResolvedAssetType);
    }

    [Fact]
    public async Task EnsureCoverAsync_keeps_fallback_when_resolver_returns_null()
    {
        var resolver = new RecordingGameMediaResolver(null);
        var (viewModel, item) = await CreateLoadedLibraryAsync(resolver);

        await viewModel.EnsureCoverAsync(item, CancellationToken.None);

        Assert.Equal(1, resolver.ResolveAndCacheCalls);
        Assert.Null(item.CoverPath);
        Assert.False(item.HasCover);
    }

    [Fact]
    public async Task EnsureCoverAsync_propagates_in_flight_cancellation_without_clearing_cover()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resolver = new RecordingGameMediaResolver(null)
        {
            ResolveOverride = async token =>
            {
                started.SetResult();
                await Task.Delay(Timeout.Infinite, token);
                return null;
            }
        };
        var (viewModel, item) = await CreateLoadedLibraryAsync(resolver);
        using var cancellation = new CancellationTokenSource();

        var pending = viewModel.EnsureCoverAsync(item, cancellation.Token);
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            // A local cover can become available while the asynchronous request is pending.
            item.SetCoverPath(@"C:\Media\existing.jpg");
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
            Assert.Equal(1, resolver.ResolveAndCacheCalls);
            Assert.Equal(cancellation.Token, resolver.LastResolveToken);
            Assert.Equal(@"C:\Media\existing.jpg", item.CoverPath);
        }
        finally
        {
            cancellation.Cancel();
        }
    }

    [Fact]
    public async Task EnsureCoverAsync_propagates_pre_cancelled_token_and_preserves_existing_cover()
    {
        var resolver = new RecordingGameMediaResolver(null);
        var (viewModel, item) = await CreateLoadedLibraryAsync(resolver);
        item.SetCoverPath(@"C:\Media\existing.jpg");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => viewModel.EnsureCoverAsync(item, cancellation.Token));

        Assert.Equal(0, resolver.ResolveAndCacheCalls);
        Assert.Equal(@"C:\Media\existing.jpg", item.CoverPath);
    }

    private static async Task<(LibraryViewModel ViewModel, LibraryItemViewModel Item)> CreateLoadedLibraryAsync(
        RecordingGameMediaResolver resolver,
        ProviderKind provider = ProviderKind.Steam)
    {
        var now = new DateTimeOffset(2026, 9, 15, 0, 30, 0, TimeSpan.Zero);
        var gameId = GameId.New();
        var title = provider == ProviderKind.Gog
            ? "The Witcher 3: Wild Hunt - Game of the Year Edition"
            : "Arma Reforger";
        var externalId = provider == ProviderKind.Gog ? "1495134320" : "1874880";
        var snapshot = new LibrarySnapshot(
            [new LogicalGame(gameId, title, false, now, now)],
            [
                new GameInstallation(InstallationId.New(), GameId.New(), ProviderKind.Steam,
                    "999", @"C:\Games\Other", null, true, true, now),
                new GameInstallation(InstallationId.New(), gameId, provider,
                    externalId, provider == ProviderKind.Gog
                        ? @"G:\Gog Games\The Witcher 3 Wild Hunt GOTY"
                        : @"G:\SteamLibrary\steamapps\common\Arma Reforger",
                    null, true, true, now)
            ]);
        var viewModel = new LibraryViewModel(
            new StubLibraryStore(snapshot),
            new RecordingSteamReferenceRuntime(),
            new SessionMonitor(new FakeSessionRuntime(now), SessionMonitorOptions.Default),
            resolver);

        // Populate the existing installation snapshot through its real public refresh path.
        await viewModel.RefreshAsync(CancellationToken.None);
        if (provider == ProviderKind.Steam)
            Assert.Equal(0, resolver.ResolveAndCacheCalls);
        return (viewModel, Assert.Single(viewModel.Items));
    }

    [Fact]
    public async Task Canonical_logo_can_come_from_linked_Steam_while_GOG_installation_remains_active()
    {
        var now = new DateTimeOffset(2026, 9, 15, 0, 30, 0, TimeSpan.Zero);
        var gameId = GameId.New();
        var snapshot = new LibrarySnapshot(
            [new LogicalGame(gameId, "Synthetic GOG Game", false, now, now)],
            [new GameInstallation(
                InstallationId.New(),
                gameId,
                ProviderKind.Gog,
                "GOG-TEST",
                @"G:\Gog Games\Synthetic",
                null,
                true,
                true,
                now)]);
        var canonical = new RecordingCanonicalMediaResolver(@"C:\Media\steam\456\logo.png");
        var viewModel = new LibraryViewModel(
            new StubLibraryStore(snapshot),
            new RecordingSteamReferenceRuntime(),
            new SessionMonitor(new FakeSessionRuntime(now), SessionMonitorOptions.Default),
            new RecordingGameMediaResolver(null),
            canonical);

        await viewModel.RefreshAsync(CancellationToken.None);
        var item = Assert.Single(viewModel.Items);
        await viewModel.EnsureLogoAsync(item, CancellationToken.None);

        Assert.Equal(@"C:\Media\steam\456\logo.png", item.LogoPath);
        Assert.Equal(ProviderKind.Gog, item.Provider);
        Assert.Equal(@"G:\Gog Games\Synthetic", item.InstallPath);
        Assert.Equal(ProviderKind.Gog, canonical.LastActiveProvider);
    }

    [Fact]
    public async Task Canonical_logo_replaces_an_invalid_active_provider_cache_candidate()
    {
        var now = new DateTimeOffset(2026, 9, 15, 0, 30, 0, TimeSpan.Zero);
        var gameId = GameId.New();
        var snapshot = new LibrarySnapshot(
            [new LogicalGame(gameId, "Synthetic GOG Game", false, now, now)],
            [new GameInstallation(InstallationId.New(), gameId, ProviderKind.Gog,
                "1103900211", @"G:\Gog Games\Synthetic", null, true, true, now)]);
        var activeResolver = new RecordingGameMediaResolver(@"C:\Media\gog\1103900211\logo.webp");
        var canonical = new RecordingCanonicalMediaResolver(@"C:\Media\steam\1715130\logo.png");
        var viewModel = new LibraryViewModel(
            new StubLibraryStore(snapshot),
            new RecordingSteamReferenceRuntime(),
            new SessionMonitor(new FakeSessionRuntime(now), SessionMonitorOptions.Default),
            activeResolver,
            canonical);

        await viewModel.RefreshAsync(CancellationToken.None);
        var item = Assert.Single(viewModel.Items);
        Assert.Null(item.LogoPath);
        await viewModel.EnsureLogoAsync(item, CancellationToken.None);

        Assert.Equal(@"C:\Media\steam\1715130\logo.png", item.LogoPath);
        Assert.Equal(1, canonical.ResolveCalls);
    }

    [Fact]
    public async Task Canonical_null_clears_an_invalid_active_provider_cache_candidate()
    {
        var now = new DateTimeOffset(2026, 9, 15, 0, 30, 0, TimeSpan.Zero);
        var gameId = GameId.New();
        var snapshot = new LibrarySnapshot(
            [new LogicalGame(gameId, "Synthetic GOG Game", false, now, now)],
            [new GameInstallation(InstallationId.New(), gameId, ProviderKind.Gog,
                "1103900211", @"G:\Gog Games\Synthetic", null, true, true, now)]);
        var activeResolver = new RecordingGameMediaResolver(@"C:\Media\gog\1103900211\logo.webp");
        var canonical = new RecordingCanonicalMediaResolver(null);
        var viewModel = new LibraryViewModel(
            new StubLibraryStore(snapshot),
            new RecordingSteamReferenceRuntime(),
            new SessionMonitor(new FakeSessionRuntime(now), SessionMonitorOptions.Default),
            activeResolver,
            canonical);

        await viewModel.RefreshAsync(CancellationToken.None);
        var item = Assert.Single(viewModel.Items);
        await viewModel.EnsureLogoAsync(item, CancellationToken.None);

        Assert.Null(item.LogoPath);
        Assert.Equal(1, canonical.ResolveCalls);
    }

    private sealed class RecordingGameMediaResolver(
        string? cachedPath) : IGameMediaResolver
    {
        public int TryGetCachedPathCalls { get; private set; }
        public int ResolveAndCacheCalls { get; private set; }

        public List<GameMediaAssetType> CachedAssetTypes { get; } = [];

        public GameMediaIdentity? LastIdentity { get; private set; }
        public GameMediaAssetType? LastAssetType { get; private set; }
        public GameMediaIdentity? LastResolvedIdentity { get; private set; }
        public GameMediaAssetType? LastResolvedAssetType { get; private set; }
        public CancellationToken LastResolveToken { get; private set; }
        public string? ResolvedPath { get; init; }
        public Func<CancellationToken, Task<string?>>? ResolveOverride { get; init; }

        public string? TryGetCachedPath(
            GameMediaIdentity identity,
            GameMediaAssetType assetType)
        {
            TryGetCachedPathCalls++;
            CachedAssetTypes.Add(assetType);
            LastIdentity = identity;
            LastAssetType = assetType;

            return assetType == GameMediaAssetType.Cover
                ? cachedPath
                : null;
        }

        public Task<string?> ResolveAndCacheAsync(
            GameMediaIdentity identity,
            GameMediaAssetType assetType,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ResolveAndCacheCalls++;
            LastResolvedIdentity = identity;
            LastResolvedAssetType = assetType;
            LastResolveToken = cancellationToken;

            return ResolveOverride is not null
                ? ResolveOverride(cancellationToken)
                : Task.FromResult(ResolvedPath);
        }
    }

    private sealed class RecordingCanonicalMediaResolver(string? path) : ICanonicalGameMediaResolver
    {
        public ProviderKind? LastActiveProvider { get; private set; }
        public int ResolveCalls { get; private set; }

        public string? TryGetCachedPath(GameId gameId, GameMediaAssetType assetType) => null;

        public Task<string?> ResolveAndCacheAsync(
            GameId gameId,
            string canonicalTitle,
            GameMediaAssetType assetType,
            ProviderKind? activeProvider,
            CancellationToken cancellationToken)
        {
            ResolveCalls++;
            LastActiveProvider = activeProvider;
            return Task.FromResult(path);
        }
    }

    private sealed class RecordingSteamReferenceRuntime :
        ISteamReferenceRuntime
    {
        private static readonly SteamReferenceSnapshot Empty =
            new(Array.Empty<SteamReferenceEntry>());

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
        DateTimeOffset observedAtUtc) : ISessionRuntime
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
                "Library media tests must not correct sessions.");
        }
    }

    private sealed class StubLibraryStore(
        LibrarySnapshot snapshot) : ILibraryStore
    {
        public Task ApplySourceScanAsync(
            SourceScanResult result,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<LibrarySnapshot> LoadSnapshotAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(snapshot);
        }
    }
}
