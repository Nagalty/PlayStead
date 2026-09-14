using System.Net;
using PlayStead.Core.Library;
using PlayStead.Core.Media;
using PlayStead.Providers.Steam;
using PlayStead.Providers.Steam.Media;

namespace PlayStead.Providers.Tests.Steam.Media;

public sealed class SteamMediaProviderLocalCoverTests : IDisposable
{
    private readonly string _root =
        Path.Combine(
            Path.GetTempPath(),
            "PlayStead.Tests",
            Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ResolveAsync_uses_local_Steam_cover_without_HTTP_request()
    {
        Directory.CreateDirectory(
            Path.Combine(
                _root,
                "appcache",
                "librarycache"));

        var localCoverPath = Path.Combine(
            _root,
            "appcache",
            "librarycache",
            "1874880_library_600x900.jpg");

        await File.WriteAllBytesAsync(
            localCoverPath,
            [0xFF, 0xD8, 0xFF, 0xD9]);

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
        Assert.Equal("steam-local", payload.Source);
        Assert.Equal(
            new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 },
            payload.Content);
        Assert.Equal(0, handler.RequestCount);
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

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;

            return Task.FromResult(
                new HttpResponseMessage(
                    HttpStatusCode.InternalServerError));
        }
    }
}
