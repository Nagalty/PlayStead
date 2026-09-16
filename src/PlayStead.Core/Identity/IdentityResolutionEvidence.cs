using PlayStead.Core.Catalog;

namespace PlayStead.Core.Identity;

public sealed record IdentityResolutionEvidence(
    IdentityResolutionEvidenceKind Kind,
    CatalogProviderKind? Provider,
    string ExternalId,
    CatalogContentId? MatchedContentId);
