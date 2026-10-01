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
        var genres = FindCollection(root, "genres");
        var categories = FindCollection(root, "categories");
        var type = FindCommonValue(root, "type");
        var launchConfigurations = FindLaunchConfigurations(root);
        var mediaAssets = FindMediaAssets(root);
        var releaseDate = FindValue(root, "date");
        var releaseReported = FindNode(root, "release_date") is not null;
        var isFreeText = FindValue(root, "is_free");
        bool? isFree = isFreeText switch
        {
            "1" or "true" => true,
            "0" or "false" => false,
            _ => null
        };
        return new SteamAppInfoEntry(
            appId,
            FindValue(root, "developer"),
            FindValue(root, "publisher"),
            genres,
            categories,
            releaseDate,
            releaseReported,
            isFree,
            FindCollection(root, "developers"),
            FindCollection(root, "publishers"),
            FindPathValue(root, "branches", "public", "buildid"),
            FindPublicDepotManifests(root),
            type,
            launchConfigurations,
            mediaAssets);
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

    private static string? FindCommonValue(KVObject root, string key)
    {
        var common = FindNode(root, "common");
        return common is null ? null : Read(common, key);
    }

    private static IReadOnlyList<SteamLaunchConfiguration>? FindLaunchConfigurations(KVObject root)
    {
        var config = root.TryGetValue("config", out var rawConfig) &&
            rawConfig.ValueType == KVValueType.Collection
            ? rawConfig
            : null;
        if (config is null ||
            !config.TryGetValue("launch", out var rawLaunch) ||
            rawLaunch.ValueType != KVValueType.Collection)
        {
            return null;
        }

        var result = new List<SteamLaunchConfiguration>();
        foreach (var entry in rawLaunch)
        {
            if (entry.Value.ValueType != KVValueType.Collection)
                continue;

            var executable = Read(entry.Value, "executable");
            if (string.IsNullOrWhiteSpace(executable))
                continue;

            var launchConfig = entry.Value.TryGetValue("config", out var rawEntryConfig) &&
                rawEntryConfig.ValueType == KVValueType.Collection
                ? rawEntryConfig
                : null;

            result.Add(new SteamLaunchConfiguration(
                executable.Trim(),
                Read(entry.Value, "workingdir")?.Trim(),
                launchConfig is null ? null : Read(launchConfig, "oslist")?.Trim()));
        }

        return result;
    }

    private static SteamMediaAssetMetadata? FindMediaAssets(KVObject root)
    {
        var common = root.TryGetValue("common", out var rawCommon) &&
            rawCommon.ValueType == KVValueType.Collection
            ? rawCommon
            : null;
        if (common is null ||
            !common.TryGetValue("library_assets", out var rawAssets) ||
            rawAssets.ValueType != KVValueType.Collection)
        {
            return null;
        }

        var capsuleHash = Read(rawAssets, "library_capsule");
        var libraryHash = Read(rawAssets, "library_600x900")
            ?? Read(rawAssets, "library_600x900_2x");
        return string.IsNullOrWhiteSpace(capsuleHash) && string.IsNullOrWhiteSpace(libraryHash)
            ? null
            : new SteamMediaAssetMetadata(capsuleHash, libraryHash);
    }

    private static string? FindPathValue(KVObject root, params string[] path)
    {
        if (TryFindPathValue(root, path, 0, out var value))
        {
            return value;
        }

        return null;
    }

    private static bool TryFindPathValue(
        KVObject node,
        IReadOnlyList<string> path,
        int index,
        out string? value)
    {
        if (index >= path.Count)
        {
            value = null;
            return false;
        }

        if (index == path.Count - 1 && node.TryGetValue(path[index], out var leaf))
        {
            value = leaf.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return true;
        }

        if (index < path.Count &&
            node.TryGetValue(path[index], out var child) &&
            child.ValueType == KVValueType.Collection &&
            TryFindPathValue(child, path, index + 1, out value))
        {
            return true;
        }

        foreach (var pair in node)
        {
            if (pair.Value.ValueType == KVValueType.Collection &&
                TryFindPathValue(pair.Value, path, index, out value))
            {
                return true;
            }
        }

        value = null;
        return false;
    }

    private static IReadOnlyDictionary<string, string>? FindPublicDepotManifests(KVObject root)
    {
        if (!root.TryGetValue("depots", out var rawDepots) ||
            rawDepots.ValueType != KVValueType.Collection)
        {
            return null;
        }

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var depot in rawDepots)
        {
            if (depot.Value.ValueType != KVValueType.Collection ||
                !depot.Value.TryGetValue("manifests", out var rawManifests) ||
                rawManifests.ValueType != KVValueType.Collection ||
                !rawManifests.TryGetValue("public", out var rawPublic) ||
                rawPublic.ValueType != KVValueType.Collection)
            {
                continue;
            }

            var gid = ReadAny(rawPublic, "gid");
            if (!string.IsNullOrWhiteSpace(gid))
            {
                result[depot.Key] = gid.Trim();
            }
        }

        return result;
    }

    private static string? ReadAny(KVObject root, string key) =>
        root.TryGetValue(key, out var value)
            ? value.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : null;

    private static KVObject? FindNode(KVObject node, string key)
    {
        if (node.TryGetValue(key, out var value) && value.ValueType == KVValueType.Collection)
            return value;
        foreach (var pair in node)
        {
            var found = FindNode(pair.Value, key);
            if (found is not null) return found;
        }
        return null;
    }

    private static IReadOnlyList<string>? FindCollection(KVObject root, string key)
    {
        var node = FindNode(root, key);
        if (node is null) return null;
        var values = new List<string>();
        foreach (var pair in node)
        {
            var description = FindValue(pair.Value, "description") ?? Read(pair.Value, "value");
            if (!string.IsNullOrWhiteSpace(description)) values.Add(description.Trim());
        }
        return values.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static string ReadCString(BinaryReader reader)
    {
        var bytes = new List<byte>();
        int value;
        while ((value = reader.Read()) > 0) bytes.Add((byte)value);
        return Encoding.UTF8.GetString(bytes.ToArray());
    }
}
