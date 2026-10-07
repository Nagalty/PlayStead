using PlayStead.Core.Catalog;
using PlayStead.Core.Library;

namespace PlayStead.Core.Identity;

public static class ProviderKindMapping
{
    public static bool TryMap(
        ProviderKind provider,
        out CatalogProviderKind catalogProvider)
    {
        if (provider is ProviderKind.Steam or ProviderKind.Epic or ProviderKind.Gog)
        {
            catalogProvider = provider switch
            {
                ProviderKind.Steam => CatalogProviderKind.Steam,
                ProviderKind.Epic => CatalogProviderKind.Epic,
                ProviderKind.Gog => CatalogProviderKind.Gog,
                _ => throw new ArgumentOutOfRangeException(nameof(provider))
            };
            return true;
        }

        catalogProvider = default;
        return false;
    }
}
