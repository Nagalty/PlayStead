using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using PlayStead.Core.Catalog;
using PlayStead.Core.Persistence;

namespace PlayStead.Data.Catalog;

public sealed class HttpCanonicalCatalogSyncService : ICanonicalCatalogSyncService
{
    private const int SupportedSchemaVersionV1 = 1;
    private const int SupportedSchemaVersionV2 = 2;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;
    private readonly CanonicalCatalogSyncOptions _options;
    private readonly CanonicalCatalogBatchImporter _importer;
    private readonly Func<CancellationToken, Task<long>> _localVersion;

    public HttpCanonicalCatalogSyncService(HttpClient httpClient, CanonicalCatalogSyncOptions options, CanonicalCatalogBatchImporter importer, Func<CancellationToken, Task<long>> localVersion)
    { _httpClient = httpClient; _options = options; _importer = importer; _localVersion = localVersion; }

    public async Task<bool> SyncAsync(CancellationToken cancellationToken)
    {
        try
        {
            CanonicalCatalogManifest manifest;
            TransportPayload transport;
            using (var networkTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                networkTimeout.CancelAfter(_options.Timeout ?? TimeSpan.FromSeconds(15));
                manifest = await _httpClient.GetFromJsonAsync<CanonicalCatalogManifest>(_options.ManifestUri, JsonOptions, networkTimeout.Token)
                    ?? throw new InvalidDataException("Catalog manifest is empty.");
                Trace.WriteLine($"[CATALOG-SYNC] Manifest Version={manifest.CatalogVersion} Entries={manifest.EntryCount} Schema={manifest.SchemaVersion}");
                transport = await DownloadTransportAsync(manifest, networkTimeout.Token).ConfigureAwait(false);
            }

            Trace.WriteLine($"[CATALOG-SYNC] Download Bytes={transport.TransportBytes.LongLength}");
            var documentBytes = await DecodePayloadAsync(manifest, transport.TransportBytes, cancellationToken).ConfigureAwait(false);
            Trace.WriteLine($"[CATALOG-SYNC] Decode Bytes={documentBytes.LongLength}");
            if (!await ImportIfNewerAsync(manifest, documentBytes, cancellationToken).ConfigureAwait(false))
            {
                Trace.WriteLine("[CATALOG-SYNC] Result=UpToDate");
                return false;
            }

            await WriteCacheAsync(manifest, transport.TransportBytes, cancellationToken).ConfigureAwait(false);
            Trace.WriteLine("[CATALOG-SYNC] CacheWrite=PASS");
            Trace.WriteLine("[CATALOG-SYNC] Result=Imported");
            return true;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            Trace.WriteLine("[CATALOG-SYNC] Result=FallbackCache");
            return await TryImportCacheAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            Trace.WriteLine($"[CATALOG-SYNC] Result=Failed Error={ex.GetType().Name}");
            return await TryImportCacheAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<TransportPayload> DownloadTransportAsync(CanonicalCatalogManifest manifest, CancellationToken token)
    {
        if (!IsSupportedManifest(manifest) || manifest.CatalogVersion < 0 || manifest.EntryCount < 0)
            throw new InvalidDataException("Catalog manifest is unsupported.");
        if (await _localVersion(token).ConfigureAwait(false) >= manifest.CatalogVersion)
            throw new CatalogAlreadyCurrentException();

        var uri = new Uri(_options.ManifestUri, manifest.PayloadUrl);
        using var response = await _httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is long length && length > _options.MaximumPayloadBytes)
            throw new InvalidDataException("Catalog payload is too large.");
        var bytes = await response.Content.ReadAsByteArrayAsync(token).ConfigureAwait(false);
        if (bytes.LongLength > _options.MaximumPayloadBytes)
            throw new InvalidDataException("Catalog payload is too large.");
        ValidateTransportBytes(manifest, bytes);
        Trace.WriteLine("[CATALOG-SYNC] Hash=PASS");
        return new TransportPayload(bytes);
    }

    private async Task<byte[]> DecodePayloadAsync(CanonicalCatalogManifest manifest, byte[] transportBytes, CancellationToken token)
    {
        if (manifest.SchemaVersion == SupportedSchemaVersionV1)
            return transportBytes;
        if (!string.Equals(manifest.PayloadEncoding, "gzip", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Catalog payload encoding is unsupported.");

        await using var compressed = new MemoryStream(transportBytes);
        await using var gzip = new GZipStream(compressed, CompressionMode.Decompress);
        using var expanded = new MemoryStream();
        await gzip.CopyToAsync(expanded, token).ConfigureAwait(false);
        var documentBytes = expanded.ToArray();
        if (manifest.PayloadUncompressedSizeBytes > 0 && manifest.PayloadUncompressedSizeBytes != documentBytes.LongLength)
            throw new InvalidDataException("Catalog uncompressed size does not match the manifest.");
        return documentBytes;
    }

    private async Task<bool> ImportIfNewerAsync(CanonicalCatalogManifest manifest, byte[] documentBytes, CancellationToken token)
    {
        if (!IsSupportedManifest(manifest) || manifest.CatalogVersion < 0 || manifest.EntryCount < 0)
            return false;
        if (await _localVersion(token).ConfigureAwait(false) >= manifest.CatalogVersion)
            return false;

        var document = JsonSerializer.Deserialize<CanonicalCatalogDocument>(documentBytes, JsonOptions)
            ?? throw new InvalidDataException("Catalog payload is empty.");
        Trace.WriteLine($"[CATALOG-SYNC] Document Schema={document.SchemaVersion} Version={document.CatalogVersion} Entries={document.Entries.Count}");
        if (document.SchemaVersion is not (SupportedSchemaVersionV1 or SupportedSchemaVersionV2) || document.CatalogVersion != manifest.CatalogVersion || document.Entries.Count != manifest.EntryCount)
            throw new InvalidDataException("Catalog document metadata does not match the manifest.");
        Trace.WriteLine("[CATALOG-SYNC] Import START");
        await _importer.ImportAsync(document, token).ConfigureAwait(false);
        Trace.WriteLine("[CATALOG-SYNC] Import END");
        return true;
    }

    private async Task<bool> TryImportCacheAsync(CancellationToken token)
    {
        var manifestPath = Path.Combine(_options.CacheDirectory, "catalog-manifest.json");
        if (!File.Exists(manifestPath)) return false;
        var manifest = JsonSerializer.Deserialize<CanonicalCatalogManifest>(await File.ReadAllBytesAsync(manifestPath, token), JsonOptions);
        if (manifest is null) return false;
        var payloadPath = Path.Combine(_options.CacheDirectory, Path.GetFileName(manifest.PayloadUrl));
        if (!File.Exists(payloadPath)) payloadPath = Path.Combine(_options.CacheDirectory, "catalog-payload.json");
        if (!File.Exists(payloadPath)) return false;
        var transportBytes = await File.ReadAllBytesAsync(payloadPath, token);
        ValidateTransportBytes(manifest, transportBytes);
        var documentBytes = await DecodePayloadAsync(manifest, transportBytes, token).ConfigureAwait(false);
        return await ImportIfNewerAsync(manifest, documentBytes, token).ConfigureAwait(false);
    }

    private async Task WriteCacheAsync(CanonicalCatalogManifest manifest, byte[] transportBytes, CancellationToken token)
    {
        Directory.CreateDirectory(_options.CacheDirectory);
        var payloadName = Path.GetFileName(manifest.PayloadUrl);
        var payloadTemp = Path.Combine(_options.CacheDirectory, payloadName + ".tmp");
        var manifestTemp = Path.Combine(_options.CacheDirectory, "catalog-manifest.json.tmp");
        await File.WriteAllBytesAsync(payloadTemp, transportBytes, token).ConfigureAwait(false);
        await File.WriteAllTextAsync(manifestTemp, JsonSerializer.Serialize(manifest, JsonOptions), token).ConfigureAwait(false);
        File.Move(payloadTemp, Path.Combine(_options.CacheDirectory, payloadName), true);
        File.Move(manifestTemp, Path.Combine(_options.CacheDirectory, "catalog-manifest.json"), true);
    }

    private static void ValidateTransportBytes(CanonicalCatalogManifest manifest, byte[] bytes)
    {
        if (manifest.PayloadSizeBytes > 0 && manifest.PayloadSizeBytes != bytes.LongLength)
            throw new InvalidDataException("Catalog payload size does not match the manifest.");
        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        if (!hash.Equals(manifest.PayloadSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Catalog payload hash does not match the manifest.");
    }

    private static bool IsSupportedManifest(CanonicalCatalogManifest manifest) =>
        manifest.SchemaVersion switch
        {
            SupportedSchemaVersionV1 => string.IsNullOrWhiteSpace(manifest.PayloadEncoding),
            SupportedSchemaVersionV2 => string.Equals(manifest.PayloadEncoding, "gzip", StringComparison.OrdinalIgnoreCase),
            _ => false
        };

    private sealed record TransportPayload(byte[] TransportBytes);
    private sealed class CatalogAlreadyCurrentException : Exception;
}
