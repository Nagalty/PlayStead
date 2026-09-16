using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Text.Json;
using PlayStead.Core.Media;

namespace PlayStead.Data.Media;

public sealed class FileGameMediaCache : IGameMediaCache
{
    private const int MaximumPayloadSize = 25 * 1024 * 1024;
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> DirectoryLocks =
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _root;

    public FileGameMediaCache(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        _root = Path.GetFullPath(root);
    }

    public string? TryGetPath(GameMediaIdentity identity, GameMediaAssetType assetType)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ValidateIdentity(identity);
        ValidateAssetType(assetType);

        var directory = GetGameDirectory(identity);
        var manifest = ReadManifest(directory);
        var key = GetAssetName(assetType);
        if (manifest?.Assets.TryGetValue(key, out var entry) == true &&
            IsSafeFileName(entry.FileName))
        {
            var recordedPath = Path.Combine(directory, entry.FileName);
            if (File.Exists(recordedPath))
            {
                return recordedPath;
            }
        }

        foreach (var extension in new[] { ".jpg", ".png" })
        {
            var candidate = Path.Combine(directory, key + extension);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    public async Task<string> StoreAsync(
        GameMediaIdentity identity,
        GameMediaPayload payload,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(payload);
        cancellationToken.ThrowIfCancellationRequested();
        ValidateIdentity(identity);
        ValidatePayload(identity, payload);

        var extension = DetectImageExtension(payload.Content);
        var contentToStore =
            payload.AssetType == GameMediaAssetType.Logo &&
            string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase)
                ? PngLogoNormalizer.Normalize(payload.Content)
                : payload.Content;

        var directory = GetGameDirectory(identity);
        var gate = DirectoryLocks.GetOrAdd(directory, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(directory);

            var assetName = GetAssetName(payload.AssetType);
            var fileName = assetName + extension;
            var finalPath = Path.Combine(directory, fileName);
            await WriteAtomicallyAsync(finalPath, contentToStore, cancellationToken)
                .ConfigureAwait(false);

            var manifest = ReadManifest(directory) ?? ReconstructManifest(directory);
            manifest.Assets[assetName] = new GameMediaCacheEntry(
                payload.Source,
                payload.ExternalId,
                assetName,
                fileName,
                DateTimeOffset.UtcNow,
                payload.SourceUri?.AbsoluteUri,
                payload.ContentType);

            var metadata = JsonSerializer.SerializeToUtf8Bytes(manifest, JsonOptions);
            await WriteAtomicallyAsync(
                    Path.Combine(directory, "metadata.json"),
                    metadata,
                    cancellationToken)
                .ConfigureAwait(false);

            DeleteAlternativeAssetFiles(
                directory,
                assetName,
                extension);

            return finalPath;
        }
        finally
        {
            gate.Release();
        }
    }

    private static void ValidateIdentity(GameMediaIdentity identity)
    {
        if (!Enum.IsDefined(identity.Provider))
        {
            throw new ArgumentOutOfRangeException(nameof(identity), "The provider is not supported.");
        }

        var id = identity.ProviderGameId;
        if (id.Contains("..", StringComparison.Ordinal) ||
            id.Contains(Path.DirectorySeparatorChar) ||
            id.Contains(Path.AltDirectorySeparatorChar) ||
            id.Contains('/') ||
            id.Contains('\\'))
        {
            throw new ArgumentException("Provider game ID must be a safe path segment.", nameof(identity));
        }
    }

    private static void ValidatePayload(GameMediaIdentity identity, GameMediaPayload payload)
    {
        ValidateAssetType(payload.AssetType);
        ArgumentException.ThrowIfNullOrWhiteSpace(payload.Source);
        ArgumentException.ThrowIfNullOrWhiteSpace(payload.ExternalId);

        if (!string.Equals(identity.ProviderGameId, payload.ExternalId, StringComparison.Ordinal))
        {
            throw new ArgumentException("Payload external ID does not match the media identity.", nameof(payload));
        }

        if (payload.Content is null)
        {
            throw new ArgumentException("Payload content is required.", nameof(payload));
        }

        if (payload.Content.Length > MaximumPayloadSize)
        {
            throw new InvalidDataException("Media payload exceeds the 25 MiB limit.");
        }
    }

    private static void ValidateAssetType(GameMediaAssetType assetType)
    {
        if (!Enum.IsDefined(assetType))
        {
            throw new ArgumentOutOfRangeException(nameof(assetType));
        }
    }

    private string GetGameDirectory(GameMediaIdentity identity) =>
        Path.Combine(
            _root,
            identity.Provider.ToString().ToLowerInvariant(),
            identity.ProviderGameId);

    private static string GetAssetName(GameMediaAssetType assetType) =>
        assetType.ToString().ToLowerInvariant();

    private static string DetectImageExtension(byte[] content)
    {
        if (IsValidPng(content))
        {
            return ".png";
        }

        if (IsValidJpeg(content))
        {
            return ".jpg";
        }

        throw new InvalidDataException("Media payload is not a valid JPEG or PNG image.");
    }

