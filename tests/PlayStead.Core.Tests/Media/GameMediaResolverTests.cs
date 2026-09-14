using PlayStead.Core.Library;
using PlayStead.Core.Media;

namespace PlayStead.Core.Tests.Media;

public sealed class GameMediaResolverTests
{
    [Fact]
    public async Task ResolveAndCacheAsync_returns_cache_without_calling_provider()
    {
        var cache = new StubCache(
            cachedPath: @"C:\Cache\steam\1874880\cover.jpg");

        var provider = new RecordingProvider();
        var resolver = new GameMediaResolver(
            cache,
            [provider]);

        var path = await resolver.ResolveAndCacheAsync(
            SteamIdentity(),
            GameMediaAssetType.Cover,
            CancellationToken.None);

        Assert.Equal(
            @"C:\Cache\steam\1874880\cover.jpg",
            path);
        Assert.Equal(0, provider.ResolveCalls);
        Assert.Equal(0, cache.StoreCalls);
    }

    [Fact]
    public async Task ResolveAndCacheAsync_cache_miss_provider_success_stores_and_returns_path()
    {
        var cache = new StubCache(
            cachedPath: null,
            storedPath: @"C:\Cache\steam\1874880\cover.jpg");

        var provider = new RecordingProvider(
            payload: ValidPayload());

        var resolver = new GameMediaResolver(
            cache,
            [provider]);

        var path = await resolver.ResolveAndCacheAsync(
            SteamIdentity(),
            GameMediaAssetType.Cover,
            CancellationToken.None);

        Assert.Equal(
            @"C:\Cache\steam\1874880\cover.jpg",
            path);
        Assert.Equal(1, provider.ResolveCalls);
        Assert.Equal(1, cache.StoreCalls);
        Assert.Same(provider.Payload, cache.LastStoredPayload);
    }

    [Fact]
    public async Task ResolveAndCacheAsync_provider_returns_null_returns_null_without_store()
    {
        var cache = new StubCache(cachedPath: null);
        var provider = new RecordingProvider(payload: null);

        var resolver = new GameMediaResolver(
            cache,
            [provider]);

        var path = await resolver.ResolveAndCacheAsync(
            SteamIdentity(),
            GameMediaAssetType.Cover,
            CancellationToken.None);

        Assert.Null(path);
        Assert.Equal(1, provider.ResolveCalls);
        Assert.Equal(0, cache.StoreCalls);
    }

    [Fact]
    public async Task ResolveAndCacheAsync_skips_provider_that_cannot_resolve_identity()
    {
        var cache = new StubCache(cachedPath: null);
        var unsupported = new RecordingProvider(
            canResolve: false,
            payload: ValidPayload());

        var supported = new RecordingProvider(
            canResolve: true,
            payload: ValidPayload());

        var resolver = new GameMediaResolver(
            cache,
            [unsupported, supported]);

        var path = await resolver.ResolveAndCacheAsync(
            SteamIdentity(),
            GameMediaAssetType.Cover,
            CancellationToken.None);

        Assert.NotNull(path);
        Assert.Equal(0, unsupported.ResolveCalls);
        Assert.Equal(1, supported.ResolveCalls);
        Assert.Equal(1, cache.StoreCalls);
    }

    [Fact]
    public async Task ResolveAndCacheAsync_propagates_pre_cancelled_token_even_when_cache_contains_asset()
    {
        var cache = new StubCache(
            cachedPath: @"C:\Cache\steam\1874880\cover.jpg");

        var provider = new RecordingProvider();
        var resolver = new GameMediaResolver(
            cache,
            [provider]);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            resolver.ResolveAndCacheAsync(
                SteamIdentity(),
                GameMediaAssetType.Cover,
                cancellation.Token));

