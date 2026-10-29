using PlayStead.Core.Catalog;
using PlayStead.Core.Library;

namespace PlayStead.Core.LocalEnvironment;

public sealed class RuleBasedLocalEnvironmentContributor : ILocalEnvironmentContributor
{
    private readonly IReadOnlyList<LocalEnvironmentRule> _rules;
    private readonly Func<GameInstallation, CatalogContentId?> _catalogResolver;

    public RuleBasedLocalEnvironmentContributor(IEnumerable<LocalEnvironmentRule> rules, Func<GameInstallation, CatalogContentId?> catalogResolver)
    {
        _rules = rules.ToArray();
        _catalogResolver = catalogResolver;
    }

    public Task<IReadOnlyList<LocalEnvironmentEntry>> DiscoverAsync(GameInstallation installation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var contentId = _catalogResolver(installation);
        if (contentId is null)
            return Task.FromResult<IReadOnlyList<LocalEnvironmentEntry>>([]);

        var entries = _rules.Where(rule => rule.CatalogContentId == contentId.Value)
            .Select(rule => new LocalEnvironmentEntry(Guid.NewGuid(), installation.Id, rule.Kind, rule.PathTemplate, rule.PathKind, rule.Confidence, rule.Source, false, DateTimeOffset.MinValue))
            .ToArray();
        return Task.FromResult<IReadOnlyList<LocalEnvironmentEntry>>(entries);
    }
}
