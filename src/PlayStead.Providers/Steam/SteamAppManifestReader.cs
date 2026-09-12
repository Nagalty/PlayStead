using PlayStead.Core.Library;
using PlayStead.Providers.Steam.ValveText;

namespace PlayStead.Providers.Steam;

public sealed class SteamAppManifestReader
{
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
