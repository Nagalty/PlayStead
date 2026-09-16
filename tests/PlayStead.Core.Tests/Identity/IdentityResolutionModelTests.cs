using System.Text.Json;
using PlayStead.Core.Catalog;
using PlayStead.Core.Identity;
using PlayStead.Core.Library;

namespace PlayStead.Core.Tests.Identity;

public sealed class IdentityResolutionModelTests
{
    private static readonly DateTimeOffset FixedTimestamp =
        new(2026, 9, 16, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Provisional_id_uses_strict_canonical_format_and_round_trips()
    {
        var id = ProvisionalIdentityId.New(FixedTimestamp);

        Assert.StartsWith("PS-TEMP-01M2NP0980", id.Value);
        Assert.Equal(34, id.Value.Length);
        Assert.All(
            id.Value[8..],
            character => Assert.Contains(
                character,
                "0123456789ABCDEFGHJKMNPQRSTVWXYZ"));
        Assert.InRange(id.Value[8], '0', '7');
        Assert.Equal(id, ProvisionalIdentityId.Parse(id.Value));
        Assert.Equal(id.Value, id.ToString());
    }

    [Fact]
    public void Generated_provisional_ids_are_distinct()
    {
        var first = ProvisionalIdentityId.New(FixedTimestamp);
        var second = ProvisionalIdentityId.New(FixedTimestamp);

        Assert.NotEqual(first, second);
    }

    [Theory]
    [InlineData("")]
    [InlineData("PS-TEMP-")]
    [InlineData("PS-TEMP-0000000000000000000000000")]
    [InlineData("PS-TEMP-000000000000000000000000000")]
    [InlineData("PS-TEMP-80000000000000000000000000")]
    [InlineData("PS-TEMP-0000000000000000000000000I")]
    [InlineData("PS-TEMP-0000000000000000000000000L")]
    [InlineData("PS-TEMP-0000000000000000000000000O")]
    [InlineData("PS-TEMP-0000000000000000000000000U")]
    [InlineData("ps-temp-00000000000000000000000000")]
    [InlineData("PS-TEMP-0000000000000000000000000a")]
    [InlineData("PLAYSTEAD-TEMP-00000000000000000000000000")]
    public void Invalid_provisional_ids_are_rejected(string value)
    {
        Assert.False(ProvisionalIdentityId.TryParse(value, out _));
        Assert.Throws<FormatException>(
            () => ProvisionalIdentityId.Parse(value));
    }

    [Fact]
    public void Resolution_state_values_are_frozen_for_sqlite()
    {
        Assert.Equal(1, (int)IdentityResolutionState.MatchConfirmed);
        Assert.Equal(2, (int)IdentityResolutionState.MatchProbable);
        Assert.Equal(3, (int)IdentityResolutionState.Ambiguous);
        Assert.Equal(4, (int)IdentityResolutionState.New);
    }

    [Fact]
    public void Evidence_kind_values_are_frozen_for_json()
    {
        Assert.Equal(1, (int)IdentityResolutionEvidenceKind.ExactProviderRef);
        Assert.Equal(2, (int)IdentityResolutionEvidenceKind.NoExactProviderRefMatch);
    }

    [Fact]
    public void Evidence_json_uses_numeric_enum_values_and_round_trips()
    {
        var contentId = new CatalogContentId(
            Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var evidence = new IdentityResolutionEvidence(
            IdentityResolutionEvidenceKind.ExactProviderRef,
            CatalogProviderKind.Steam,
            "2479810",
            contentId);

        var json = JsonSerializer.Serialize(evidence);
        var restored =
            JsonSerializer.Deserialize<IdentityResolutionEvidence>(json);

        Assert.Contains("\"Kind\":1", json);
        Assert.Contains("\"Provider\":1", json);
        Assert.Contains("\"ExternalId\":\"2479810\"", json);
        Assert.Equal(evidence, restored);
    }

    [Fact]
    public void Resolution_models_preserve_observation_candidate_and_evidence()
    {
        var contentId = CatalogContentId.New();
        var evidence = new IdentityResolutionEvidence(
            IdentityResolutionEvidenceKind.ExactProviderRef,
            CatalogProviderKind.Steam,
            "1874880",
            contentId);
        var observation = new GameIdentityObservation(
            CatalogProviderKind.Steam,
            "1874880",
            "Arma Reforger",
            FixedTimestamp);
        var result = new IdentityResolutionResult(
            IdentityResolutionState.MatchConfirmed,
            contentId,
            evidence);

        Assert.Equal(CatalogProviderKind.Steam, observation.Provider);
        Assert.Equal(contentId, result.CandidateContentId);
        Assert.Equal(evidence, result.Evidence);
    }

    [Fact]
    public void Direct_confirmed_resolution_allows_no_provisional_id()
    {
        var gameId = GameId.New();
        var contentId = CatalogContentId.New();
        var evidence = new IdentityResolutionEvidence(
            IdentityResolutionEvidenceKind.ExactProviderRef,
            CatalogProviderKind.Steam,
            "1874880",
            contentId);

        var resolution = new GameIdentityResolution(
            gameId,
            ProvisionalIdentityId: null,
            IdentityResolutionState.MatchConfirmed,
            contentId,
            evidence,
            FixedTimestamp,
            FixedTimestamp);

        Assert.Equal(gameId, resolution.GameId);
        Assert.Null(resolution.ProvisionalIdentityId);
        Assert.Equal(contentId, resolution.CandidateContentId);
    }
}
