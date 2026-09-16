namespace PlayStead.Core.Catalog;

public sealed record CatalogContentRelation(
    CatalogContentId SourceContentId,
    CatalogRelationKind RelationKind,
    CatalogContentId TargetContentId,
    CatalogProvenance Provenance,
    CatalogConfidence Confidence);
