using PlayStead.Core.Catalog;

namespace PlayStead.Core.Persistence;

public interface ICanonicalCatalogStore
{
    Task<CatalogMetadata> GetMetadataAsync(CancellationToken cancellationToken);

    Task<CatalogContent?> GetByIdAsync(
        CatalogContentId contentId,
        CancellationToken cancellationToken);

    Task<CatalogContent?> GetByPublicIdAsync(
        PlaySteadPublicId publicId,
        CancellationToken cancellationToken);

    Task<CatalogContent?> FindByProviderRefAsync(
        CatalogProviderKind provider,
        string externalId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<CatalogProviderRef>> GetProviderRefsAsync(
        CatalogContentId contentId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<CatalogAlias>> GetAliasesAsync(
        CatalogContentId contentId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<CatalogContentRelation>> GetRelationsFromAsync(
        CatalogContentId sourceContentId,
        CancellationToken cancellationToken);
}