    private static bool IsValidPng(ReadOnlySpan<byte> content)
    {
        ReadOnlySpan<byte> signature = [137, 80, 78, 71, 13, 10, 26, 10];
        if (content.Length < 8 || !content[..8].SequenceEqual(signature))
        {
            return false;
        }

        var offset = 8;
        var sawHeader = false;
        var sawData = false;
        while (offset <= content.Length - 12)
        {
            var length = BinaryPrimitives.ReadUInt32BigEndian(content.Slice(offset, 4));
            if (length > int.MaxValue || length > (uint)(content.Length - offset - 12))
            {
                return false;
            }

            var chunkLength = (int)length;
            var type = content.Slice(offset + 4, 4);
            var data = content.Slice(offset + 8, chunkLength);
            var expectedCrc = BinaryPrimitives.ReadUInt32BigEndian(
                content.Slice(offset + 8 + chunkLength, 4));
            if (ComputePngCrc(type, data) != expectedCrc)
            {
                return false;
            }

            if (!sawHeader)
            {
                if (!type.SequenceEqual("IHDR"u8) || chunkLength != 13)
                {
                    return false;
                }

                sawHeader = true;
            }
            else if (type.SequenceEqual("IDAT"u8))
            {
                sawData = true;
            }
            else if (type.SequenceEqual("IEND"u8))
            {
                return chunkLength == 0 && sawData && offset + 12 == content.Length;
            }

            offset += 12 + chunkLength;
        }

        return false;
    }

    private static uint ComputePngCrc(ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        var crc = uint.MaxValue;
        foreach (var value in type)
        {
            crc = UpdateCrc(crc, value);
        }

        foreach (var value in data)
        {
            crc = UpdateCrc(crc, value);
        }

        return ~crc;
    }

    private static uint UpdateCrc(uint crc, byte value)
    {
        crc ^= value;
        for (var bit = 0; bit < 8; bit++)
        {
            crc = (crc & 1) == 0 ? crc >> 1 : (crc >> 1) ^ 0xEDB88320u;
        }

        return crc;
    }

    private static bool IsValidJpeg(ReadOnlySpan<byte> content)
    {
        if (content.Length < 8 || content[0] != 0xFF || content[1] != 0xD8 ||
            content[^2] != 0xFF || content[^1] != 0xD9)
        {
            return false;
        }

        var offset = 2;
        var sawFrame = false;
        while (offset < content.Length - 2)
        {
            if (content[offset++] != 0xFF)
            {
                return false;
            }

            while (offset < content.Length && content[offset] == 0xFF)
            {
                offset++;
            }

            if (offset >= content.Length)
            {
                return false;
            }

            var marker = content[offset++];
            if (marker is 0xD8 or 0x01 || marker is >= 0xD0 and <= 0xD7)
            {
                continue;
            }

            if (offset > content.Length - 2)
            {
                return false;
            }

            var segmentLength = BinaryPrimitives.ReadUInt16BigEndian(content.Slice(offset, 2));
            if (segmentLength < 2 || segmentLength > content.Length - offset)
            {
                return false;
            }

            if (marker is >= 0xC0 and <= 0xC3)
            {
                sawFrame = true;
            }

            offset += segmentLength;
            if (marker == 0xDA)
            {
                return sawFrame && offset < content.Length - 2;
            }
        }

        return false;
    }

    private static void DeleteAlternativeAssetFiles(
        string directory,
        string assetName,
        string currentExtension)
    {
        foreach (var extension in new[] { ".jpg", ".png" })
        {
            if (string.Equals(
                    extension,
                    currentExtension,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            File.Delete(
                Path.Combine(
                    directory,
                    assetName + extension));
        }
    }

    private static async Task WriteAtomicallyAsync(
        string finalPath,
        byte[] content,
        CancellationToken cancellationToken)
    {
        var temporaryPath = finalPath + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            await using (var stream = new FileStream(
                             temporaryPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             81920,
                             FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await stream.WriteAsync(content, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, finalPath, overwrite: true);
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }

    private static GameMediaCacheManifest? ReadManifest(string directory)
    {
        try
        {
            var path = Path.Combine(directory, "metadata.json");
            if (!File.Exists(path))
            {
                return null;
            }

            var manifest = JsonSerializer.Deserialize<GameMediaCacheManifest>(
                File.ReadAllBytes(path),
                JsonOptions);
            return manifest?.Assets is null ? null : manifest;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static GameMediaCacheManifest ReconstructManifest(string directory)
    {
        var assets = new Dictionary<string, GameMediaCacheEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var assetType in Enum.GetValues<GameMediaAssetType>())
        {
            var assetName = GetAssetName(assetType);
            var fileName = new[] { assetName + ".jpg", assetName + ".png" }
                .FirstOrDefault(candidate => File.Exists(Path.Combine(directory, candidate)));
            if (fileName is null)
            {
                continue;
            }

            assets[assetName] = new GameMediaCacheEntry(
                "unknown",
                "unknown",
                assetName,
                fileName,
                File.GetLastWriteTimeUtc(Path.Combine(directory, fileName)),
                null,
                null);
        }

        return new GameMediaCacheManifest(assets);
    }

    private static bool IsSafeFileName(string fileName) =>
        !string.IsNullOrWhiteSpace(fileName) &&
        string.Equals(fileName, Path.GetFileName(fileName), StringComparison.Ordinal) &&
        !fileName.Contains("..", StringComparison.Ordinal);
}
