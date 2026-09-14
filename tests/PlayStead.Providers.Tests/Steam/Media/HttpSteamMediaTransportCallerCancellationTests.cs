using System.Net;
using PlayStead.Core.Media;
using PlayStead.Providers.Steam.Media;

namespace PlayStead.Providers.Tests.Steam.Media;

public sealed class HttpSteamMediaTransportCallerCancellationTests
{
    [Fact]
    public async Task TryDownloadAsync_propagates_caller_cancellation()
    {
        var handler = new WaitingHandler();
        using var httpClient = new HttpClient(handler);
        var transport = new HttpSteamMediaTransport(
            httpClient,
            TimeSpan.FromSeconds(5));

        using var callerCts = new CancellationTokenSource(
            TimeSpan.FromMilliseconds(25));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => transport.TryDownloadAsync(
                "1874880",
                GameMediaAssetType.Cover,
                [new Uri("https://cdn.cloudflare.steamstatic.com/steam/apps/1874880/library_600x900.jpg")],
                callerCts.Token));

        Assert.True(handler.WasCanceled);
    }

    private sealed class WaitingHandler : HttpMessageHandler
    {
        public bool WasCanceled { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(
                    Timeout.InfiniteTimeSpan,
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                WasCanceled = true;
                throw;
            }

            throw new InvalidOperationException(
                "Request should have been canceled by the caller.");
        }
    }
}
