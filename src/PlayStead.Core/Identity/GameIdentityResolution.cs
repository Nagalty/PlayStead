using PlayStead.Core.Catalog;
using PlayStead.Core.Library;

namespace PlayStead.Core.Identity;

public sealed record GameIdentityResolution(
    GameId GameId,
    ProvisionalIdentityId? ProvisionalIdentityId,
    IdentityResolutionState State,
    CatalogContentId? CandidateContentId,
    IdentityResolutionEvidence Evidence,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
