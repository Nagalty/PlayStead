using PlayStead.Core.Library;

namespace PlayStead.Core.LocalArtifacts;

public sealed class LocalArtifactDiscoveryService : IGameLocalArtifactDiscoveryService
{
    private readonly IReadOnlyList<GameLocalArtifactRule> _rules;

    public LocalArtifactDiscoveryService(IEnumerable<GameLocalArtifactRule>? rules = null)
    {
        _rules = (rules ?? []).ToArray();
    }

    public Task<IReadOnlyList<GameLocalArtifact>> DiscoverAsync(
        GameId gameId,
        CancellationToken cancellationToken)
        => DiscoverAsync(gameId, provider: null, providerGameId: null, cancellationToken);

    public Task<IReadOnlyList<GameLocalArtifact>> DiscoverAsync(
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

            artifacts.Add(new GameLocalArtifact(
                gameId,
                rule.Kind,
                path,
                rule.Source,
                Directory.Exists(path)
                    ? GameLocalArtifactStatus.KnownAndExists
                    : GameLocalArtifactStatus.KnownButMissing));
        }

        return Task.FromResult<IReadOnlyList<GameLocalArtifact>>(artifacts);
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
}
