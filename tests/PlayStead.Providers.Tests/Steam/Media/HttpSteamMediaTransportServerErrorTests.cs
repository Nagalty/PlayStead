using System.Net;
using PlayStead.Core.Media;
using PlayStead.Providers.Steam.Media;

namespace PlayStead.Providers.Tests.Steam.Media;

public sealed class HttpSteamMediaTransportServerErrorTests
{
    [Fact]
    public async Task TryDownloadAsync_returns_null_on_non_404_server_error_without_trying_next_candidate()
    {
        var handler = new SequencedHandler();
        using var httpClient = new HttpClient(handler);
        var transport = new HttpSteamMediaTransport(httpClient);

        var payload = await transport.TryDownloadAsync(
            "1874880",
            GameMediaAssetType.Cover,
            [
                new Uri("https://cdn.cloudflare.steamstatic.com/steam/apps/1874880/library_600x900.jpg"),
                new Uri("https://cdn.cloudflare.steamstatic.com/steam/apps/1874880/fallback.jpg")
            ],
            CancellationToken.None);

        Assert.Null(payload);
        Assert.Equal(1, handler.RequestCount);
    }

    private sealed class SequencedHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequestCount++;

            if (RequestCount == 1)
            {
                return Task.FromResult(
                    new HttpResponseMessage(HttpStatusCode.InternalServerError)
                    {
                        RequestMessage = request
                    });
            }

            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent([0xFF, 0xD8, 0xFF, 0xD9]),
                    RequestMessage = request
                });
        }
    }
}
