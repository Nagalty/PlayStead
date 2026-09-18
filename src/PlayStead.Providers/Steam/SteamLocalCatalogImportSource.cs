using PlayStead.Core.Library;
using PlayStead.Core.Persistence;

namespace PlayStead.Providers.Steam;

public sealed class SteamLocalCatalogImportSource
{
    private readonly WindowsSteamRootLocator _rootLocator;
    private readonly SteamLibraryFoldersReader _folders;
    private readonly SteamAppManifestReader _manifests;
    private readonly SteamAppInfoReader _appInfo;

    public SteamLocalCatalogImportSource(WindowsSteamRootLocator rootLocator, SteamLibraryFoldersReader folders, SteamAppManifestReader manifests, SteamAppInfoReader appInfo)
    { _rootLocator = rootLocator; _folders = folders; _manifests = manifests; _appInfo = appInfo; }

    public IReadOnlyList<CanonicalCatalogImportItem> CreateItems(LibrarySnapshot snapshot, DateTimeOffset observedAtUtc)
    {
        var root = _rootLocator.TryLocate();
        if (root is null) return Array.Empty<CanonicalCatalogImportItem>();
        var appInfoPath = Path.Combine(root, "appcache", "appinfo.vdf");
        var byExternal = snapshot.Installations.Where(x => x.Provider == ProviderKind.Steam).ToDictionary(x => x.ExternalId, StringComparer.Ordinal);
        var items = new List<CanonicalCatalogImportItem>();
        foreach (var library in _folders.Read(root))
        {
            foreach (var manifest in Directory.EnumerateFiles(Path.Combine(library, "steamapps"), "appmanifest_*.acf"))
            {
                try
                {
                    var installation = _manifests.Read(manifest, library, observedAtUtc);
                    if (!byExternal.TryGetValue(installation.ExternalId, out var local)) continue;
                    var app = uint.TryParse(installation.ExternalId, out var id) ? _appInfo.Find(appInfoPath, id, installation.Title) : null;
                    items.Add(new CanonicalCatalogImportItem(local.GameId, installation.ExternalId, installation.Title, app?.Developer, app?.Publisher, observedAtUtc));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException)
                { }
            }
        }
        return items;
    }
}
