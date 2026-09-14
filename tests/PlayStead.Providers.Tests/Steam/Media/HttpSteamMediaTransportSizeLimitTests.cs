using System.Net;
using System.Net.Http.Headers;
using PlayStead.Core.Media;
using PlayStead.Providers.Steam.Media;

namespace PlayStead.Providers.Tests.Steam.Media;

public sealed class HttpSteamMediaTransportSizeLimitTests
{
    private const long MaxMediaBytes = 25L * 1024 * 1024;

    [Fact]
    public async Task TryDownloadAsync_rejects_oversized_ContentLength_before_reading_body()
    {
        var content = new TrackingContent(MaxMediaBytes + 1);
        var handler = new OversizedResponseHandler(content);

        using var httpClient = new HttpClient(handler);
        var transport = new HttpSteamMediaTransport(httpClient);

        var payload = await transport.TryDownloadAsync(
            "1874880",
            GameMediaAssetType.Cover,
            [new Uri("https://cdn.cloudflare.steamstatic.com/steam/apps/1874880/library_600x900.jpg")],
            CancellationToken.None);

        Assert.False(content.WasRead);
        Assert.Null(payload);
    }

    private sealed class OversizedResponseHandler : HttpMessageHandler
    {
        private readonly HttpContent _content;

        public OversizedResponseHandler(HttpContent content)
        {
            _content = content;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = _content,
                    RequestMessage = request
                });
        }
    }

    private sealed class TrackingContent : HttpContent
    {
        public TrackingContent(long declaredLength)
        {
            Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            Headers.ContentLength = declaredLength;
        }

        public bool WasRead { get; private set; }

        protected override async Task SerializeToStreamAsync(
            Stream stream,
            TransportContext? context)
        {
            WasRead = true;
            await stream.WriteAsync(new byte[] { 0x01, 0x02, 0x03, 0x04 });
        }

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }
}
