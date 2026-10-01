using System.Net;
using System.IO.Compression;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PlayStead.Core.Catalog;
using PlayStead.Data.Catalog;

namespace PlayStead.Data.Tests.Catalog;

public sealed class HttpCanonicalCatalogSyncServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "PlayStead.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Valid_manifest_and_payload_are_imported_and_cached()
    {
        Directory.CreateDirectory(_root);
        var options = new CatalogDatabaseOptions(Path.Combine(_root, "catalog.db"), Path.Combine(_root, "Backups"));
        await new CatalogDatabaseInitializer(options).InitializeAsync(CancellationToken.None);
        var payload = Payload(7);
        var manifest = Manifest(payload, 7);
        using var client = Client(manifest, payload);
        var service = CreateService(options, client, 0);

        Assert.True(await service.SyncAsync(CancellationToken.None));
        Assert.Equal(7, (await new SqliteCanonicalCatalogStore(options).GetMetadataAsync(CancellationToken.None)).CatalogVersion);
        Assert.True(File.Exists(Path.Combine(_root, "cache", "catalog-manifest.json")));
    }

    [Fact]
    public async Task Network_failure_uses_last_valid_cache()
    {
        Directory.CreateDirectory(_root);
        var options = new CatalogDatabaseOptions(Path.Combine(_root, "catalog.db"), Path.Combine(_root, "Backups"));
        await new CatalogDatabaseInitializer(options).InitializeAsync(CancellationToken.None);
        var payload = Payload(8);
        var manifest = Manifest(payload, 8);
        var cache = Path.Combine(_root, "cache");
        Directory.CreateDirectory(cache);
        await File.WriteAllBytesAsync(Path.Combine(cache, "catalog-payload.json"), payload);
        await File.WriteAllTextAsync(Path.Combine(cache, "catalog-manifest.json"), JsonSerializer.Serialize(manifest));
        using var client = new HttpClient(new ThrowingHandler()) { BaseAddress = new Uri("https://catalog.invalid/") };
        var service = CreateService(options, client, 0);

        Assert.True(await service.SyncAsync(CancellationToken.None));
        Assert.Equal(8, (await new SqliteCanonicalCatalogStore(options).GetMetadataAsync(CancellationToken.None)).CatalogVersion);
    }

    [Fact]
    public async Task V2_gzip_manifest_is_decompressed_verified_and_imported()
    {
        Directory.CreateDirectory(_root);
        var options = new CatalogDatabaseOptions(Path.Combine(_root, "catalog.db"), Path.Combine(_root, "Backups"));
        await new CatalogDatabaseInitializer(options).InitializeAsync(CancellationToken.None);
        var json = Payload(9);
        var compressed = Gzip(json);
        var manifest = new CanonicalCatalogManifest(2, 9, DateTimeOffset.UtcNow, "catalog-payload.json.gz", Convert.ToHexString(SHA256.HashData(compressed)), 1, compressed.LongLength, "gzip", json.LongLength);
        using var client = Client(manifest, compressed);
        var service = CreateService(options, client, 0);

        Assert.True(await service.SyncAsync(CancellationToken.None));
        Assert.Equal(9, (await new SqliteCanonicalCatalogStore(options).GetMetadataAsync(CancellationToken.None)).CatalogVersion);
    }

    private HttpCanonicalCatalogSyncService CreateService(CatalogDatabaseOptions options, HttpClient client, long localVersion)
    {
        var cache = Path.Combine(_root, "cache");
        var importer = new CanonicalCatalogBatchImporter(options);
        return new HttpCanonicalCatalogSyncService(client, new CanonicalCatalogSyncOptions(new Uri("https://catalog.invalid/manifest.json"), cache), importer, _ => Task.FromResult(localVersion));
    }

    private static HttpClient Client(CanonicalCatalogManifest manifest, byte[] payload) =>
        new(new StaticHandler(manifest, payload)) { BaseAddress = new Uri("https://catalog.invalid/") };

    private static byte[] Payload(long version) => JsonSerializer.SerializeToUtf8Bytes(new CanonicalCatalogDocument(1, version, DateTimeOffset.UtcNow,
                [new CanonicalCatalogEntry(new CatalogContentId(Guid.NewGuid()), PlaySteadPublicId.Parse($"PlayStead-{version:000000}"), "Game", "GAME", null, "Dev", "Pub", ["RPG"], [], CatalogProvenance.Igdb, DateTimeOffset.UtcNow)]));

    private static byte[] Gzip(byte[] payload)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.SmallestSize, true)) gzip.Write(payload);
        return output.ToArray();
    }

    private static CanonicalCatalogManifest Manifest(byte[] payload, long version) =>
        new(1, version, DateTimeOffset.UtcNow, "catalog-payload.json", Convert.ToHexString(SHA256.HashData(payload)), 1);

    private sealed class StaticHandler(CanonicalCatalogManifest manifest, byte[] payload) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = request.RequestUri!.AbsolutePath.EndsWith("manifest.json", StringComparison.Ordinal)
                    ? JsonContent.Create(manifest)
                    : new ByteArrayContent(payload)
            });
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("offline");
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
