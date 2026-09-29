using PlayStead.Core.Library;

namespace PlayStead.Core.LocalArtifacts;

public sealed class LocalArtifactDiscoveryService : IGameLocalArtifactDiscoveryService
{
    private readonly IReadOnlyList<GameLocalArtifactRule> _rules;
    private readonly IUserDefinedLocalArtifactStore? _userDefinedStore;

    public LocalArtifactDiscoveryService(IEnumerable<GameLocalArtifactRule>? rules = null, IUserDefinedLocalArtifactStore? userDefinedStore = null)
    {
        _rules = (rules ?? []).ToArray();
        _userDefinedStore = userDefinedStore;
    }

    public Task<IReadOnlyList<GameLocalArtifact>> DiscoverAsync(
        GameId gameId,
        CancellationToken cancellationToken)
        => DiscoverAsync(gameId, provider: null, providerGameId: null, cancellationToken);

    public async Task<IReadOnlyList<GameLocalArtifact>> DiscoverAsync(
        GameId gameId,
        ProviderKind? provider,
        string? providerGameId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var artifacts = new List<GameLocalArtifact>();

        foreach (var rule in _rules.Where(rule =>
                     (rule.GameId is null || rule.GameId == gameId) &&
                     (rule.Provider is null || rule.Provider == provider) &&
                     (rule.ProviderGameId is null || string.Equals(rule.ProviderGameId, providerGameId, StringComparison.Ordinal))))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryNormalizePath(rule.PathTemplate, out var path))
                continue;

            var exists = File.Exists(path) || Directory.Exists(path);
            artifacts.Add(new GameLocalArtifact(
                gameId,
                rule.Kind,
                path,
                rule.Source,
                exists ? GameLocalArtifactStatus.KnownAndExists : GameLocalArtifactStatus.KnownButMissing,
                rule.PathTemplate,
                Details: exists ? LocalArtifactDetailsReader.Read(path) : null));
        }

        if (_userDefinedStore is not null)
        {
            var builtInPaths = artifacts
                .Select(artifact => (artifact.Kind, Path: NormalizeForComparison(artifact.Path)))
                .ToHashSet();
            foreach (var custom in await _userDefinedStore.GetByGameAsync(gameId, cancellationToken))
            {
                if (!TryNormalizePath(custom.Path, out var path))
                    continue;
                if (!builtInPaths.Add((custom.Kind, NormalizeForComparison(path))))
                    continue;
                var exists = File.Exists(path) || Directory.Exists(path);
                artifacts.Add(new GameLocalArtifact(
                    gameId,
                    custom.Kind,
                    path,
                    GameLocalArtifactSource.UserDefined,
                    exists ? GameLocalArtifactStatus.KnownAndExists : GameLocalArtifactStatus.KnownButMissing,
                    $"user:{custom.Id:D}",
                    Details: exists ? LocalArtifactDetailsReader.Read(path) : null));
            }
        }

        return artifacts;
    }

    private static bool TryNormalizePath(string template, out string path)
    {
        path = string.Empty;
        if (string.IsNullOrWhiteSpace(template))
            return false;

        try
        {
            var expanded = Environment.ExpandEnvironmentVariables(template.Trim());
            if (!Path.IsPathFullyQualified(expanded))
                return false;

            path = Path.GetFullPath(expanded);
            return Path.IsPathFullyQualified(path);
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }

    private static string NormalizeForComparison(string path) => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
}
