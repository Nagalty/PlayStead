using PlayStead.Core.Library;
using PlayStead.Core.Catalog;
using System.Text.RegularExpressions;

namespace PlayStead.Core.LocalEnvironment;

public enum LocalEnvironmentEntryKind
{
    Save,
    Config,
    Log,
    Mod,
    Other
}

public enum LocalEnvironmentPathKind
{
    File,
    Directory,
    Pattern
}

public enum LocalEnvironmentConfidence
{
    Confirmed,
    Detected,
    Unknown
}

public enum LocalEnvironmentSource
{
    Provider,
    Catalog,
    CommunityRegistry,
    Heuristic,
    User,
    SystemDetection
}

public enum LocalEnvironmentCloudProvider
{
    None,
    Unknown,
    SteamCloud,
    EpicCloud,
    GogCloud,
    Other
}

public enum LocalEnvironmentCustomizationState
{
    VanillaConfirmed,
    Customized,
    Indeterminate
}

public sealed record LocalEnvironmentEntry(
    Guid Id,
    InstallationId InstallationId,
    LocalEnvironmentEntryKind Kind,
    string Path,
    LocalEnvironmentPathKind PathKind,
    LocalEnvironmentConfidence Confidence,
    LocalEnvironmentSource Source,
    bool Exists,
    DateTimeOffset LastObservedAtUtc,
    DateTimeOffset? LastModifiedUtc = null,
    IReadOnlyDictionary<string, string>? Metadata = null,
    bool IsUserOverride = false);

public sealed record GameLocalEnvironment(
    InstallationId InstallationId,
    DateTimeOffset DiscoveredAtUtc,
    DateTimeOffset RefreshedAtUtc,
    IReadOnlyList<LocalEnvironmentEntry> Entries);

public sealed record LocalEnvironmentRule(
    CatalogContentId CatalogContentId,
    LocalEnvironmentEntryKind Kind,
    string PathTemplate,
    LocalEnvironmentPathKind PathKind,
    LocalEnvironmentConfidence Confidence,
    LocalEnvironmentSource Source);

public static class LocalEnvironmentPathNormalizer
{
    public static string Normalize(string path, GameInstallation installation)
    {
        if (!TryNormalize(path, installation, out var normalized))
            throw new FormatException($"The local environment path contains an unknown variable: '{path}'.");
        return normalized;
    }

    public static bool TryNormalize(string path, GameInstallation installation, out string normalized)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var expanded = path.Trim().Replace('/', '\\');
        expanded = expanded.Replace("<INSTALL_ROOT>", installation.InstallRootPath ?? installation.InstallPath, StringComparison.OrdinalIgnoreCase);
        expanded = ReplaceKnownVariable(expanded, "DOCUMENTS", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
        expanded = ReplaceKnownVariable(expanded, "SAVEDGAMES", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Saved Games"));
        expanded = Environment.ExpandEnvironmentVariables(expanded);
        if (Regex.IsMatch(expanded, "%[^%]+%"))
        {
            normalized = path;
            return false;
        }

        if (!Path.IsPathRooted(expanded))
            expanded = Path.Combine(installation.InstallRootPath ?? installation.InstallPath, expanded);

        normalized = Path.GetFullPath(expanded);
        return true;
    }

    private static string ReplaceKnownVariable(string value, string name, string replacement) =>
        string.IsNullOrWhiteSpace(replacement) ? value : value.Replace($"%{name}%", replacement, StringComparison.OrdinalIgnoreCase);
}
