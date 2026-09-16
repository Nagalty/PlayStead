using System.Buffers.Binary;
using System.IO.Compression;

namespace PlayStead.Data.Media;

internal static class PngLogoNormalizer
{
    private const byte AlphaThreshold = 8;
    private const int PaddingPixels = 4;
    private const int MaximumDecodedBytes = 64 * 1024 * 1024;

    private static ReadOnlySpan<byte> PngSignature =>
        [137, 80, 78, 71, 13, 10, 26, 10];

    public static byte[] Normalize(byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (!TryDecode(content, out var image))
        {
            return content;
        }

        if (!TryFindVisibleBounds(image, out var bounds))
        {
            return content;
        }

        var left = Math.Max(0, bounds.Left - PaddingPixels);
        var top = Math.Max(0, bounds.Top - PaddingPixels);
        var right = Math.Min(image.Width - 1, bounds.Right + PaddingPixels);
        var bottom = Math.Min(image.Height - 1, bounds.Bottom + PaddingPixels);

        if (left == 0 &&
            top == 0 &&
            right == image.Width - 1 &&
            bottom == image.Height - 1)
        {
            return content;
        }

        var croppedWidth = right - left + 1;
        var croppedHeight = bottom - top + 1;
        var croppedPixels = new byte[croppedWidth * croppedHeight * image.BytesPerPixel];

        for (var y = 0; y < croppedHeight; y++)
        {
            var sourceOffset =
                ((top + y) * image.Width + left) *
                image.BytesPerPixel;

            var destinationOffset =
                y * croppedWidth * image.BytesPerPixel;

            Buffer.BlockCopy(
                image.Pixels,
                sourceOffset,
                croppedPixels,
                destinationOffset,
                croppedWidth * image.BytesPerPixel);
        }

        return Encode(
            image,
            croppedPixels,
            croppedWidth,
            croppedHeight);
    }

    private static bool TryDecode(
        byte[] content,
        out DecodedPng image)
    {
        image = default!;

        if (content.Length < 8 ||
            !content.AsSpan(0, 8).SequenceEqual(PngSignature))
        {
            return false;
        }

        var offset = 8;
        var sawHeader = false;
        var sawEnd = false;
        var width = 0;
        var height = 0;
        byte bitDepth = 0;
        byte colorType = 0;
        byte compressionMethod = 0;
        byte filterMethod = 0;
        byte interlaceMethod = 0;
        byte[]? palette = null;
        byte[]? transparency = null;

        using var compressed = new MemoryStream();

        while (offset <= content.Length - 12)
        {
            var chunkLengthValue =
                BinaryPrimitives.ReadUInt32BigEndian(
                    content.AsSpan(offset, 4));

            if (chunkLengthValue > int.MaxValue ||
                chunkLengthValue > (uint)(content.Length - offset - 12))
            {
                return false;
            }

            var chunkLength = (int)chunkLengthValue;
            var type = content.AsSpan(offset + 4, 4);
            var data = content.AsSpan(offset + 8, chunkLength);

            if (!sawHeader)
            {
                if (!type.SequenceEqual("IHDR"u8) ||
                    chunkLength != 13)
                {
                    return false;
                }

                var widthValue =
                    BinaryPrimitives.ReadUInt32BigEndian(
                        data[..4]);

                var heightValue =
                    BinaryPrimitives.ReadUInt32BigEndian(
                        data.Slice(4, 4));

                if (widthValue == 0 ||
                    heightValue == 0 ||
                    widthValue > int.MaxValue ||
                    heightValue > int.MaxValue)
                {
                    return false;
                }

                width = (int)widthValue;
                height = (int)heightValue;
                bitDepth = data[8];
                colorType = data[9];
                compressionMethod = data[10];
                filterMethod = data[11];
                interlaceMethod = data[12];
                sawHeader = true;
            }
            else if (type.SequenceEqual("PLTE"u8))
            {
                palette = data.ToArray();
            }
            else if (type.SequenceEqual("tRNS"u8))
            {
                transparency = data.ToArray();
            }
            else if (type.SequenceEqual("IDAT"u8))
            {
                compressed.Write(data);
            }
            else if (type.SequenceEqual("IEND"u8))
            {
                sawEnd = true;
                break;
            }

            offset += 12 + chunkLength;
        }

        if (!sawHeader ||
            !sawEnd ||
            compressed.Length == 0 ||
            bitDepth != 8 ||
            compressionMethod != 0 ||
            filterMethod != 0 ||
            interlaceMethod != 0)
        {
            return false;
        }

        var bytesPerPixel =
            colorType switch
            {
                4 => 2,
                6 => 4,
                3 => 1,
                _ => 0
            };

        if (bytesPerPixel == 0)
        {
            return false;
        }

        if (colorType == 3 &&
            (palette is null ||
             palette.Length == 0 ||
             palette.Length % 3 != 0))
        {
            return false;
        }

        long decodedByteCount =
            (long)width *
            height *
            bytesPerPixel;

        if (decodedByteCount <= 0 ||
            decodedByteCount > MaximumDecodedBytes)
        {
            return false;
        }

        byte[] filtered;

        try
        {
            compressed.Position = 0;

            using var inflater =
                new ZLibStream(
                    compressed,
                    CompressionMode.Decompress,
                    leaveOpen: true);

            using var decoded = new MemoryStream();

            inflater.CopyTo(decoded);

            filtered = decoded.ToArray();
        }
        catch (InvalidDataException)
        {
            return false;
        }

        var rowByteCount =
            checked(width * bytesPerPixel);

        var expectedFilteredLength =
            checked((rowByteCount + 1) * height);

        if (filtered.Length != expectedFilteredLength)
        {
            return false;
        }

        var pixels =
            new byte[
                checked(
                    width *
                    height *
                    bytesPerPixel)];

        var previousRow =
            new byte[rowByteCount];

        var currentRow =
            new byte[rowByteCount];

        for (var y = 0; y < height; y++)
        {
            var sourceOffset =
                y * (rowByteCount + 1);

            var filterType =
                filtered[sourceOffset];

            filtered.AsSpan(
                    sourceOffset + 1,
                    rowByteCount)
                .CopyTo(currentRow);

            if (!TryUnfilterRow(
                    currentRow,
                    previousRow,
                    bytesPerPixel,
                    filterType))
            {
                return false;
            }

            currentRow.CopyTo(
                pixels,
                y * rowByteCount);

            (previousRow, currentRow) =
                (currentRow, previousRow);
        }

        image =
            new DecodedPng(
                width,
                height,
                bitDepth,
                colorType,
                bytesPerPixel,
                pixels,
                palette,
                transparency);

        return true;
    }

