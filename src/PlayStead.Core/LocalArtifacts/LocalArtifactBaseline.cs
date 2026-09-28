using PlayStead.Core.Library;

namespace PlayStead.Core.LocalArtifacts;

public sealed record LocalArtifactBaseline(
    GameId GameId,
    GameLocalArtifactKind Kind,
    string ArtifactIdentity,
    string Algorithm,
    string Hash,
    int FileCount,
    long TotalSizeBytes,
    DateTimeOffset CapturedAtUtc);

public enum LocalArtifactBaselineStatus
{
    NoBaseline,
    Unchanged,
    Changed,
    Missing,
    Unavailable
}

public interface ILocalArtifactBaselineStore
{
    Task<LocalArtifactBaseline?> GetAsync(GameId gameId, GameLocalArtifactKind kind, string artifactIdentity, CancellationToken cancellationToken);
    Task UpsertAsync(LocalArtifactBaseline baseline, CancellationToken cancellationToken);
}

public sealed class LocalArtifactBaselineComparisonService
{
    public LocalArtifactBaselineStatus Compare(GameLocalArtifact artifact, ArtifactFingerprintResult current, LocalArtifactBaseline? baseline)
    {
        if (!current.IsAvailable)
            return artifact.Status == GameLocalArtifactStatus.KnownButMissing
                ? LocalArtifactBaselineStatus.Missing
                : LocalArtifactBaselineStatus.Unavailable;
        if (baseline is null)
            return LocalArtifactBaselineStatus.NoBaseline;
        return string.Equals(baseline.Hash, current.Fingerprint!.Hash, StringComparison.OrdinalIgnoreCase)
            ? LocalArtifactBaselineStatus.Unchanged
            : LocalArtifactBaselineStatus.Changed;
    }
}
