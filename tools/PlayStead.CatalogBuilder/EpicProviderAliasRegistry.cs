using PlayStead.Core.Catalog;

namespace PlayStead.CatalogBuilder;

public static class EpicProviderAliasRegistry
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<CanonicalCatalogProviderReference>> Aliases =
        new Dictionary<string, IReadOnlyList<CanonicalCatalogProviderReference>>(StringComparer.OrdinalIgnoreCase)
        {
            ["1ff858e1c36b45c9a54ca66c4279dbb1"] =
            [
                Alias("581c8d4fd9574884bff66cbdbaa42def", "catalog-item"),
                Alias("6430e58041234e41b8f81f68f01450ed", "namespace"),
                Alias("3e02273b543f4ff0a1c24d3b534a9ac3", "app-name")
            ]
        };

    public static IReadOnlyList<CanonicalCatalogProviderReference> AddProvenAliases(
        IEnumerable<CanonicalCatalogProviderReference> providerReferences)
    {
        ArgumentNullException.ThrowIfNull(providerReferences);
        var references = providerReferences.ToList();
        foreach (var existing in references.Where(x => x.Provider == CatalogProviderKind.Epic).ToArray())
        {
            if (!Aliases.TryGetValue(existing.ExternalId, out var aliases))
                continue;

            foreach (var alias in aliases)
            {
                if (references.Any(x => x.Provider == alias.Provider && x.ExternalId.Equals(alias.ExternalId, StringComparison.OrdinalIgnoreCase)))
                    continue;
                references.Add(alias);
            }
        }

        return references;
    }

    private static CanonicalCatalogProviderReference Alias(string externalId, string externalType) =>
        new(CatalogProviderKind.Epic, externalId, externalType, CatalogProvenance.AdminConfirmed, CatalogConfidence.Deterministic, DateTimeOffset.UnixEpoch);
}
