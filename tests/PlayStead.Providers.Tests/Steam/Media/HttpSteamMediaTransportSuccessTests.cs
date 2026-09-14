using System.Net;
using System.Net.Http.Headers;
using PlayStead.Core.Media;
using PlayStead.Providers.Steam.Media;

namespace PlayStead.Providers.Tests.Steam.Media;

public sealed class HttpSteamMediaTransportSuccessTests
{
    [Fact]
    public async Task TryDownloadAsync_returns_remote_payload_from_successful_response()
    {
        var expectedBytes = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        var uri = new Uri(
            "https://cdn.cloudflare.steamstatic.com/steam/apps/1874880/library_600x900.jpg");

        var handler = new SuccessHandler(
            expectedBytes,
            "image/jpeg");

        using var httpClient = new HttpClient(handler);
        var transport = new HttpSteamMediaTransport(httpClient);

        var payload = await transport.TryDownloadAsync(
            "1874880",
            GameMediaAssetType.Cover,
            [uri],
            CancellationToken.None);

        Assert.NotNull(payload);
        Assert.Equal(GameMediaAssetType.Cover, payload.AssetType);
        Assert.Equal("steam-remote", payload.Source);
        Assert.Equal("1874880", payload.ExternalId);
        Assert.Equal(expectedBytes, payload.Content);
        Assert.Equal("image/jpeg", payload.ContentType);
        Assert.Equal(uri, payload.SourceUri);
        Assert.Equal(1, handler.RequestCount);
    }

    private sealed class SuccessHandler : HttpMessageHandler
    {
        private readonly byte[] _content;
        private readonly string _contentType;

        public SuccessHandler(
            byte[] content,
            string contentType)
        {
            _content = content;
            _contentType = contentType;
        }

        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequestCount++;

            var content = new ByteArrayContent(_content);
            content.Headers.ContentType =
                new MediaTypeHeaderValue(_contentType);

            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = content,
                    RequestMessage = request
                });
        }
    }
}
