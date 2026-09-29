using PlayStead.Core.Library;

namespace PlayStead.Core.LocalArtifacts;

public sealed class UserDefinedLocalArtifactService(
    IUserDefinedLocalArtifactStore store,
    IEnumerable<GameLocalArtifactRule>? builtInRules = null)
{
    private readonly IReadOnlyList<GameLocalArtifactRule> _builtInRules = (builtInRules ?? []).ToArray();

    public async Task<UserDefinedLocalArtifact> AddAsync(GameId gameId, GameLocalArtifactKind kind, string path, string? displayName, CancellationToken cancellationToken)
        => await AddAsync(gameId, null, null, kind, path, displayName, cancellationToken);

    public async Task<UserDefinedLocalArtifact> AddAsync(
        GameId gameId,
        ProviderKind? provider,
        string? providerGameId,
        GameLocalArtifactKind kind,
        string path,
        string? displayName,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!TryNormalizeExistingDirectory(path, out var normalized))
            throw new ArgumentException("A valid existing absolute directory is required.", nameof(path));

        var existing = await store.GetByGameAsync(gameId, cancellationToken);
        if (existing.Any(item => item.Kind == kind && string.Equals(NormalizeForComparison(item.Path), NormalizeForComparison(normalized), StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("This folder is already known for this game and type.");

        if (_builtInRules.Any(rule =>
                rule.Kind == kind &&
                (rule.GameId is null || rule.GameId == gameId) &&
                (rule.Provider is null || rule.Provider == provider) &&
                (rule.ProviderGameId is null || string.Equals(rule.ProviderGameId, providerGameId, StringComparison.Ordinal)) &&
                TryExpandRulePath(rule.PathTemplate, out var builtInPath) &&
                string.Equals(NormalizeForComparison(builtInPath), NormalizeForComparison(normalized), StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("This folder is already known by PlayStead.");

        var artifact = new UserDefinedLocalArtifact(Guid.NewGuid(), gameId, kind, normalized, string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim(), DateTimeOffset.UtcNow);
        await store.AddAsync(artifact, cancellationToken);
        return artifact;
    }

    public Task RemoveAsync(Guid id, CancellationToken cancellationToken) => store.RemoveAsync(id, cancellationToken);

    private static bool TryNormalizeExistingDirectory(string? value, out string path)
    {
        path = string.Empty;
        if (string.IsNullOrWhiteSpace(value)) return false;
        try
        {
            var full = Path.GetFullPath(value.Trim());
            if (!Path.IsPathFullyQualified(full) || !Directory.Exists(full)) return false;
            path = full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return true;
        }
        catch (ArgumentException) { return false; }
        catch (NotSupportedException) { return false; }
    }

    private static string NormalizeForComparison(string path) => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private static bool TryExpandRulePath(string template, out string path)
    {
        path = string.Empty;
        try
        {
            var expanded = Environment.ExpandEnvironmentVariables(template);
            var full = Path.GetFullPath(expanded);
            if (!Path.IsPathFullyQualified(full)) return false;
            path = full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return true;
        }
        catch (ArgumentException) { return false; }
        catch (NotSupportedException) { return false; }
    }
}