    private static bool TryUnfilterRow(
        Span<byte> row,
        ReadOnlySpan<byte> previousRow,
        int bytesPerPixel,
        byte filterType)
    {
        switch (filterType)
        {
            case 0:
                return true;

            case 1:
                for (var x = 0; x < row.Length; x++)
                {
                    byte left =
                        x >= bytesPerPixel
                            ? row[x - bytesPerPixel]
                            : (byte)0;

                    row[x] =
                        unchecked(
                            (byte)(
                                row[x] +
                                left));
                }

                return true;

            case 2:
                for (var x = 0; x < row.Length; x++)
                {
                    row[x] =
                        unchecked(
                            (byte)(
                                row[x] +
                                previousRow[x]));
                }

                return true;

            case 3:
                for (var x = 0; x < row.Length; x++)
                {
                    byte left =
                        x >= bytesPerPixel
                            ? row[x - bytesPerPixel]
                            : (byte)0;

                    var up =
                        previousRow[x];

                    row[x] =
                        unchecked(
                            (byte)(
                                row[x] +
                                ((left + up) >> 1)));
                }

                return true;

            case 4:
                for (var x = 0; x < row.Length; x++)
                {
                    byte left =
                        x >= bytesPerPixel
                            ? row[x - bytesPerPixel]
                            : (byte)0;

                    var up =
                        previousRow[x];

                    byte upperLeft =
                        x >= bytesPerPixel
                            ? previousRow[x - bytesPerPixel]
                            : (byte)0;

                    row[x] =
                        unchecked(
                            (byte)(
                                row[x] +
                                PaethPredictor(
                                    left,
                                    up,
                                    upperLeft)));
                }

                return true;

            default:
                return false;
        }
    }