        Assert.Equal(0, provider.ResolveCalls);
        Assert.Equal(0, cache.StoreCalls);
    }

    [Fact]
    public async Task ResolveAndCacheAsync_provider_acquisition_failure_returns_null()
    {
        var cache = new StubCache(cachedPath: null);
        var provider = new RecordingProvider(
            resolveException: new HttpRequestException("network unavailable"));

        var resolver = new GameMediaResolver(
            cache,
            [provider]);

        var path = await resolver.ResolveAndCacheAsync(
            SteamIdentity(),
            GameMediaAssetType.Cover,
            CancellationToken.None);

        Assert.Null(path);
        Assert.Equal(1, provider.ResolveCalls);
        Assert.Equal(0, cache.StoreCalls);
    }

    [Fact]
    public async Task ResolveAndCacheAsync_invalid_payload_rejected_by_cache_returns_null()
    {
        var cache = new StubCache(
            cachedPath: null,
            storeException: new InvalidDataException("invalid image"));

        var provider = new RecordingProvider(
            payload: ValidPayload());

        var resolver = new GameMediaResolver(
            cache,
            [provider]);

        var path = await resolver.ResolveAndCacheAsync(
            SteamIdentity(),
            GameMediaAssetType.Cover,
            CancellationToken.None);

        Assert.Null(path);
        Assert.Equal(1, provider.ResolveCalls);
        Assert.Equal(1, cache.StoreCalls);
    }

    [Fact]
    public void TryGetCachedPath_delegates_to_cache()
    {
        var cache = new StubCache(
            cachedPath: @"C:\Cache\steam\1874880\cover.jpg");

        var resolver = new GameMediaResolver(
            cache,
            []);

        var path = resolver.TryGetCachedPath(
            SteamIdentity(),
            GameMediaAssetType.Cover);

        Assert.Equal(
            @"C:\Cache\steam\1874880\cover.jpg",
            path);
        Assert.Equal(1, cache.TryGetCalls);
    }

    private static GameMediaIdentity SteamIdentity() =>
        new(
            ProviderKind.Steam,
            "1874880",
            "Arma Reforger");

    private static GameMediaPayload ValidPayload() =>
        new(
            GameMediaAssetType.Cover,
            "steam-remote",
            "1874880",
            [0xFF, 0xD8, 0xFF, 0xD9],
            "image/jpeg",
            new Uri(
                "https://cdn.cloudflare.steamstatic.com/steam/apps/1874880/library_600x900.jpg"));

    private sealed class StubCache : IGameMediaCache
    {
        private readonly string? _cachedPath;
        private readonly string _storedPath;
        private readonly Exception? _storeException;

        public StubCache(
            string? cachedPath,
            string storedPath = @"C:\Cache\steam\1874880\cover.jpg",
            Exception? storeException = null)
        {
            _cachedPath = cachedPath;
            _storedPath = storedPath;
            _storeException = storeException;
        }

        public int TryGetCalls { get; private set; }
        public int StoreCalls { get; private set; }
        public GameMediaPayload? LastStoredPayload { get; private set; }

        public string? TryGetPath(
            GameMediaIdentity identity,
            GameMediaAssetType assetType)
        {
            TryGetCalls++;
            return _cachedPath;
        }

        public Task<string> StoreAsync(
            GameMediaIdentity identity,
            GameMediaPayload payload,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StoreCalls++;
            LastStoredPayload = payload;

            if (_storeException is not null)
            {
                throw _storeException;
            }

            return Task.FromResult(_storedPath);
        }
    }

    private sealed class RecordingProvider : IGameMediaProvider
    {
        private readonly bool _canResolve;
        private readonly Exception? _resolveException;

        public RecordingProvider(
            bool canResolve = true,
            GameMediaPayload? payload = null,
            Exception? resolveException = null)
        {
            _canResolve = canResolve;
            Payload = payload;
            _resolveException = resolveException;
        }

        public int ResolveCalls { get; private set; }
        public GameMediaPayload? Payload { get; }

        public bool CanResolve(GameMediaIdentity identity) =>
            _canResolve;

        public Task<GameMediaPayload?> ResolveAsync(
            GameMediaIdentity identity,
            GameMediaAssetType assetType,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ResolveCalls++;

            if (_resolveException is not null)
            {
                throw _resolveException;
            }

            return Task.FromResult(Payload);
        }
    }
}
