using PlayStead.Core.Library;
using PlayStead.Core.Modding;

namespace PlayStead.Core.Tests.Modding;

public sealed class ModEvidenceTests
{
    [Fact]
    public void No_evidence_is_unknown_and_there_is_no_not_modded_state()
    {
        Assert.Equal(ModDetectionState.Unknown, ModEvidenceAggregation.GetState([]));
        Assert.DoesNotContain("NotModded", Enum.GetNames<ModDetectionState>());
    }

    [Fact]
    public void Workshop_evidence_is_possible_but_confirmed_evidence_wins()
    {
        var game = GameId.New();
        var now = DateTimeOffset.UtcNow;
        var evidence = new[]
        {
            new ModEvidence(game, ProviderKind.Steam, "steam-workshop-content", ModEvidenceKind.WorkshopContentPresent, ModDetectionState.PossiblyModded, now),
            new ModEvidence(game, ProviderKind.Steam, "game-loader", ModEvidenceKind.ModLoaderPresent, ModDetectionState.ConfirmedModded, now)
        };

        Assert.Equal(ModDetectionState.ConfirmedModded, ModEvidenceAggregation.GetState(evidence, now));
    }

    [Fact]
    public void Future_evidence_is_not_current()
    {
        var evidence = new ModEvidence(GameId.New(), ProviderKind.Steam, "detector", ModEvidenceKind.ModFilesDetected, ModDetectionState.ConfirmedModded, DateTimeOffset.UtcNow.AddMinutes(1));
        Assert.Equal(ModDetectionState.Unknown, ModEvidenceAggregation.GetState([evidence], DateTimeOffset.UtcNow));
    }
}
