using PlayStead.Core.Catalog;

namespace PlayStead.Core.Identity;

public sealed record IdentityResolutionResult(
    IdentityResolutionState State,
    CatalogContentId? CandidateContentId,
    IdentityResolutionEvidence Evidence);
