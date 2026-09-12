using PlayStead.Core.Library;
using PlayStead.Core.Scanning;

namespace PlayStead.Providers.Steam;

public sealed class SteamLocalLibrarySource : ILocalLibrarySource
{
    private readonly WindowsSteamRootLocator _rootLocator;
    private readonly SteamLibraryFoldersReader _foldersReader;
    private readonly SteamAppManifestReader _manifestReader;

    public SteamLocalLibrarySource(
        WindowsSteamRootLocator rootLocator,
        SteamLibraryFoldersReader foldersReader,
        SteamAppManifestReader manifestReader)
    {
        _rootLocator = rootLocator;
        _foldersReader = foldersReader;
        _manifestReader = manifestReader;
    }

    public ProviderKind Provider => ProviderKind.Steam;

    public Task<SourceScanResult> ScanAsync(
        CancellationToken cancellationToken)
    {
        var observedAtUtc = DateTimeOffset.UtcNow;
        var root = _rootLocator.TryLocate();

        if (root is null)
        {
            return Task.FromResult(
                SourceScanResult.Success(
                    Provider,
                    observedAtUtc,
                    Array.Empty<DiscoveredInstallation>()));
        }

        var found = new List<DiscoveredInstallation>();
        var warnings = new List<string>();

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

                try
                {
                    var item = _manifestReader.Read(
                        manifest,
                        libraryRoot,
                        observedAtUtc);

                    if (Directory.Exists(item.InstallPath))
                    {
                        found.Add(item);
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
        }

        var ordered = found
            .OrderBy(
                x => x.Title,
                StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(
                x => x.ExternalId,
                StringComparer.Ordinal)
            .ToArray();

        return Task.FromResult(
            SourceScanResult.Success(
                Provider,
                observedAtUtc,
                ordered,
                warnings));
    }
}
