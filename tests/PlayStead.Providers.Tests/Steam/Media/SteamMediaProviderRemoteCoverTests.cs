using System.Net;
using System.Reflection;
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
    public async Task ResolveAsync_downloads_modern_hashed_cover_before_legacy_candidates()
    {
        Directory.CreateDirectory(_root);
        const string hash = "86d898447e0e475e3f8a9cc1ef660a80032472d7";
        var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler);
        var appInfoReader = new SteamAppInfoReader(_ => new MemoryStream(
            CreateAppInfoWithLibraryAssets(3768760, hash),
            writable: false));
        var provider = new SteamMediaProvider(
            new WindowsSteamRootLocator([_root]),
            new SteamLocalMediaLocator(),
            new HttpSteamMediaTransport(httpClient),
            appInfoReader: appInfoReader);

        var payload = await provider.ResolveAsync(
            new GameMediaIdentity(ProviderKind.Steam, "3768760", "007 First Light"),
            GameMediaAssetType.Cover,
            CancellationToken.None);

        Assert.NotNull(payload);
        Assert.Equal(
            "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/3768760/86d898447e0e475e3f8a9cc1ef660a80032472d7/library_600x900_2x.jpg",
            payload.SourceUri?.AbsoluteUri);
        Assert.Equal(1, handler.RequestCount);
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

    private static byte[] CreateAppInfoWithLibraryAssets(uint appId, string hash)
    {
        var assembly = Assembly.Load("ValveKeyValue");
        var objectType = assembly.GetType("ValveKeyValue.KVObject", throwOnError: true)!;
        var serializerType = assembly.GetType("ValveKeyValue.KVSerializer", throwOnError: true)!;
        var formatType = assembly.GetType("ValveKeyValue.KVSerializationFormat", throwOnError: true)!;
        var optionsType = assembly.GetType("ValveKeyValue.KVSerializerOptions", throwOnError: true)!;
        var tableType = assembly.GetType("ValveKeyValue.StringTable", throwOnError: true)!;
        var serializer = serializerType.GetMethod("Create", BindingFlags.Public | BindingFlags.Static)!
            .Invoke(null, [Enum.Parse(formatType, "KeyValues1Binary")])!;
        var stringTable = Activator.CreateInstance(tableType)!;
        var options = Activator.CreateInstance(optionsType)!;
        optionsType.GetProperty("StringTable")!.SetValue(options, stringTable);
        var add = objectType.GetMethod("Add", [typeof(string), objectType])!;
        var collection = objectType.GetMethod("Collection", Type.EmptyTypes)!;
        var serialize = serializerType.GetMethod(
            "Serialize",
            [typeof(Stream), objectType, typeof(string), optionsType])!;

        var root = collection.Invoke(null, null)!;
        var common = collection.Invoke(null, null)!;
        var assets = collection.Invoke(null, null)!;
        add.Invoke(assets, ["library_capsule", Activator.CreateInstance(objectType, hash)!]);
        add.Invoke(assets, ["library_600x900", Activator.CreateInstance(objectType, hash)!]);
        add.Invoke(common, ["library_assets", assets]);
        add.Invoke(root, ["common", common]);

        using var payload = new MemoryStream();
        serialize.Invoke(serializer, [payload, root, "appinfo", options]);

        using var output = new MemoryStream();
        using var writer = new BinaryWriter(output, System.Text.Encoding.UTF8, leaveOpen: true);
        writer.Write(0x07564429u);
        writer.Write(1u);
        writer.Write(0L);
        writer.Write(appId);
        writer.Write(checked((uint)(60 + payload.Length)));
        writer.Write(0u);
        writer.Write(0u);
        writer.Write(0ul);
        writer.Write(new byte[20]);
        writer.Write(0u);
        writer.Write(new byte[20]);
        writer.Write(payload.ToArray());
        writer.Write(0u);
        writer.Write(0u);
        var stringTableOffset = output.Position;
        var tableValues = (string[])tableType.GetMethod("ToArray")!.Invoke(stringTable, null)!;
        writer.Write(checked((uint)tableValues.Length));
        foreach (var value in tableValues)
        {
            writer.Write(System.Text.Encoding.UTF8.GetBytes(value));
            writer.Write((byte)0);
        }

        output.Position = 8;
        writer.Write(stringTableOffset);
        writer.Flush();
        return output.ToArray();
    }
}
