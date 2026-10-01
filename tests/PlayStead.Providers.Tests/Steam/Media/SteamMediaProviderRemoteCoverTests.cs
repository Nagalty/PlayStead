using System.Net;
using PlayStead.Core.Library;
using PlayStead.Core.Media;
using PlayStead.Providers.Steam;
using PlayStead.Providers.Steam.Media;

namespace PlayStead.Providers.Tests.Steam.Media;

public sealed class SteamMediaProviderRemoteCoverTests : IDisposable
{
    private readonly string _root =
        Path.Combine(
            Path.GetTempPath(),
            "PlayStead.Tests",
            Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ResolveAsync_downloads_cover_from_CDN_when_local_cover_is_missing()
    {
        Directory.CreateDirectory(_root);

        var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler);

        var provider = new SteamMediaProvider(
            new WindowsSteamRootLocator([_root]),
            new SteamLocalMediaLocator(),
            new HttpSteamMediaTransport(httpClient));

        var payload = await provider.ResolveAsync(
            new GameMediaIdentity(
                ProviderKind.Steam,
                "1874880",
                "Arma Reforger"),
            GameMediaAssetType.Cover,
            CancellationToken.None);

        Assert.NotNull(payload);
        Assert.Equal("steam-remote", payload.Source);
        Assert.Equal(1, handler.RequestCount);
        Assert.Equal(
            "https://cdn.cloudflare.steamstatic.com/steam/apps/1874880/library_capsule_2x.jpg",
            handler.LastRequestUri?.AbsoluteUri);
    }

    [Fact]
    public async Task ResolveAsync_uses_official_capsule_for_manual_bridge_AppId_3768760()
    {
        Directory.CreateDirectory(_root);

        var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler);
        var provider = new SteamMediaProvider(
            new WindowsSteamRootLocator([_root]),
            new SteamLocalMediaLocator(),
            new HttpSteamMediaTransport(httpClient));

        var payload = await provider.ResolveAsync(
            new GameMediaIdentity(ProviderKind.Steam, "3768760", "007 First Light"),
            GameMediaAssetType.Cover,
            CancellationToken.None);

        Assert.NotNull(payload);
        Assert.Equal(
            "https://cdn.cloudflare.steamstatic.com/steam/apps/3768760/library_capsule_2x.jpg",
            payload.SourceUri?.AbsoluteUri);
    }

    [Fact]
    public async Task ResolveAsync_tries_next_cover_candidate_after_recoverable_404()
    {
        Directory.CreateDirectory(_root);
        var handler = new FallbackHandler();
        using var httpClient = new HttpClient(handler);
        var provider = new SteamMediaProvider(
            new WindowsSteamRootLocator([_root]),
            new SteamLocalMediaLocator(),
            new HttpSteamMediaTransport(httpClient));

        var payload = await provider.ResolveAsync(
            new GameMediaIdentity(ProviderKind.Steam, "1874880", "Arma Reforger"),
            GameMediaAssetType.Cover,
            CancellationToken.None);

        Assert.NotNull(payload);
        Assert.Equal(GameMediaAssetType.Cover, payload.AssetType);
        Assert.Equal(2, handler.RequestCount);
        Assert.EndsWith("library_capsule.jpg", handler.LastRequestUri!.AbsoluteUri, StringComparison.Ordinal);
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

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }
        public Uri? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            RequestCount++;
            LastRequestUri = request.RequestUri;

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(
                    [0xFF, 0xD8, 0xFF, 0xD9])
            };

            response.Content.Headers.ContentType =
                new System.Net.Http.Headers.MediaTypeHeaderValue(
                    "image/jpeg");

            return Task.FromResult(response);
        }
    }

    private sealed class FallbackHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }
        public Uri? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            LastRequestUri = request.RequestUri;
            if (RequestCount == 1)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent([0xFF, 0xD8, 0xFF, 0xD9])
            };
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
            return Task.FromResult(response);
        }
    }
}
