using System.Net;
using PlayStead.Core.Media;
using PlayStead.Providers.Steam.Media;

namespace PlayStead.Providers.Tests.Steam.Media;

public sealed class HttpSteamMediaTransportNotFoundTests
{
    [Fact]
    public async Task TryDownloadAsync_returns_null_after_single_404_request()
    {
        var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler);
        var transport = new HttpSteamMediaTransport(httpClient);

        var uri = new Uri(
            "https://cdn.cloudflare.steamstatic.com/steam/apps/1874880/library_600x900.jpg");

        var payload = await transport.TryDownloadAsync(
            "1874880",
            GameMediaAssetType.Cover,
            [uri],
            CancellationToken.None);

        Assert.Null(payload);
        Assert.Equal(1, handler.RequestCount);
        Assert.Equal(uri, handler.LastRequestUri);
    }


    [Fact]
    public async Task TryDownloadAsync_tries_next_candidate_after_404()
    {
        var handler = new NotFoundThenSuccessHandler();
        using var httpClient = new HttpClient(handler);
        var transport = new HttpSteamMediaTransport(httpClient);

        var first = new Uri(
            "https://cdn.cloudflare.steamstatic.com/steam/apps/1874880/library_600x900.jpg");

        var second = new Uri(
            "https://cdn.cloudflare.steamstatic.com/steam/apps/1874880/fallback.jpg");

        var payload = await transport.TryDownloadAsync(
            "1874880",
            GameMediaAssetType.Cover,
            [first, second],
            CancellationToken.None);

        Assert.NotNull(payload);
        Assert.Equal("steam-remote", payload.Source);
        Assert.Equal(second, payload.SourceUri);
        Assert.Equal(2, handler.RequestCount);
        Assert.Equal(
            new[] { first, second },
            handler.RequestUris);
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

            return Task.FromResult(
                new HttpResponseMessage(
                    HttpStatusCode.NotFound));
        }
    }

    private sealed class NotFoundThenSuccessHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        public List<Uri> RequestUris { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            RequestCount++;
            RequestUris.Add(request.RequestUri!);

            if (RequestCount == 1)
            {
                return Task.FromResult(
                    new HttpResponseMessage(HttpStatusCode.NotFound)
                    {
                        RequestMessage = request
                    });
            }

            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(
                        [0xFF, 0xD8, 0xFF, 0xD9]),
                    RequestMessage = request
                });
        }
    }

}
