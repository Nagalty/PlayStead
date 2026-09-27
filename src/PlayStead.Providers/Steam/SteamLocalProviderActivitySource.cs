using PlayStead.Core.Library;
using PlayStead.Core.ProviderActivity;

namespace PlayStead.Providers.Steam;

public sealed class SteamLocalProviderActivitySource : IProviderActivityMetadataSource
{
    private readonly WindowsSteamRootLocator _rootLocator;
    private readonly SteamLibraryFoldersReader _folders;
    private readonly SteamAppManifestReader _manifests;

    public SteamLocalProviderActivitySource(WindowsSteamRootLocator rootLocator, SteamLibraryFoldersReader folders, SteamAppManifestReader manifests)
    { _rootLocator = rootLocator; _folders = folders; _manifests = manifests; }
    public ProviderKind Provider => ProviderKind.Steam;

    public Task<IReadOnlyList<ProviderActivityMetadata>> GetAsync(IReadOnlyCollection<GameInstallation> installations, CancellationToken cancellationToken)
    {
        var byExternal = installations.ToDictionary(x => x.ExternalId, StringComparer.Ordinal);
        var root = _rootLocator.TryLocate();
        if (root is null) return Task.FromResult<IReadOnlyList<ProviderActivityMetadata>>(Array.Empty<ProviderActivityMetadata>());
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
        return Task.FromResult<IReadOnlyList<ProviderActivityMetadata>>(result);
    }
}
