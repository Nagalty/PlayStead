using PlayStead.Core.Library;
using PlayStead.Core.ProviderActivity;

namespace PlayStead.Providers.Steam;

public sealed class SteamLocalProviderActivitySource : IProviderActivityMetadataSource
{
    private readonly WindowsSteamRootLocator _rootLocator;
    private readonly SteamLibraryFoldersReader _folders;
    private readonly SteamAppManifestReader _manifests;
    private readonly SteamProcessLogSessionImporter? _processLogImporter;

    public SteamLocalProviderActivitySource(
        WindowsSteamRootLocator rootLocator,
        SteamLibraryFoldersReader folders,
        SteamAppManifestReader manifests,
        SteamProcessLogSessionImporter? processLogImporter = null)
    { _rootLocator = rootLocator; _folders = folders; _manifests = manifests; _processLogImporter = processLogImporter; }
    public ProviderKind Provider => ProviderKind.Steam;

    public async Task<IReadOnlyList<ProviderActivityMetadata>> GetAsync(IReadOnlyCollection<GameInstallation> installations, CancellationToken cancellationToken)
    {
        var byExternal = installations.ToDictionary(x => x.ExternalId, StringComparer.Ordinal);
        var root = _rootLocator.TryLocate();
        if (root is null) return Array.Empty<ProviderActivityMetadata>();
        if (_processLogImporter is not null)
            await _processLogImporter.ImportAsync(installations, cancellationToken);
        var result = new List<ProviderActivityMetadata>();
        var now = DateTimeOffset.UtcNow;
        foreach (var library in _folders.Read(root))
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var path in Directory.EnumerateFiles(Path.Combine(library, "steamapps"), "appmanifest_*.acf"))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var appId = Path.GetFileNameWithoutExtension(path)["appmanifest_".Length..];
                    if (byExternal.TryGetValue(appId, out var installation))
                        result.Add(_manifests.ReadActivity(path, installation.GameId, now));
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
                catch (FormatException) { }
            }
        }
        return result;
    }
}
