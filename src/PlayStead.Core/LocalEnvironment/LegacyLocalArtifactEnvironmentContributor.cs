using PlayStead.Core.Library;
using PlayStead.Core.LocalArtifacts;

namespace PlayStead.Core.LocalEnvironment;

/// <summary>Bridges the trusted 0.4.x artifact rules and user folders into the installation-scoped model.</summary>
public sealed class LegacyLocalArtifactEnvironmentContributor : ILocalEnvironmentContributor
{
    private readonly IReadOnlyList<GameLocalArtifactRule> _rules;
    private readonly IUserDefinedLocalArtifactStore _users;

    public LegacyLocalArtifactEnvironmentContributor(IEnumerable<GameLocalArtifactRule> rules, IUserDefinedLocalArtifactStore users)
    {
        _rules = rules.ToArray();
        _users = users;
    }

    public async Task<IReadOnlyList<LocalEnvironmentEntry>> DiscoverAsync(GameInstallation installation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entries = new List<LocalEnvironmentEntry>();
        foreach (var rule in _rules.Where(x => Matches(x, installation)))
        {
            entries.Add(new LocalEnvironmentEntry(Guid.NewGuid(), installation.Id, ToKind(rule.Kind), rule.PathTemplate, rule.PathKind, LocalEnvironmentConfidence.Confirmed, LocalEnvironmentSource.Catalog, false, DateTimeOffset.MinValue));
        }

        foreach (var user in await _users.GetByGameAsync(installation.GameId, cancellationToken).ConfigureAwait(false))
        {
            entries.Add(new LocalEnvironmentEntry(user.Id, installation.Id, ToKind(user.Kind), user.Path, LocalEnvironmentPathKind.Directory, LocalEnvironmentConfidence.Confirmed, LocalEnvironmentSource.User, false, user.CreatedAtUtc, Metadata: new Dictionary<string, string> { ["DisplayName"] = user.DisplayName ?? string.Empty }, IsUserOverride: true));
        }
        return entries;
    }

    private static bool Matches(GameLocalArtifactRule rule, GameInstallation installation) =>
        (rule.GameId is null || rule.GameId == installation.GameId) &&
        (rule.Provider is null || rule.Provider == installation.Provider) &&
        (rule.ProviderGameId is null || string.Equals(rule.ProviderGameId, installation.ExternalId, StringComparison.Ordinal));

    private static LocalEnvironmentEntryKind ToKind(GameLocalArtifactKind kind) => kind switch
    {
        GameLocalArtifactKind.Configuration => LocalEnvironmentEntryKind.Config,
        GameLocalArtifactKind.SaveData => LocalEnvironmentEntryKind.Save,
        GameLocalArtifactKind.Log => LocalEnvironmentEntryKind.Log,
        _ => LocalEnvironmentEntryKind.Other
    };
}
