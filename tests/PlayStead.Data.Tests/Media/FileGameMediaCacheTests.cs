using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using PlayStead.Core.Library;
using PlayStead.Core.Media;
using PlayStead.Data.Media;

namespace PlayStead.Data.Tests.Media;

public sealed class FileGameMediaCacheTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "PlayStead.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task StoreAsync_persists_valid_JPEG_under_provider_and_id()
    {
        var cache = new FileGameMediaCache(_root);

        var path = await cache.StoreAsync(
            Identity(),
            ValidCoverPayload(),
            CancellationToken.None);

        Assert.Equal(Path.Combine(_root, "steam", "1874880", "cover.jpg"), path);
        Assert.Equal(ValidJpegBytes(), await File.ReadAllBytesAsync(path));
        Assert.Equal(path, cache.TryGetPath(Identity(), GameMediaAssetType.Cover));
    }

    [Fact]
    public async Task Stored_asset_is_found_by_a_new_cache_instance()
    {
        var first = new FileGameMediaCache(_root);
        var path = await first.StoreAsync(Identity(), ValidCoverPayload(), CancellationToken.None);

        var reopened = new FileGameMediaCache(_root);

        Assert.Equal(path, reopened.TryGetPath(Identity(), GameMediaAssetType.Cover));
    }

    [Fact]
    public async Task StoreAsync_rejects_HTML_disguised_as_image()
    {
        var cache = new FileGameMediaCache(_root);
        var payload = Payload(Encoding.UTF8.GetBytes("<html>not image</html>"), "image/jpeg");

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            cache.StoreAsync(Identity(), payload, CancellationToken.None));
    }

    [Fact]
    public async Task StoreAsync_rejects_truncated_image_container()
    {
        var cache = new FileGameMediaCache(_root);
        var payload = Payload([0xFF, 0xD8, 0xFF, 0xD9], "image/jpeg");

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            cache.StoreAsync(Identity(), payload, CancellationToken.None));
    }

    [Fact]
    public async Task Failed_replacement_keeps_previous_valid_asset()
    {
        var cache = new FileGameMediaCache(_root);
        var previous = await cache.StoreAsync(Identity(), ValidCoverPayload(), CancellationToken.None);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            cache.StoreAsync(
                Identity(),
                Payload(Encoding.UTF8.GetBytes("invalid"), "image/jpeg"),
                CancellationToken.None));

        Assert.True(File.Exists(previous));
        Assert.Equal(ValidJpegBytes(), await File.ReadAllBytesAsync(previous));
        Assert.Equal(previous, cache.TryGetPath(Identity(), GameMediaAssetType.Cover));
    }

    [Fact]
    public async Task StoreAsync_uses_magic_bytes_for_extension_and_preserves_other_assets()
    {
        var cache = new FileGameMediaCache(_root);
        var cover = await cache.StoreAsync(Identity(), ValidCoverPayload(), CancellationToken.None);
        var logo = await cache.StoreAsync(
            Identity(),
            new GameMediaPayload(
                GameMediaAssetType.Logo,
                "steam-remote",
                "1874880",
                ValidPngBytes(),
                "image/jpeg",
                null),
            CancellationToken.None);

        Assert.EndsWith("logo.png", logo, StringComparison.Ordinal);
        Assert.Equal(cover, cache.TryGetPath(Identity(), GameMediaAssetType.Cover));
        Assert.Equal(logo, cache.TryGetPath(Identity(), GameMediaAssetType.Logo));
    }


    [Fact]
    public async Task Replacing_asset_with_a_different_format_removes_stale_previous_file()
    {
        var cache = new FileGameMediaCache(_root);

        var jpegPath = await cache.StoreAsync(
            Identity(),
            ValidCoverPayload(),
            CancellationToken.None);

        var pngPayload = new GameMediaPayload(
            GameMediaAssetType.Cover,
            "steam-remote",
            "1874880",
            ValidPngBytes(),
            "image/png",
            new Uri("https://cdn.cloudflare.steamstatic.com/steam/apps/1874880/library_600x900.png"));

        var pngPath = await cache.StoreAsync(
            Identity(),
            pngPayload,
            CancellationToken.None);

        Assert.EndsWith("cover.png", pngPath, StringComparison.Ordinal);
        Assert.False(File.Exists(jpegPath));
        Assert.Equal(
            pngPath,
            cache.TryGetPath(
                Identity(),
                GameMediaAssetType.Cover));
    }

    [Theory]
    [InlineData("../1874880")]
    [InlineData("..")]
    [InlineData("folder/1874880")]
    [InlineData("folder\\1874880")]
    public void Unsafe_provider_game_id_is_rejected(string providerGameId)
    {
        var identity = new GameMediaIdentity(ProviderKind.Manual, providerGameId, "Game");

        Assert.Throws<ArgumentException>(() =>
            new FileGameMediaCache(_root).TryGetPath(identity, GameMediaAssetType.Cover));
    }

    [Fact]
    public async Task StoreAsync_rejects_payload_for_another_game()
    {
        var cache = new FileGameMediaCache(_root);
        var payload = ValidCoverPayload() with { ExternalId = "730" };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            cache.StoreAsync(Identity(), payload, CancellationToken.None));
    }

    [Fact]
    public async Task StoreAsync_rejects_payload_over_25_MiB()
    {
        var cache = new FileGameMediaCache(_root);
        var payload = Payload(new byte[(25 * 1024 * 1024) + 1], "image/png");

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            cache.StoreAsync(Identity(), payload, CancellationToken.None));
    }

    [Fact]
    public async Task StoreAsync_honors_pre_cancelled_token_without_creating_game_directory()
    {
        var cache = new FileGameMediaCache(_root);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            cache.StoreAsync(Identity(), ValidCoverPayload(), cancellation.Token));

        Assert.False(Directory.Exists(Path.Combine(_root, "steam", "1874880")));
    }

    [Fact]
    public async Task Corrupt_metadata_does_not_hide_existing_asset_and_is_repaired_on_write()
    {
        var cache = new FileGameMediaCache(_root);
        var cover = await cache.StoreAsync(Identity(), ValidCoverPayload(), CancellationToken.None);
        var metadata = Path.Combine(Path.GetDirectoryName(cover)!, "metadata.json");
        await File.WriteAllTextAsync(metadata, "{broken");

        Assert.Equal(cover, new FileGameMediaCache(_root).TryGetPath(Identity(), GameMediaAssetType.Cover));

        await cache.StoreAsync(
            Identity(),
            new GameMediaPayload(GameMediaAssetType.Logo, "steam-remote", "1874880", ValidPngBytes(), "image/png", null),
            CancellationToken.None);

        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(metadata));
        Assert.True(document.RootElement.GetProperty("assets").TryGetProperty("cover", out _));
        Assert.True(document.RootElement.GetProperty("assets").TryGetProperty("logo", out _));
    }

    [Fact]
    public async Task Concurrent_asset_writes_leave_both_assets_available()
    {
        var cache = new FileGameMediaCache(_root);

        await Task.WhenAll(
            cache.StoreAsync(Identity(), ValidCoverPayload(), CancellationToken.None),
            cache.StoreAsync(
                Identity(),
                new GameMediaPayload(GameMediaAssetType.Logo, "steam-remote", "1874880", ValidPngBytes(), "image/png", null),
                CancellationToken.None));

        Assert.NotNull(cache.TryGetPath(Identity(), GameMediaAssetType.Cover));
        Assert.NotNull(cache.TryGetPath(Identity(), GameMediaAssetType.Logo));
    }

    [Fact]
    public async Task StoreAsync_crops_transparent_padding_from_PNG_logo()
    {
        var cache =
            new FileGameMediaCache(
                _root);

        var source =
            CreateRgbaPng(
                width: 40,
                height: 30,
                visibleLeft: 13,
                visibleTop: 11,
                visibleWidth: 14,
                visibleHeight: 8);

        var path =
            await cache.StoreAsync(
                Identity(),
                new GameMediaPayload(
                    GameMediaAssetType.Logo,
                    "steam-remote",
                    "1874880",
                    source,
                    "image/png",
                    null),
                CancellationToken.None);

        var stored =
            await File.ReadAllBytesAsync(
                path);

        Assert.Equal(
            (22, 16),
            ReadPngDimensions(
                stored));
    }

    [Fact]
    public async Task StoreAsync_does_not_crop_transparent_padding_from_non_logo_PNG()
    {
        var cache =
            new FileGameMediaCache(
                _root);

        var source =
            CreateRgbaPng(
                width: 40,
                height: 30,
                visibleLeft: 13,
                visibleTop: 11,
                visibleWidth: 14,
                visibleHeight: 8);

        var path =
            await cache.StoreAsync(
                Identity(),
                new GameMediaPayload(
                    GameMediaAssetType.Cover,
                    "steam-remote",
                    "1874880",
                    source,
                    "image/png",
                    null),
                CancellationToken.None);

        Assert.Equal(
            source,
            await File.ReadAllBytesAsync(
                path));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static GameMediaIdentity Identity() =>
        new(ProviderKind.Steam, "1874880", "Arma Reforger");

    private static GameMediaPayload ValidCoverPayload() =>
        Payload(ValidJpegBytes(), "image/jpeg");

    private static GameMediaPayload Payload(byte[] content, string? contentType) =>
        new(
            GameMediaAssetType.Cover,
            "steam-remote",
            "1874880",
            content,
            contentType,
            new Uri("https://cdn.cloudflare.steamstatic.com/steam/apps/1874880/library_600x900.jpg"));

    private static byte[] ValidJpegBytes() =>
        Convert.FromBase64String(
            "/9j/4AAQSkZJRgABAQEAYABgAAD/2wBDAAMCAgMCAgMDAwMEAwMEBQgFBQQEBQoHBwYIDAoMDAsKCwsNDhIQDQ4RDgsLEBYQERMUFRUVDA8XGBYUGBIUFRT/2wBDAQMEBAUEBQkFBQkUDQsNFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBT/wAARCAABAAEDASIAAhEBAxEB/8QAHwAAAQUBAQEBAQEAAAAAAAAAAAECAwQFBgcICQoL/8QAtRAAAgEDAwIEAwUFBAQAAAF9AQIDAAQRBRIhMUEGE1FhByJxFDKBkaEII0KxwRVS0fAkM2JyggkKFhcYGRolJicoKSo0NTY3ODk6Q0RFRkdISUpTVFVWV1hZWmNkZWZnaGlqc3R1dnd4eXqDhIWGh4iJipKTlJWWl5iZmqKjpKWmp6ipqrKztLW2t7i5usLDxMXGx8jJytLT1NXW19jZ2uHi4+Tl5ufo6erx8vP09fb3+Pn6/8QAHwEAAwEBAQEBAQEBAQAAAAAAAAECAwQFBgcICQoL/8QAtREAAgECBAQDBAcFBAQAAQJ3AAECAxEEBSExBhJBUQdhcRMiMoEIFEKRobHBCSMzUvAVYnLRChYkNOEl8RcYGRomJygpKjU2Nzg5OkNERUZHSElKU1RVVldYWVpjZGVmZ2hpanN0dXZ3eHl6goOEhYaHiImKkpOUlZaXmJmaoqOkpaanqKmqsrO0tba3uLm6wsPExcbHyMnK0tPU1dbX2Nna4uPk5ebn6Onq8vP09fb3+Pn6/9oADAMBAAIRAxEAPwD9U6KKKAP/2Q==");

    private static byte[] ValidPngBytes() =>
        Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");


    private static byte[] CreateRgbaPng(
        int width,
        int height,
        int visibleLeft,
        int visibleTop,
        int visibleWidth,
        int visibleHeight)
    {
        var rowByteCount =
            width * 4;

        using var filtered =
            new MemoryStream(
                (rowByteCount + 1) *
                height);

        for (var y = 0; y < height; y++)
        {
            filtered.WriteByte(0);

            for (var x = 0; x < width; x++)
            {
                var visible =
                    x >= visibleLeft &&
                    x < visibleLeft + visibleWidth &&
                    y >= visibleTop &&
                    y < visibleTop + visibleHeight;

                filtered.WriteByte(255);
                filtered.WriteByte(255);
                filtered.WriteByte(255);
                filtered.WriteByte(
                    visible
                        ? (byte)255
                        : (byte)0);
            }
        }

        byte[] compressedBytes;

        using (var compressed =
               new MemoryStream())
        {
            using (var deflater =
                   new ZLibStream(
                       compressed,
                       CompressionLevel.Optimal,
                       leaveOpen: true))
            {
                var filteredBytes =
                    filtered.ToArray();

                deflater.Write(
                    filteredBytes,
                    0,
                    filteredBytes.Length);
            }

            compressedBytes =
                compressed.ToArray();
        }

        using var output =
            new MemoryStream();

        output.Write(
            new byte[]
            {
                137, 80, 78, 71,
                13, 10, 26, 10
            });

        Span<byte> header =
            stackalloc byte[13];

        BinaryPrimitives.WriteUInt32BigEndian(
            header[..4],
            (uint)width);

        BinaryPrimitives.WriteUInt32BigEndian(
            header.Slice(4, 4),
            (uint)height);

        header[8] = 8;
        header[9] = 6;
        header[10] = 0;
        header[11] = 0;
        header[12] = 0;

        WritePngChunk(
            output,
            "IHDR"u8,
            header);

        WritePngChunk(
            output,
            "IDAT"u8,
            compressedBytes);

        WritePngChunk(
            output,
            "IEND"u8,
            ReadOnlySpan<byte>.Empty);

        return output.ToArray();
    }

    private static (int Width, int Height)
        ReadPngDimensions(
            byte[] content)
    {
        Assert.True(
            content.Length >= 24);

        Assert.Equal(
            new byte[]
            {
                137, 80, 78, 71,
                13, 10, 26, 10
            },
            content[..8]);

        return (
            checked(
                (int)
                BinaryPrimitives.ReadUInt32BigEndian(
                    content.AsSpan(
                        16,
                        4))),
            checked(
                (int)
                BinaryPrimitives.ReadUInt32BigEndian(
                    content.AsSpan(
                        20,
                        4))));
    }

    private static void WritePngChunk(
        Stream stream,
        ReadOnlySpan<byte> type,
        ReadOnlySpan<byte> data)
    {
        Span<byte> length =
            stackalloc byte[4];

        BinaryPrimitives.WriteUInt32BigEndian(
            length,
            (uint)data.Length);

        stream.Write(
            length);

        stream.Write(
            type);

        stream.Write(
            data);

        Span<byte> crcBytes =
            stackalloc byte[4];

        BinaryPrimitives.WriteUInt32BigEndian(
            crcBytes,
            ComputePngCrc(
                type,
                data));

        stream.Write(
            crcBytes);
    }

    private static uint ComputePngCrc(
        ReadOnlySpan<byte> type,
        ReadOnlySpan<byte> data)
    {
        var crc =
            uint.MaxValue;

        foreach (var value in type)
        {
            crc =
                UpdatePngCrc(
                    crc,
                    value);
        }

        foreach (var value in data)
        {
            crc =
                UpdatePngCrc(
                    crc,
                    value);
        }

        return ~crc;
    }

    private static uint UpdatePngCrc(
        uint crc,
        byte value)
    {
        crc ^= value;

        for (var bit = 0; bit < 8; bit++)
        {
            crc =
                (crc & 1) == 0
                    ? crc >> 1
                    : (crc >> 1) ^
                      0xEDB88320u;
        }

        return crc;
    }
}
