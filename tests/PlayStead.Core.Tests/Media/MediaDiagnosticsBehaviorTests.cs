using PlayStead.Core.Library;
using PlayStead.Core.Media;

namespace PlayStead.Core.Tests.Media;

public sealed class MediaDiagnosticsBehaviorTests
{
    [Fact]
    public async Task Resolver_reports_cache_hit_once_and_does_not_call_provider()
    {
        var diagnostics = new RecordingDiagnostics();
        var cache = new StubCache(@"C:\cache\cover.jpg");
        var provider = new StubProvider();
        var resolver = new GameMediaResolver(cache, [provider], diagnostics);

        var path = await resolver.ResolveAndCacheAsync(Identity(), GameMediaAssetType.Cover, default);

        Assert.Equal(@"C:\cache\cover.jpg", path);
        Assert.Equal(0, provider.Calls);
        Assert.Collection(diagnostics.Events,
            item => Assert.Equal(MediaResolutionEventKind.CacheHit, item.Kind));
    }

    [Fact]
    public async Task Resolver_reports_cache_miss_and_fallback_once_when_no_provider_resolves()
    {
        var diagnostics = new RecordingDiagnostics();
        var resolver = new GameMediaResolver(new StubCache(null), [], diagnostics);

        var path = await resolver.ResolveAndCacheAsync(Identity(), GameMediaAssetType.Hero, default);

        Assert.Null(path);
        Assert.Collection(diagnostics.Events,
            item => Assert.Equal(MediaResolutionEventKind.CacheMiss, item.Kind),
            item => Assert.Equal(MediaResolutionEventKind.FallbackUsed, item.Kind));
    }

    [Fact]
    public async Task Resolver_reports_invalid_image_when_cache_rejects_provider_payload()
    {
        var diagnostics = new RecordingDiagnostics();
        var provider = new StubProvider(new GameMediaPayload(
            GameMediaAssetType.Cover, "steam-remote", "1874880", [1], "image/jpeg",
            new Uri("https://example.test/cover.jpg")));
        var resolver = new GameMediaResolver(new StubCache(null, rejectStore: true), [provider], diagnostics);

        var path = await resolver.ResolveAndCacheAsync(Identity(), GameMediaAssetType.Cover, default);

        Assert.Null(path);
        Assert.Collection(diagnostics.Events,
            item => Assert.Equal(MediaResolutionEventKind.CacheMiss, item.Kind),
            item => Assert.Equal(MediaResolutionEventKind.InvalidImage, item.Kind),
            item => Assert.Equal(MediaResolutionEventKind.FallbackUsed, item.Kind));
    }

    private static GameMediaIdentity Identity() =>
        new(ProviderKind.Steam, "1874880", "Arma Reforger");

    private sealed class RecordingDiagnostics : IMediaDiagnostics
    {
        public List<MediaResolutionEvent> Events { get; } = [];
        public void Report(MediaResolutionEvent mediaEvent) => Events.Add(mediaEvent);
    }

    private sealed class StubCache(string? path, bool rejectStore = false) : IGameMediaCache
    {
        public string? TryGetPath(GameMediaIdentity identity, GameMediaAssetType assetType) => path;
        public Task<string> StoreAsync(GameMediaIdentity identity, GameMediaPayload payload,
            CancellationToken cancellationToken) => rejectStore
            ? Task.FromException<string>(new InvalidDataException("invalid payload"))
            : Task.FromResult("stored.jpg");
    }

    private sealed class StubProvider(GameMediaPayload? payload = null) : IGameMediaProvider
    {
        public int Calls { get; private set; }
        public bool CanResolve(GameMediaIdentity identity) => true;
        public Task<GameMediaPayload?> ResolveAsync(GameMediaIdentity identity, GameMediaAssetType assetType,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(payload);
        }
    }
}
