using PlayStead.Core.Library;
using PlayStead.Core.Persistence;

namespace PlayStead.Providers.Steam;

public interface ISteamLocalCatalogImportSource
{
    IReadOnlyList<CanonicalCatalogImportItem> CreateItems(
        LibrarySnapshot snapshot,
        DateTimeOffset observedAtUtc);
}

public sealed class SteamLocalCatalogImportSource : ISteamLocalCatalogImportSource
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
        var discovered = new List<(GameInstallation Local, DiscoveredInstallation Installation)>();
        foreach (var library in _folders.Read(root))
        {
            foreach (var manifest in Directory.EnumerateFiles(Path.Combine(library, "steamapps"), "appmanifest_*.acf"))
            {
                try
                {
                    var installation = _manifests.Read(manifest, library, observedAtUtc);
                    if (!byExternal.TryGetValue(installation.ExternalId, out var local)) continue;
                    discovered.Add((local, installation));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException)
                { }
            }
        }

        var requestedIds = discovered
            .Select(item => uint.TryParse(item.Installation.ExternalId, out var id) ? id : (uint?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value);
        var appInfo = _appInfo.FindMany(appInfoPath, requestedIds);
        var items = new List<CanonicalCatalogImportItem>(discovered.Count);
        foreach (var item in discovered)
        {
            SteamAppInfoEntry? app = null;
            if (uint.TryParse(item.Installation.ExternalId, out var id))
                appInfo.TryGetValue(id, out app);
            items.Add(new CanonicalCatalogImportItem(
                item.Local.GameId,
                item.Installation.ExternalId,
                item.Installation.Title,
                app?.Developer,
                app?.Publisher,
                observedAtUtc));
        }

        return items;
    }
}
