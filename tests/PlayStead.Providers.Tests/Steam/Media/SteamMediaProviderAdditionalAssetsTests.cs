using System.Net;
using System.Net.Http.Headers;
using PlayStead.Core.Library;
using PlayStead.Core.Media;
using PlayStead.Providers.Steam;
using PlayStead.Providers.Steam.Media;

namespace PlayStead.Providers.Tests.Steam.Media;

public sealed class SteamMediaProviderAdditionalAssetsTests : IDisposable
{
    private const string Cdn = "https://cdn.cloudflare.steamstatic.com/steam/apps/1874880/";
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "PlayStead.Tests", Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData(GameMediaAssetType.Header, "1874880_library_header.jpg", "image/jpeg")]
    [InlineData(GameMediaAssetType.Hero, "1874880_library_hero.jpg", "image/jpeg")]
    [InlineData(GameMediaAssetType.Logo, "1874880_logo.png", "image/png")]
    public async Task Local_hit_returns_asset_without_remote_transport(
        GameMediaAssetType type, string filename, string contentType)
    {
        var directory = Path.Combine(_root, "appcache", "librarycache");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, filename);
        byte[] bytes = type == GameMediaAssetType.Logo
            ? [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]
            : [0xFF, 0xD8, 0xFF, 0xD9];
        await File.WriteAllBytesAsync(path, bytes);
        var transport = new RejectingTransport();
        var provider = new SteamMediaProvider(
            new WindowsSteamRootLocator([_root]), new SteamLocalMediaLocator(), transport);

        var payload = await provider.ResolveAsync(Identity(), type, CancellationToken.None);

        Assert.NotNull(payload);
        Assert.Equal(type, payload.AssetType);
        Assert.Equal("steam-local", payload.Source);
        Assert.Equal("1874880", payload.ExternalId);
        Assert.Equal(bytes, payload.Content);
        Assert.Equal(contentType, payload.ContentType);
        Assert.Equal(new Uri(Path.GetFullPath(path)), payload.SourceUri);
        Assert.Equal(0, transport.Calls);
    }

    [Theory]
    [InlineData(GameMediaAssetType.Header, "library_header.jpg", "image/jpeg")]
    [InlineData(GameMediaAssetType.Hero, "library_hero.jpg", "image/jpeg")]
    [InlineData(GameMediaAssetType.Logo, "logo.png", "image/png")]
    public async Task Local_miss_returns_first_successful_remote_candidate(
        GameMediaAssetType type, string filename, string contentType)
    {
        using var handler = new RecordingHandler(contentType, HttpStatusCode.OK);
        using var client = new HttpClient(handler);
        var provider = CreateProvider(client);

        var payload = await provider.ResolveAsync(Identity(), type, CancellationToken.None);

        Assert.NotNull(payload);
        Assert.Equal(new[] { Cdn + filename }, handler.Requests);
        AssertPayload(payload, type, filename, contentType);
    }

    [Fact]
    public async Task Header_404_then_success_returns_second_candidate_in_exact_order()
    {
        using var handler = new RecordingHandler("image/jpeg",
            HttpStatusCode.NotFound, HttpStatusCode.OK);
        using var client = new HttpClient(handler);
        var provider = CreateProvider(client);

        var payload = await provider.ResolveAsync(
            Identity(), GameMediaAssetType.Header, CancellationToken.None);

        Assert.NotNull(payload);
        Assert.Equal(new[] { Cdn + "library_header.jpg", Cdn + "header.jpg" }, handler.Requests);
        AssertPayload(payload, GameMediaAssetType.Header, "header.jpg", "image/jpeg");
    }

    [Theory]
    [InlineData(GameMediaAssetType.Header, "library_header.jpg", "header.jpg")]
    [InlineData(GameMediaAssetType.Hero, "library_hero.jpg", null)]
    [InlineData(GameMediaAssetType.Logo, "logo.png", null)]
    public async Task Local_and_remote_misses_try_all_candidates_then_return_null(
        GameMediaAssetType type, string first, string? second)
    {
        using var handler = new RecordingHandler("image/jpeg",
            HttpStatusCode.NotFound, HttpStatusCode.NotFound);
        using var client = new HttpClient(handler);
        var provider = CreateProvider(client);

        var payload = await provider.ResolveAsync(Identity(), type, CancellationToken.None);

        string[] expected = second is null
            ? [Cdn + first]
            : [Cdn + first, Cdn + second];
        Assert.Equal(expected, handler.Requests);
        Assert.Null(payload);
    }

    private SteamMediaProvider CreateProvider(HttpClient client)
    {
        Directory.CreateDirectory(_root);
        return new SteamMediaProvider(
            new WindowsSteamRootLocator([_root]),
            new SteamLocalMediaLocator(),
            new HttpSteamMediaTransport(client));
    }

    private static GameMediaIdentity Identity() =>
        new(ProviderKind.Steam, "1874880", "Arma Reforger");

    private static void AssertPayload(GameMediaPayload payload,
        GameMediaAssetType type, string filename, string contentType)
    {
        Assert.Equal(type, payload.AssetType);
        Assert.Equal("steam-remote", payload.Source);
        Assert.Equal("1874880", payload.ExternalId);
        Assert.Equal(RecordingHandler.Bytes, payload.Content);
        Assert.Equal(contentType, payload.ContentType);
        Assert.Equal(new Uri(Cdn + filename), payload.SourceUri);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private sealed class RejectingTransport : ISteamMediaTransport
    {
        public int Calls { get; private set; }
        public Task<GameMediaPayload?> TryDownloadAsync(string appId,
            GameMediaAssetType assetType, IReadOnlyList<Uri> candidates,
            CancellationToken cancellationToken)
        {
            Calls++;
            throw new InvalidOperationException("A local hit must not invoke remote transport.");
        }
    }

    private sealed class RecordingHandler(
        string contentType, params HttpStatusCode[] statuses) : HttpMessageHandler
    {
        public static readonly byte[] Bytes = [0x01, 0x02, 0x03];
        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var index = Requests.Count;
            Requests.Add(request.RequestUri!.AbsoluteUri);
            Assert.True(index < statuses.Length, "Unexpected extra HTTP request.");
            var response = new HttpResponseMessage(statuses[index])
            {
                RequestMessage = request,
                Content = new ByteArrayContent(Bytes)
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            return Task.FromResult(response);
        }
    }
}
