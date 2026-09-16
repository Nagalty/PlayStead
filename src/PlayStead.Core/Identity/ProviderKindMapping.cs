using PlayStead.Core.Catalog;
using PlayStead.Core.Library;

namespace PlayStead.Core.Identity;

public static class ProviderKindMapping
{
    public static bool TryMap(
        ProviderKind provider,
        out CatalogProviderKind catalogProvider)
    {
        if (provider == ProviderKind.Steam)
        {
            catalogProvider = CatalogProviderKind.Steam;
            return true;
        }

        catalogProvider = default;
        return false;
    }
}
