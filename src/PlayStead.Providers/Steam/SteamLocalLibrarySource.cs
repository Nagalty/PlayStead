using PlayStead.Core.Library;
using PlayStead.Core.Scanning;

namespace PlayStead.Providers.Steam;

public sealed class SteamLocalLibrarySource : ILocalLibrarySource, ISteamEligibleInstallationSnapshot
{
    private readonly WindowsSteamRootLocator _rootLocator;
    private readonly SteamLibraryFoldersReader _foldersReader;
    private readonly SteamAppManifestReader _manifestReader;
    private readonly SteamAppInfoReader _appInfoReader;

    public SteamLocalLibrarySource(
        WindowsSteamRootLocator rootLocator,
        SteamLibraryFoldersReader foldersReader,
        SteamAppManifestReader manifestReader,
        SteamAppInfoReader appInfoReader)
    {
        _rootLocator = rootLocator;
        _foldersReader = foldersReader;
        _manifestReader = manifestReader;
        _appInfoReader = appInfoReader;
    }

    public ProviderKind Provider => ProviderKind.Steam;
    public IReadOnlySet<string>? EligibleExternalIds { get; private set; }

    public Task<SourceScanResult> ScanAsync(
        CancellationToken cancellationToken)
    {
        var observedAtUtc = DateTimeOffset.UtcNow;
        var root = _rootLocator.TryLocate();

        if (root is null)
        {
            EligibleExternalIds = new HashSet<string>(StringComparer.Ordinal);
            return Task.FromResult(
                SourceScanResult.Success(
                    Provider,
                    observedAtUtc,
                    Array.Empty<DiscoveredInstallation>()));
        }

        var found = new List<DiscoveredInstallation>();
        var warnings = new List<string>();
        var manifests = new List<(string Path, string LibraryRoot)>();

        foreach (var libraryRoot in _foldersReader.Read(root))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var steamApps = Path.Combine(
                libraryRoot,
                "steamapps");

            foreach (var manifest in Directory.EnumerateFiles(
                         steamApps,
                         "appmanifest_*.acf",
                         SearchOption.TopDirectoryOnly))
            {
                cancellationToken.ThrowIfCancellationRequested();
                manifests.Add((manifest, libraryRoot));
            }
        }

        var appInfoPath = Path.Combine(root, "appcache", "appinfo.vdf");
        var appIds = manifests
            .Select(x => Path.GetFileNameWithoutExtension(x.Path)?["appmanifest_".Length..])
            .Where(x => uint.TryParse(x, out _))
            .Select(x => uint.Parse(x!))
            .ToArray();
        var appInfo = _appInfoReader.FindMany(appInfoPath, appIds);

        foreach (var (manifest, libraryRoot) in manifests)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var item = _manifestReader.Read(
                    manifest,
                    libraryRoot,
                    observedAtUtc);

                SteamAppInfoEntry? entry = null;
                var kind = uint.TryParse(item.ExternalId, out var appId) &&
                    appInfo.TryGetValue(appId, out entry)
                    ? MapContentKind(entry.Type)
                    : InstallationContentKind.Unknown;
                item = item with { ContentKind = kind };

                if (kind.IsGameEligible() &&
                    Directory.Exists(item.InstallPath) &&
                    HasDeclaredWindowsLaunchTarget(item.InstallPath, entry))
                {
                    found.Add(item);
                }
                else if (!kind.IsGameEligible())
                {
                    System.Diagnostics.Trace.WriteLine(
                        $"[DISCOVERY] Skipped non-game installation: {item.ExternalId}/{item.Title} type={entry?.Type ?? "unknown"}");
                }
                else if (kind.IsGameEligible() && entry?.LaunchConfigurations is not null)
                {
                    System.Diagnostics.Trace.WriteLine(
                        $"[STEAM-DISCOVERY] Installation rejected: appid={item.ExternalId} reason=missing-launch-target");
                }
            }
            catch (Exception ex) when (
                ex is IOException
                or UnauthorizedAccessException
                or FormatException)
            {
                warnings.Add(
                    $"{Path.GetFileName(manifest)}: {ex.GetType().Name}");
            }
        }

        var ordered = found
            .OrderBy(
                x => x.Title,
                StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(
                x => x.ExternalId,
                StringComparer.Ordinal)
            .ToArray();

        EligibleExternalIds = ordered
            .Select(x => x.ExternalId)
            .ToHashSet(StringComparer.Ordinal);

        return Task.FromResult(
            SourceScanResult.Success(
                Provider,
                observedAtUtc,
                ordered,
                warnings));
    }

    private static InstallationContentKind MapContentKind(string? type) =>
        type?.Trim().ToLowerInvariant() switch
        {
            "game" => InstallationContentKind.Game,
            "tool" => InstallationContentKind.Tool,
            "application" => InstallationContentKind.Application,
            "driver" => InstallationContentKind.Driver,
            "sdk" => InstallationContentKind.Sdk,
            "runtime" => InstallationContentKind.Runtime,
            _ => InstallationContentKind.Unknown
        };

    private static bool HasDeclaredWindowsLaunchTarget(
        string installPath,
        SteamAppInfoEntry? entry)
    {
        // Missing appinfo/launch data remains conservative: preserve the existing
        // manifest + directory behavior when Steam has not supplied launch data.
        if (entry?.LaunchConfigurations is null)
            return true;

        var windows = entry.LaunchConfigurations
            .Where(x => string.IsNullOrWhiteSpace(x.OsList) ||
                        x.OsList.Contains("windows", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (windows.Length == 0)
            return true;

        return windows.Any(x =>
        {
            var relative = x.Executable
                .Replace('/', Path.DirectorySeparatorChar)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var path = Path.GetFullPath(Path.Combine(installPath, relative));
            return File.Exists(path);
        });
    }
}