    private static byte PaethPredictor(
        byte left,
        byte up,
        byte upperLeft)
    {
        var estimate =
            left + up - upperLeft;

        var leftDistance =
            Math.Abs(
                estimate - left);

        var upDistance =
            Math.Abs(
                estimate - up);

        var upperLeftDistance =
            Math.Abs(
                estimate - upperLeft);

        if (leftDistance <= upDistance &&
            leftDistance <= upperLeftDistance)
        {
            return left;
        }

        return upDistance <= upperLeftDistance
            ? up
            : upperLeft;
    }

    private static bool TryFindVisibleBounds(
        DecodedPng image,
        out PixelBounds bounds)
    {
        var left = image.Width;
        var top = image.Height;
        var right = -1;
        var bottom = -1;

        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                if (GetAlpha(
                        image,
                        x,
                        y) <= AlphaThreshold)
                {
                    continue;
                }

                left = Math.Min(left, x);
                top = Math.Min(top, y);
                right = Math.Max(right, x);
                bottom = Math.Max(bottom, y);
            }
        }

        if (right < left ||
            bottom < top)
        {
            bounds = default;
            return false;
        }

        bounds =
            new PixelBounds(
                left,
                top,
                right,
                bottom);

        return true;
    }

    private static byte GetAlpha(
        DecodedPng image,
        int x,
        int y)
    {
        var offset =
            (y * image.Width + x) *
            image.BytesPerPixel;

        return image.ColorType switch
        {
            6 =>
                image.Pixels[offset + 3],

            4 =>
                image.Pixels[offset + 1],

            3 =>
                GetIndexedAlpha(
                    image,
                    image.Pixels[offset]),

            _ =>
                byte.MaxValue
        };
    }

    private static byte GetIndexedAlpha(
        DecodedPng image,
        byte paletteIndex)
    {
        if (image.Transparency is null ||
            paletteIndex >= image.Transparency.Length)
        {
            return byte.MaxValue;
        }

        return image.Transparency[paletteIndex];
    }

    private static byte[] Encode(
        DecodedPng source,
        byte[] pixels,
        int width,
        int height)
    {
        var rowByteCount =
            checked(
                width *
                source.BytesPerPixel);

        using var filtered =
            new MemoryStream(
                checked(
                    (rowByteCount + 1) *
                    height));

        for (var y = 0; y < height; y++)
        {
            filtered.WriteByte(0);

            filtered.Write(
                pixels,
                y * rowByteCount,
                rowByteCount);
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

        using var output = new MemoryStream();

        output.Write(
            PngSignature);

        Span<byte> header =
            stackalloc byte[13];

        BinaryPrimitives.WriteUInt32BigEndian(
            header[..4],
            (uint)width);

        BinaryPrimitives.WriteUInt32BigEndian(
            header.Slice(4, 4),
            (uint)height);

        header[8] =
            source.BitDepth;

        header[9] =
            source.ColorType;

        header[10] = 0;
        header[11] = 0;
        header[12] = 0;

        WriteChunk(
            output,
            "IHDR"u8,
            header);

        if (source.ColorType == 3 &&
            source.Palette is not null)
        {
            WriteChunk(
                output,
                "PLTE"u8,
                source.Palette);

            if (source.Transparency is not null)
            {
                WriteChunk(
                    output,
                    "tRNS"u8,
                    source.Transparency);
            }
        }

        WriteChunk(
            output,
            "IDAT"u8,
            compressedBytes);

        WriteChunk(
            output,
            "IEND"u8,
            ReadOnlySpan<byte>.Empty);

        return output.ToArray();
    }

    private static void WriteChunk(
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
            ComputeCrc(
                type,
                data));

        stream.Write(
            crcBytes);
    }

    private static uint ComputeCrc(
        ReadOnlySpan<byte> type,
        ReadOnlySpan<byte> data)
    {
        var crc =
            uint.MaxValue;

        foreach (var value in type)
        {
            crc =
                UpdateCrc(
                    crc,
                    value);
        }

        foreach (var value in data)
        {
            crc =
                UpdateCrc(
                    crc,
                    value);
        }

        return ~crc;
    }

    private static uint UpdateCrc(
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

    private sealed record DecodedPng(
        int Width,
        int Height,
        byte BitDepth,
        byte ColorType,
        int BytesPerPixel,
        byte[] Pixels,
        byte[]? Palette,
        byte[]? Transparency);

    private readonly record struct PixelBounds(
        int Left,
        int Top,
        int Right,
        int Bottom);
}
