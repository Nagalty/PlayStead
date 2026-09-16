using PlayStead.Core.Catalog;
using PlayStead.Core.Library;

namespace PlayStead.Core.Identity;

public interface ILocalIdentityReconciler
{
    Task ReconcileAsync(
        GameId localGameId,
        CatalogContentId canonicalContentId,
        IdentityResolutionEvidence evidence,
        DateTimeOffset observedAtUtc,
        CancellationToken cancellationToken);
}
