namespace PlayStead.Core.Catalog;

public sealed record CatalogContent(
    CatalogContentId Id,
    PlaySteadPublicId PublicId,
    CatalogContentKind Kind,
    string CanonicalTitle,
    string NormalizedTitle,
    DateOnly? ReleaseDate,
    string? Developer,
    string? Publisher,
    CatalogContentStatus Status,
    CatalogContentId? RedirectTargetId,
    IReadOnlyList<string>? Genres = null,
    CatalogMedia? Media = null);
