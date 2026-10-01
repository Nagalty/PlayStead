using PlayStead.Core.Library;
using PlayStead.Core.Media;
using PlayStead.Providers.Steam;
using PlayStead.Providers.Steam.Media;

namespace PlayStead.Providers.Tests.Steam.Media;

public sealed class SteamMediaProviderTransportDelegationTests : IDisposable
{
    private readonly string _root =
        Path.Combine(
            Path.GetTempPath(),
            "PlayStead.Tests",
            Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ResolveAsync_delegates_remote_cover_to_transport_when_local_cover_is_missing()
    {
        Directory.CreateDirectory(_root);

        var expectedPayload = new GameMediaPayload(
            GameMediaAssetType.Cover,
            "steam-remote",
            "1874880",
            [0x01, 0x02, 0x03],
            "image/jpeg",
            new Uri("https://cdn.cloudflare.steamstatic.com/steam/apps/1874880/library_600x900.jpg"));

        var transport = new RecordingTransport(expectedPayload);

        var provider = new SteamMediaProvider(
            new WindowsSteamRootLocator([_root]),
            new SteamLocalMediaLocator(),
            transport);

        var payload = await provider.ResolveAsync(
            new GameMediaIdentity(
                ProviderKind.Steam,
                "1874880",
                "Arma Reforger"),
            GameMediaAssetType.Cover,
            CancellationToken.None);

        Assert.Equal(1, transport.CallCount);
        Assert.Equal("1874880", transport.LastAppId);
        Assert.Equal(GameMediaAssetType.Cover, transport.LastAssetType);
        Assert.Collection(
            transport.LastCandidates!,
            first => Assert.Equal(
                "https://cdn.cloudflare.steamstatic.com/steam/apps/1874880/library_capsule_2x.jpg",
                first.AbsoluteUri),
            second => Assert.Equal(
                "https://cdn.cloudflare.steamstatic.com/steam/apps/1874880/library_capsule.jpg",
                second.AbsoluteUri),
            third => Assert.Equal(
                "https://cdn.cloudflare.steamstatic.com/steam/apps/1874880/library_600x900_2x.jpg",
                third.AbsoluteUri),
            fourth => Assert.Equal(
                "https://cdn.cloudflare.steamstatic.com/steam/apps/1874880/library_600x900.jpg",
                fourth.AbsoluteUri));
        Assert.Same(expectedPayload, payload);
    }


    [Fact]
    public async Task ResolveAsync_returns_null_when_transport_returns_null()
    {
        Directory.CreateDirectory(_root);

        var transport = new RecordingTransport(payload: null);

        var provider = new SteamMediaProvider(
            new WindowsSteamRootLocator([_root]),
            new SteamLocalMediaLocator(),
            transport);

        var payload = await provider.ResolveAsync(
            new GameMediaIdentity(
                ProviderKind.Steam,
                "1874880",
                "Arma Reforger"),
            GameMediaAssetType.Cover,
            CancellationToken.None);

        Assert.Null(payload);
        Assert.Equal(1, transport.CallCount);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(
                _root,
                recursive: true);
        }
    }

    private sealed class RecordingTransport : ISteamMediaTransport
    {
        private readonly GameMediaPayload? _payload;

        public RecordingTransport(GameMediaPayload? payload)
        {
            _payload = payload;
        }

        public int CallCount { get; private set; }
        public string? LastAppId { get; private set; }
        public GameMediaAssetType? LastAssetType { get; private set; }
        public IReadOnlyList<Uri>? LastCandidates { get; private set; }

        public Task<GameMediaPayload?> TryDownloadAsync(
            string appId,
            GameMediaAssetType assetType,
            IReadOnlyList<Uri> candidates,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            CallCount++;
            LastAppId = appId;
            LastAssetType = assetType;
            LastCandidates = candidates.ToArray();

            return Task.FromResult<GameMediaPayload?>(_payload);
        }
    }
}
