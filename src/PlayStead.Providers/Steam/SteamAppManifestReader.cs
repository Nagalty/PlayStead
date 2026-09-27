using PlayStead.Core.Library;
using PlayStead.Core.ProviderActivity;
using PlayStead.Providers.Steam.ValveText;

namespace PlayStead.Providers.Steam;

public sealed class SteamAppManifestReader
{
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
}
