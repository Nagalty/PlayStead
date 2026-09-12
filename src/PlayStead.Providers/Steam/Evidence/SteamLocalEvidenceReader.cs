using PlayStead.Core.Steam;
using PlayStead.Providers.Steam.ValveText;

namespace PlayStead.Providers.Steam.Evidence;

public sealed class SteamLocalEvidenceReader
{
    public SteamLocalEvidence Read(
        string manifestPath,
        DateTimeOffset observedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);

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

        var buildId = OptionalString(
            appState,
            "buildid");

        var branchName = ReadBranchName(appState);

        var depots = ReadInstalledDepots(appState);

        return new SteamLocalEvidence(
            appId,
            buildId,
            branchName,
            depots,
            observedAtUtc);
    }

    private static string ReadBranchName(
        IReadOnlyDictionary<string, object> appState)
    {
        if (appState.TryGetValue("UserConfig", out var userConfigValue) &&
            userConfigValue is IReadOnlyDictionary<string, object> userConfig &&
            userConfig.TryGetValue("BetaKey", out var betaKeyValue) &&
            betaKeyValue is string betaKey &&
            !string.IsNullOrWhiteSpace(betaKey))
        {
            return betaKey;
        }

        return "public";
    }

    private static IReadOnlyDictionary<string, string> ReadInstalledDepots(
        IReadOnlyDictionary<string, object> appState)
    {
        if (!appState.TryGetValue(
                "InstalledDepots",
                out var installedDepotsValue) ||
            installedDepotsValue is not
                IReadOnlyDictionary<string, object> installedDepots)
        {
            return new Dictionary<string, string>(
                StringComparer.Ordinal);
        }

        var result = new SortedDictionary<string, string>(
            StringComparer.Ordinal);

        foreach (var pair in installedDepots)
        {
            if (!pair.Key.All(char.IsAsciiDigit) ||
                pair.Value is not
                    IReadOnlyDictionary<string, object> depotBlock ||
                !depotBlock.TryGetValue(
                    "manifest",
                    out var manifestValue) ||
                manifestValue is not string manifestId ||
                string.IsNullOrWhiteSpace(manifestId))
            {
                continue;
            }

            result[pair.Key] = manifestId;
        }

        return new Dictionary<string, string>(
            result,
            StringComparer.Ordinal);
    }

    private static string? OptionalString(
        IReadOnlyDictionary<string, object> block,
        string key)
    {
        if (block.TryGetValue(key, out var value) &&
            value is string text &&
            !string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        return null;
    }

    private static string Required(
        IReadOnlyDictionary<string, object> block,
        string key,
        string manifestPath)
    {
        var value = OptionalString(block, key);

        if (value is not null)
        {
            return value;
        }

        throw new FormatException(
            $"Steam manifest '{manifestPath}' is missing '{key}'.");
    }
}
