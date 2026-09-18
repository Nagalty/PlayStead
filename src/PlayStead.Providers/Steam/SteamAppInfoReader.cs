using System.Text;
using ValveKeyValue;

namespace PlayStead.Providers.Steam;

public sealed class SteamAppInfoReader
{
    private const uint BaseMagic = 0x075644;

    public SteamAppInfoEntry? Find(string path, uint appId, string? expectedName = null)
    {
        if (!File.Exists(path)) return null;
        try
        {
            using var stream = File.OpenRead(path);
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
            var rawMagic = reader.ReadUInt32();
            var version = (byte)(rawMagic & 0xFF);
            if ((rawMagic >> 8) != BaseMagic || version < 40) return null;
            _ = reader.ReadUInt32();
            var stringTableOffset = reader.ReadInt64();
            if (stringTableOffset < 0 || stringTableOffset >= stream.Length) return null;
            stream.Position = stringTableOffset;
            var count = reader.ReadUInt32();
            var strings = new List<string>((int)Math.Min(count, 100_000));
            for (var i = 0; i < count; i++) strings.Add(ReadCString(reader));
            var table = new StringTable(strings);
            stream.Position = 16;
            while (stream.Position + 8 <= stream.Length)
            {
                var currentId = reader.ReadUInt32();
                if (currentId == 0) return null;
                var size = reader.ReadUInt32();
                var entryEnd = checked(stream.Position + size);
                if (entryEnd > stream.Length || size < 60) return null;
                _ = reader.ReadUInt32();
                _ = reader.ReadUInt32();
                _ = reader.ReadUInt64();
                reader.ReadBytes(20);
                _ = reader.ReadUInt32();
                reader.ReadBytes(20);
                var options = new KVSerializerOptions { StringTable = table };
                var document = KVSerializer.Create(KVSerializationFormat.KeyValues1Binary).Deserialize(stream, options);
                if (stream.Position != entryEnd) return null;
                if (currentId == appId) return ReadEntry(currentId, document);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or EndOfStreamException or OverflowException or KeyValueException)
        { return null; }
        return null;
    }

    private static SteamAppInfoEntry ReadEntry(uint appId, KVDocument document)
    {
        var root = document.Root;
        if (root.TryGetValue("appinfo", out var nested)) root = nested;
        return new SteamAppInfoEntry(appId, FindValue(root, "developer"), FindValue(root, "publisher"));
    }

    private static string? Read(KVObject root, string key) =>
        root.TryGetValue(key, out var value) && value.ValueType == KVValueType.String
            ? value.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : null;

    private static string? FindValue(KVObject node, string key)
    {
        var direct = Read(node, key);
        if (!string.IsNullOrWhiteSpace(direct)) return direct;
        foreach (var pair in node)
        {
            var value = FindValue(pair.Value, key);
            if (!string.IsNullOrWhiteSpace(value)) return value;
        }
        return null;
    }

    private static string ReadCString(BinaryReader reader)
    {
        var bytes = new List<byte>();
        int value;
        while ((value = reader.Read()) > 0) bytes.Add((byte)value);
        return Encoding.UTF8.GetString(bytes.ToArray());
    }
}
