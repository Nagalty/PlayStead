using System.Globalization;
using PlayStead.Core.Library;
using PlayStead.Core.ProviderGameMetadata;

namespace PlayStead.Providers.Steam;

public sealed class SteamLocalGameMetadataSource : IProviderGameMetadataSource
{
    private readonly WindowsSteamRootLocator _rootLocator;
    private readonly SteamAppInfoReader _appInfo;

    public SteamLocalGameMetadataSource(WindowsSteamRootLocator rootLocator, SteamAppInfoReader appInfo)
    {
        _rootLocator = rootLocator;
        _appInfo = appInfo;
    }

    public ProviderKind Provider => ProviderKind.Steam;

    public Task<IReadOnlyList<ProviderGameMetadataPatch>> GetAsync(LibrarySnapshot snapshot, CancellationToken cancellationToken)
    {
        var root = _rootLocator.TryLocate();
        if (root is null) return Task.FromResult<IReadOnlyList<ProviderGameMetadataPatch>>([]);
        var path = Path.Combine(root, "appcache", "appinfo.vdf");
        var installations = snapshot.Installations.Where(x => x.Provider == ProviderKind.Steam && x.IsPresent).ToArray();
        var ids = installations.Select(x => uint.TryParse(x.ExternalId, out var id) ? id : (uint?)null).Where(x => x.HasValue).Select(x => x!.Value);
        var entries = _appInfo.FindMany(path, ids);
        var result = new List<ProviderGameMetadataPatch>();
        foreach (var installation in installations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!uint.TryParse(installation.ExternalId, NumberStyles.None, CultureInfo.InvariantCulture, out var id) || !entries.TryGetValue(id, out var entry))
                continue;
            result.Add(SteamGameMetadataMapper.Map(entry, installation.GameId, ProviderKind.Steam, DateTimeOffset.UtcNow));
        }
        return Task.FromResult<IReadOnlyList<ProviderGameMetadataPatch>>(result);
    }
}
