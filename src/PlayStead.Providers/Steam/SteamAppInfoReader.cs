using System.Text;
using ValveKeyValue;

namespace PlayStead.Providers.Steam;

public sealed class SteamAppInfoReader
{
    private const uint BaseMagic = 0x075644;
    private readonly Func<string, Stream> _openRead;
    private readonly Func<string, bool> _exists;

    public SteamAppInfoReader()
        : this(File.OpenRead, File.Exists)
    {
    }

    public SteamAppInfoReader(Func<string, Stream> openRead)
        : this(openRead, _ => true)
    {
    }

    private SteamAppInfoReader(
        Func<string, Stream> openRead,
        Func<string, bool> exists)
    {
        _openRead = openRead ?? throw new ArgumentNullException(nameof(openRead));
        _exists = exists ?? throw new ArgumentNullException(nameof(exists));
    }

    public SteamAppInfoEntry? Find(string path, uint appId, string? expectedName = null)
    {
        var entries = FindMany(path, [appId]);
        return entries.TryGetValue(appId, out var entry) ? entry : null;
    }

    public IReadOnlyDictionary<uint, SteamAppInfoEntry> FindMany(
        string path,
        IEnumerable<uint> appIds)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(appIds);

        var requested = appIds.ToHashSet();
        if (requested.Count == 0 || !_exists(path))
            return new Dictionary<uint, SteamAppInfoEntry>();

        var matches = new Dictionary<uint, SteamAppInfoEntry>();
        try
        {
            using var stream = _openRead(path);
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
            var rawMagic = reader.ReadUInt32();
            var version = (byte)(rawMagic & 0xFF);
            if ((rawMagic >> 8) != BaseMagic || version < 40) return matches;
            _ = reader.ReadUInt32();
            var stringTableOffset = reader.ReadInt64();
            if (stringTableOffset < 0 || stringTableOffset >= stream.Length) return matches;
            stream.Position = stringTableOffset;
            var count = reader.ReadUInt32();
            var strings = new List<string>((int)Math.Min(count, 100_000));
            for (var i = 0; i < count; i++) strings.Add(ReadCString(reader));
            var table = new StringTable(strings);
            stream.Position = 16;
            while (stream.Position + 8 <= stream.Length)
            {
                var currentId = reader.ReadUInt32();
                if (currentId == 0) break;
                var size = reader.ReadUInt32();
                var entryEnd = checked(stream.Position + size);
                if (entryEnd > stream.Length || size < 60) break;
                _ = reader.ReadUInt32();
                _ = reader.ReadUInt32();
                _ = reader.ReadUInt64();
                reader.ReadBytes(20);
                _ = reader.ReadUInt32();
                reader.ReadBytes(20);

                if (!requested.Contains(currentId))
                {
                    stream.Position = entryEnd;
                    continue;
                }

                var options = new KVSerializerOptions { StringTable = table };
                var document = KVSerializer.Create(KVSerializationFormat.KeyValues1Binary).Deserialize(stream, options);
                if (stream.Position != entryEnd) break;
                matches[currentId] = ReadEntry(currentId, document);
                if (matches.Count == requested.Count) break;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or EndOfStreamException or OverflowException or KeyValueException)
        {
            // Local Steam metadata is optional; preserve exact matches parsed before a malformed tail.
        }

        return matches;
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
