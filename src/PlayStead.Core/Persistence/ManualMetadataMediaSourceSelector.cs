using PlayStead.Core.Catalog;
using PlayStead.Core.Library;
using PlayStead.Core.Media;

namespace PlayStead.Core.Persistence;

public static class ManualMetadataMediaSourceSelector
{
    public static MediaSourceIdentity? Select(IEnumerable<CatalogProviderRef> providerRefs)
    {
        ArgumentNullException.ThrowIfNull(providerRefs);

        var steamRefs = providerRefs
            .Where(x => x.Provider == CatalogProviderKind.Steam)
            .Where(x => x.Confidence == CatalogConfidence.Deterministic)
            .ToArray();

        return steamRefs.Length == 1
            ? new MediaSourceIdentity(ProviderKind.Steam, steamRefs[0].ExternalId)
            : null;
    }
}
