using PlayStead.Core.Graphics;
using PlayStead.Core.Library;

namespace PlayStead.Core.Tests.Graphics;

public sealed class GraphicsTechnologySupportTests
{
    [Fact]
    public void Support_evidence_preserves_source_and_observation_time()
    {
        var observedAt = DateTimeOffset.UtcNow;
        var evidence = new GraphicsTechnologySupportEvidence(
            GameId.New(),
            GraphicsTechnology.FSR,
            GraphicsSupportStatus.Supported,
            GraphicsTechnologySupportSourceKind.OfficialVendor,
            "AMD",
            observedAt);

        Assert.Equal(GraphicsTechnologySupportSourceKind.OfficialVendor, evidence.SourceKind);
        Assert.Equal("AMD", evidence.SourceName);
        Assert.Equal(observedAt, evidence.ObservedAtUtc);
    }

    [Fact]
    public void Observation_can_carry_support_provenance_without_changing_runtime_claims()
    {
        var observedAt = DateTimeOffset.UtcNow;
        var evidence = new GraphicsTechnologySupportEvidence(
            GameId.New(),
            GraphicsTechnology.FrameGeneration,
            GraphicsSupportStatus.Supported,
            GraphicsTechnologySupportSourceKind.OfficialVendor,
            "AMD",
            observedAt);
        var observation = new GraphicsTechnologyObservation(
            evidence.GameId,
            GraphicsTechnology.FrameGeneration,
            GraphicsSupportStatus.Supported,
            GraphicsRuntimeStatus.Unknown,
            GraphicsActivationStatus.Unknown,
            null,
            [],
            SupportEvidence: [evidence]);

        Assert.Equal(GraphicsSupportStatus.Supported, observation.SupportStatus);
        Assert.Equal(GraphicsRuntimeStatus.Unknown, observation.RuntimeStatus);
        Assert.Equal(GraphicsActivationStatus.Unknown, observation.ActivationStatus);
        Assert.Single(observation.SupportEvidenceList);
    }
}
