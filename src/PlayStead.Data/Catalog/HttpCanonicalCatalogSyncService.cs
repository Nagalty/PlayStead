using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using PlayStead.Core.Catalog;
using PlayStead.Core.Persistence;

namespace PlayStead.Data.Catalog;

public sealed class HttpCanonicalCatalogSyncService : ICanonicalCatalogSyncService
{
    private const int SupportedSchemaVersion = 1;
    private readonly HttpClient _httpClient;
    private readonly CanonicalCatalogSyncOptions _options;
    private readonly CanonicalCatalogBatchImporter _importer;
    private readonly Func<CancellationToken, Task<long>> _localVersion;

    public HttpCanonicalCatalogSyncService(HttpClient httpClient, CanonicalCatalogSyncOptions options, CanonicalCatalogBatchImporter importer, Func<CancellationToken, Task<long>> localVersion)
    { _httpClient = httpClient; _options = options; _importer = importer; _localVersion = localVersion; }

    public async Task<bool> SyncAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.Timeout ?? TimeSpan.FromSeconds(15));
        try
        {
            var manifest = await _httpClient.GetFromJsonAsync<CanonicalCatalogManifest>(_options.ManifestUri, timeout.Token)
                ?? throw new InvalidDataException("Catalog manifest is empty.");
            var bytes = await DownloadPayloadAsync(manifest, timeout.Token);
            if (bytes is null || !await ImportIfNewerAsync(manifest, bytes, timeout.Token)) return false;
            await WriteCacheAsync(manifest, bytes, timeout.Token);
            return true;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return await TryImportCacheAsync(timeout.Token);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // Keep the sync resilient; callers can observe the false result and retain the current catalog.
            return await TryImportCacheAsync(timeout.Token);
        }
    }

    private async Task<byte[]?> DownloadPayloadAsync(CanonicalCatalogManifest manifest, CancellationToken token)
    {
        if (manifest.SchemaVersion != SupportedSchemaVersion || manifest.CatalogVersion < 0 || manifest.EntryCount < 0)
            return null;
        if (await _localVersion(token).ConfigureAwait(false) >= manifest.CatalogVersion)
            return null;
        var uri = new Uri(_options.ManifestUri, manifest.PayloadUrl);
        using var response = await _httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is long length && length > _options.MaximumPayloadBytes) return null;
        var bytes = await response.Content.ReadAsByteArrayAsync(token);
        if (bytes.LongLength > _options.MaximumPayloadBytes || (manifest.PayloadSizeBytes > 0 && manifest.PayloadSizeBytes != bytes.LongLength)) return null;
        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        return hash.Equals(manifest.PayloadSha256, StringComparison.OrdinalIgnoreCase) ? bytes : null;
    }

    private async Task<bool> ImportIfNewerAsync(CanonicalCatalogManifest manifest, byte[] bytes, CancellationToken token)
    {
        if (manifest.SchemaVersion != SupportedSchemaVersion || manifest.CatalogVersion < 0 || manifest.EntryCount < 0)
            return false;
        if (await _localVersion(token).ConfigureAwait(false) >= manifest.CatalogVersion)
            return false;
        var document = JsonSerializer.Deserialize<CanonicalCatalogDocument>(bytes)
            ?? throw new InvalidDataException("Catalog payload is empty.");
        if (document.SchemaVersion != manifest.SchemaVersion || document.CatalogVersion != manifest.CatalogVersion || document.Entries.Count != manifest.EntryCount)
            return false;
        await _importer.ImportAsync(document, token);
        return true;
    }

    private async Task<bool> TryImportCacheAsync(CancellationToken token)
    {
        var manifestPath = Path.Combine(_options.CacheDirectory, "catalog-manifest.json");
        var payloadPath = Path.Combine(_options.CacheDirectory, "catalog-payload.json");
        if (!File.Exists(manifestPath) || !File.Exists(payloadPath)) return false;
        var manifest = JsonSerializer.Deserialize<CanonicalCatalogManifest>(await File.ReadAllBytesAsync(manifestPath, token));
        if (manifest is null) return false;
        var bytes = await File.ReadAllBytesAsync(payloadPath, token);
        return await ImportIfNewerAsync(manifest, bytes, token);
    }

    private async Task WriteCacheAsync(CanonicalCatalogManifest manifest, byte[] bytes, CancellationToken token)
    {
        Directory.CreateDirectory(_options.CacheDirectory);
        var payloadTemp = Path.Combine(_options.CacheDirectory, "catalog-payload.json.tmp");
        var manifestTemp = Path.Combine(_options.CacheDirectory, "catalog-manifest.json.tmp");
        await File.WriteAllBytesAsync(payloadTemp, bytes, token);
        await File.WriteAllTextAsync(manifestTemp, JsonSerializer.Serialize(manifest), token);
        File.Move(payloadTemp, Path.Combine(_options.CacheDirectory, "catalog-payload.json"), true);
        File.Move(manifestTemp, Path.Combine(_options.CacheDirectory, "catalog-manifest.json"), true);
    }
}
