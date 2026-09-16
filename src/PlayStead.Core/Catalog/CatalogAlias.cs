namespace PlayStead.Core.Catalog;

public sealed record CatalogAlias(
    CatalogContentId ContentId,
    string Alias,
    string NormalizedAlias,
    CatalogProvenance Provenance);
