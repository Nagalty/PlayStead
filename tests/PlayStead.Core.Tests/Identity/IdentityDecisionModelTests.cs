using PlayStead.Core.Catalog;
using PlayStead.Core.Identity;
using PlayStead.Core.Library;
using PlayStead.Core.Persistence;

namespace PlayStead.Core.Tests.Identity;

public sealed class IdentityDecisionModelTests
{
    [Fact]
    public void Enum_values_are_stable()
    {
        Assert.Equal(1, (int)IdentityDecisionType.UserConfirmed);
        Assert.Equal(2, (int)IdentityDecisionType.UserRejected);
    }

    [Fact]
    public void Decision_round_trips_active_and_revoked_state()
    {
        var gameId = GameId.New();
        var contentId = CatalogContentId.New();
        var created = DateTimeOffset.UtcNow;
        var decision = new GameIdentityDecision(
            IdentityDecisionId.New(), gameId, contentId,
            IdentityDecisionType.UserConfirmed, created, created, null);

        Assert.Equal(gameId, decision.GameId);
        Assert.Equal(contentId, decision.CatalogContentId);
        Assert.Equal(created, decision.CreatedUtc);
        Assert.Equal(created, decision.UpdatedUtc);
        Assert.Null(decision.RevokedUtc);

        var revoked = decision with { RevokedUtc = created.AddMinutes(1) };
        Assert.NotNull(revoked.RevokedUtc);
    }

    [Fact]
    public void Both_decision_types_are_representable()
    {
        var now = DateTimeOffset.UtcNow;
        var gameId = GameId.New();
        var contentId = CatalogContentId.New();
        var confirmed = new GameIdentityDecision(IdentityDecisionId.New(), gameId, contentId, IdentityDecisionType.UserConfirmed, now, now, null);
        var rejected = confirmed with { DecisionId = IdentityDecisionId.New(), DecisionType = IdentityDecisionType.UserRejected };

        Assert.Equal(IdentityDecisionType.UserConfirmed, confirmed.DecisionType);
        Assert.Equal(IdentityDecisionType.UserRejected, rejected.DecisionType);
    }

    [Fact]
    public void Contracts_compile_with_expected_signatures()
    {
        IIdentityDecisionStore store = null!;
        IIdentityDecisionService service = null!;
        Assert.Null(store);
        Assert.Null(service);
    }
}
