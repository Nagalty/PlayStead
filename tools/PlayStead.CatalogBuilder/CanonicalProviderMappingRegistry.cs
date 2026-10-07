using PlayStead.Core.Catalog;

namespace PlayStead.CatalogBuilder;

/// <summary>
/// Maintained, reviewed cross-provider mappings. Entries are keyed by an
/// existing deterministic provider reference; no title matching is involved.
/// </summary>
public static class CanonicalProviderMappingRegistry
{
    private static readonly IReadOnlyDictionary<(CatalogProviderKind Provider, string ExternalId), IReadOnlyList<CanonicalCatalogProviderReference>> Mappings =
        new Dictionary<(CatalogProviderKind, string), IReadOnlyList<CanonicalCatalogProviderReference>>
        {
            [(CatalogProviderKind.Steam, "1715130")] =
            [
                new(CatalogProviderKind.Gog, "1103900211", "product-id", CatalogProvenance.AdminConfirmed, CatalogConfidence.Deterministic, DateTimeOffset.UnixEpoch)
            ],
            [(CatalogProviderKind.Epic, "581c8d4fd9574884bff66cbdbaa42def")] =
            [
                new(CatalogProviderKind.Steam, "686810", "app-id", CatalogProvenance.AdminConfirmed, CatalogConfidence.Deterministic, DateTimeOffset.UnixEpoch)
            ]
        };

    public static IReadOnlyList<CanonicalCatalogProviderReference> AddVerifiedMappings(
        IEnumerable<CanonicalCatalogProviderReference> providerReferences)
    {
        ArgumentNullException.ThrowIfNull(providerReferences);
        var result = providerReferences.ToList();
        foreach (var reference in result.ToArray())
        {
            if (!Mappings.TryGetValue((reference.Provider, reference.ExternalId), out var mappings))
                continue;

            foreach (var mapping in mappings)
            {
                if (!result.Any(x => x.Provider == mapping.Provider && x.ExternalId.Equals(mapping.ExternalId, StringComparison.OrdinalIgnoreCase)))
                    result.Add(mapping);
            }
        }

        return result;
    }
}
