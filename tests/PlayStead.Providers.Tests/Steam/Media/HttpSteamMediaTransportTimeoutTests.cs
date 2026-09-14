using System.Diagnostics;
using System.Net;
using PlayStead.Core.Media;
using PlayStead.Providers.Steam.Media;

namespace PlayStead.Providers.Tests.Steam.Media;

public sealed class HttpSteamMediaTransportTimeoutTests
{
    [Fact]
    public async Task TryDownloadAsync_returns_null_when_internal_timeout_expires()
    {
        var handler = new DelayedHandler(TimeSpan.FromMilliseconds(250));
        using var httpClient = new HttpClient(handler);
        var transport = new HttpSteamMediaTransport(
            httpClient,
            TimeSpan.FromMilliseconds(25));

        var stopwatch = Stopwatch.StartNew();

        var payload = await transport.TryDownloadAsync(
            "1874880",
            GameMediaAssetType.Cover,
            [new Uri("https://cdn.cloudflare.steamstatic.com/steam/apps/1874880/library_600x900.jpg")],
            CancellationToken.None);

        stopwatch.Stop();

        Assert.Null(payload);
        Assert.True(handler.WasCanceled);
        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromMilliseconds(200),
            $"Internal timeout did not stop the request promptly: {stopwatch.Elapsed}.");
    }

    private sealed class DelayedHandler : HttpMessageHandler
    {
        private readonly TimeSpan _delay;

        public DelayedHandler(TimeSpan delay)
        {
            _delay = delay;
        }

        public bool WasCanceled { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(_delay, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                WasCanceled = true;
                throw;
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent([0x01]),
                RequestMessage = request
            };
        }
    }
}
