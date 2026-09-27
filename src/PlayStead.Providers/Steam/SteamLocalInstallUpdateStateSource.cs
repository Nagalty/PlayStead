using PlayStead.Core.Library;
using PlayStead.Core.ProviderInstallUpdate;

namespace PlayStead.Providers.Steam;

public sealed class SteamLocalInstallUpdateStateSource : IProviderInstallUpdateStateSource
{
    private readonly WindowsSteamRootLocator _rootLocator;
    private readonly SteamLibraryFoldersReader _folders;
    private readonly SteamAppManifestReader _manifests;
    private readonly SteamAppInfoReader _appInfo;
    private readonly ProviderInstallUpdateStateEvaluator _evaluator;

    public SteamLocalInstallUpdateStateSource(
        WindowsSteamRootLocator rootLocator,
        SteamLibraryFoldersReader folders,
        SteamAppManifestReader manifests,
        SteamAppInfoReader appInfo,
        ProviderInstallUpdateStateEvaluator evaluator)
    {
        _rootLocator = rootLocator;
        _folders = folders;
        _manifests = manifests;
        _appInfo = appInfo;
        _evaluator = evaluator;
    }

    public ProviderKind Provider => ProviderKind.Steam;

    public Task<IReadOnlyList<ProviderInstallUpdateState>> GetAsync(
        IReadOnlyCollection<GameInstallation> installations,
        CancellationToken cancellationToken)
    {
        var byExternal = installations.ToDictionary(x => x.ExternalId, StringComparer.Ordinal);
        var root = _rootLocator.TryLocate();
        if (root is null)
        {
            var missingObservedAt = DateTimeOffset.UtcNow;
            return Task.FromResult<IReadOnlyList<ProviderInstallUpdateState>>(
                installations
                    .Where(x => x.IsPresent)
                    .Select(x => _evaluator.Evaluate(
                        x.GameId,
                        x.Provider,
                        x.ExternalId,
                        new ProviderInstallUpdateEvidence(null, null, null, null, null, null, null, null),
                        missingObservedAt))
                    .ToArray());
        }

        var observedAt = DateTimeOffset.UtcNow;
        var result = new List<ProviderInstallUpdateState>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var appInfoPath = Path.Combine(root, "appcache", "appinfo.vdf");
        var appInfoById = _appInfo.FindMany(
            appInfoPath,
            byExternal.Keys
                .Where(x => uint.TryParse(x, out _))
                .Select(uint.Parse));
        foreach (var library in _folders.Read(root))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var steamApps = Path.Combine(library, "steamapps");
            foreach (var manifestPath in Directory.EnumerateFiles(steamApps, "appmanifest_*.acf"))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var appId = Path.GetFileNameWithoutExtension(manifestPath)["appmanifest_".Length..];
                    if (!byExternal.TryGetValue(appId, out var installation))
                    {
                        continue;
                    }

                    var evidence = _manifests.ReadInstallUpdateEvidence(manifestPath);
                    if (uint.TryParse(appId, out var numericAppId) &&
                        appInfoById.TryGetValue(numericAppId, out var appInfo))
                    {
                        evidence = evidence with
                        {
                            PublicBuildId = appInfo.PublicBuildId,
                            PublicDepotManifests = appInfo.PublicDepotManifests
                        };
                    }
                    seen.Add(appId);
                    result.Add(_evaluator.Evaluate(
                        installation.GameId,
                        installation.Provider,
                        appId,
                        evidence,
                        observedAt));
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
                catch (FormatException) { }
            }
        }

        foreach (var installation in installations.Where(x => x.IsPresent && !seen.Contains(x.ExternalId)))
        {
            result.Add(_evaluator.Evaluate(
                installation.GameId,
                installation.Provider,
                installation.ExternalId,
                new ProviderInstallUpdateEvidence(null, null, null, null, null, null, null, null),
                observedAt));
        }

        return Task.FromResult<IReadOnlyList<ProviderInstallUpdateState>>(
            result.OrderBy(x => x.ProviderGameId, StringComparer.Ordinal).ToArray());
    }
}
