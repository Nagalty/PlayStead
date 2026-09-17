using PlayStead.Core.Catalog;
using PlayStead.Core.Library;

namespace PlayStead.Core.Identity;

public sealed record GameIdentityDecision(
    IdentityDecisionId DecisionId,
    GameId GameId,
    CatalogContentId CatalogContentId,
    IdentityDecisionType DecisionType,
    DateTimeOffset CreatedUtc,
    DateTimeOffset UpdatedUtc,
    DateTimeOffset? RevokedUtc);
