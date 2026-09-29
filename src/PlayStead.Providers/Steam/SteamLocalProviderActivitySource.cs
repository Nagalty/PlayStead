using PlayStead.Core.Library;
using PlayStead.Core.ProviderActivity;

namespace PlayStead.Providers.Steam;

public sealed class SteamLocalProviderActivitySource : IProviderActivityMetadataSource
{
    private readonly WindowsSteamRootLocator _rootLocator;
    private readonly SteamLibraryFoldersReader _folders;
    private readonly SteamAppManifestReader _manifests;
    private readonly SteamLocalConfigActivityReader _localConfig;
    private readonly SteamProcessLogSessionImporter? _processLogImporter;

    public SteamLocalProviderActivitySource(
        WindowsSteamRootLocator rootLocator,
        SteamLibraryFoldersReader folders,
        SteamAppManifestReader manifests,
        SteamLocalConfigActivityReader? localConfig = null,
        SteamProcessLogSessionImporter? processLogImporter = null)
    {
        _rootLocator = rootLocator;
        _folders = folders;
        _manifests = manifests;
        _localConfig = localConfig ?? new SteamLocalConfigActivityReader();
        _processLogImporter = processLogImporter;
    }
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
        var localConfig = _localConfig.Read(root, byExternal.Keys.ToArray());
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
                    {
                        var manifestActivity = _manifests.ReadActivity(path, installation.GameId, now);
                        if (localConfig.TryGetValue(appId, out var localLifetime))
                        {
                            result.Add(manifestActivity with
                            {
                                TotalPlaytime = localLifetime.Total,
                                Availability = ProviderActivityAvailability.Complete
                            });
                        }
                        else
                        {
                            result.Add(manifestActivity);
                        }
                        System.Diagnostics.Trace.WriteLine(
                            $"[STEAM-PLAYTIME] AppId={appId} ManifestPlaytime={Format(manifestActivity.TotalPlaytime)} " +
                            $"LocalConfigPlaytime={Format(localLifetime: localConfig.GetValueOrDefault(appId))} " +
                            $"Selected={Format(localConfig.TryGetValue(appId, out var selected) ? selected.Total : manifestActivity.TotalPlaytime)}");
                    }
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
                catch (FormatException) { }
            }
        }
        return result;
    }

    private static string Format(TimeSpan? value) =>
        value is { } duration ? ((long)duration.TotalMinutes).ToString() : "<none>";

    private static string Format(SteamLocalConfigLifetime? localLifetime) =>
        localLifetime is null
            ? "<none>"
            : $"{localLifetime.PlaytimeMinutes}+{localLifetime.PlaytimeDisconnectedMinutes}";
}
