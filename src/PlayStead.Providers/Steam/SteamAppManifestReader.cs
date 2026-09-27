using PlayStead.Core.Library;
using PlayStead.Core.ProviderActivity;
using PlayStead.Core.ProviderInstallUpdate;
using PlayStead.Providers.Steam.ValveText;

namespace PlayStead.Providers.Steam;

public sealed class SteamAppManifestReader
{
    public ProviderInstallUpdateEvidence ReadInstallUpdateEvidence(string manifestPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);

        var root = ValveTextParser.Parse(File.ReadAllText(manifestPath));
        if (!root.TryGetValue("AppState", out var raw) || raw is not IReadOnlyDictionary<string, object> app)
        {
            throw new FormatException($"Steam manifest '{manifestPath}' has no AppState block.");
        }

        return new ProviderInstallUpdateEvidence(
            OptionalString(app, "buildid"),
            OptionalString(app, "TargetBuildID"),
            OptionalString(app, "BytesToDownload"),
            OptionalString(app, "BytesDownloaded"),
            OptionalString(app, "BytesToStage"),
            OptionalString(app, "BytesStaged"),
            OptionalString(app, "StagingSize"),
            ParseInt(app, "StateFlags"),
            null,
            InstalledDepotManifests(app));
    }

    public ProviderActivityMetadata ReadActivity(
        string manifestPath,
        GameId gameId,
        DateTimeOffset observedAtUtc)
    {
        var root = ValveTextParser.Parse(File.ReadAllText(manifestPath));
        if (!root.TryGetValue("AppState", out var raw) || raw is not IReadOnlyDictionary<string, object> app)
        {
            throw new FormatException($"Steam manifest '{manifestPath}' has no AppState block.");
        }
        var appId = Required(app, "appid", manifestPath);
        long? minutes = null;
        if (app.TryGetValue("playtime_forever", out var rawMinutes) && rawMinutes is string text && long.TryParse(text, out var parsed) && parsed >= 0)
        {
            minutes = parsed;
        }
        DateTimeOffset? lastPlayed = null;
        if (app.TryGetValue("LastPlayed", out var rawLast) && rawLast is string lastText && long.TryParse(lastText, out var unix) && unix > 0)
        {
            lastPlayed = DateTimeOffset.FromUnixTimeSeconds(unix);
        }
        var availability = minutes.HasValue || lastPlayed.HasValue
            ? ProviderActivityAvailability.Complete
            : ProviderActivityAvailability.Unknown;
        return new ProviderActivityMetadata(gameId, ProviderKind.Steam, appId,
            minutes.HasValue ? TimeSpan.FromMinutes(minutes.Value) : null,
            lastPlayed, observedAtUtc, availability);
    }

    public DiscoveredInstallation Read(
        string manifestPath,
        string libraryRoot,
        DateTimeOffset observedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(libraryRoot);

        var root = ValveTextParser.Parse(
            File.ReadAllText(manifestPath));

        if (!root.TryGetValue("AppState", out var appStateValue) ||
            appStateValue is not IReadOnlyDictionary<string, object> appState)
        {
            throw new FormatException(
                $"Steam manifest '{manifestPath}' has no AppState block.");
        }

        var appId = Required(
            appState,
            "appid",
            manifestPath);

        var name = Required(
            appState,
            "name",
            manifestPath);

        var installDir = Required(
            appState,
            "installdir",
            manifestPath);

        long? installedSizeBytes = null;

        if (appState.TryGetValue("SizeOnDisk", out var rawSize) &&
            rawSize is string sizeText &&
            long.TryParse(sizeText, out var parsedSize) &&
            parsedSize >= 0)
        {
            installedSizeBytes = parsedSize;
        }

        var installPath = Path.Combine(
            libraryRoot,
            "steamapps",
            "common",
            installDir);

        return DiscoveredInstallation.Create(
            ProviderKind.Steam,
            appId,
            name,
            installPath,
            installedSizeBytes,
            observedAtUtc);
    }

    private static string Required(
        IReadOnlyDictionary<string, object> block,
        string key,
        string manifestPath)
    {
        if (block.TryGetValue(key, out var value) &&
            value is string text &&
            !string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        throw new FormatException(
            $"Steam manifest '{manifestPath}' is missing '{key}'.");
    }

    private static string? OptionalString(
        IReadOnlyDictionary<string, object> block,
        string key) =>
        block.TryGetValue(key, out var value) &&
        value is string text &&
        !string.IsNullOrWhiteSpace(text)
            ? text
            : null;

    private static int? ParseInt(
        IReadOnlyDictionary<string, object> block,
        string key) =>
        int.TryParse(OptionalString(block, key), out var value)
            ? value
            : null;

    private static IReadOnlyDictionary<string, string>? InstalledDepotManifests(
        IReadOnlyDictionary<string, object> app)
    {
        if (!app.TryGetValue("InstalledDepots", out var raw) ||
            raw is not IReadOnlyDictionary<string, object> depots)
        {
            return null;
        }

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var depot in depots)
        {
            if (depot.Value is IReadOnlyDictionary<string, object> values &&
                values.TryGetValue("manifest", out var manifest) &&
                manifest is string text &&
                !string.IsNullOrWhiteSpace(text))
            {
                result[depot.Key] = text.Trim();
            }
        }

        return result;
    }
}
