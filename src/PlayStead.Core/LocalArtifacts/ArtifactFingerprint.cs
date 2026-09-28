namespace PlayStead.Core.LocalArtifacts;

public sealed record ArtifactFingerprint(
    string Algorithm,
    string Hash,
    int FileCount,
    long TotalSizeBytes,
    DateTimeOffset CapturedAtUtc);

public sealed record ArtifactFingerprintResult(
    ArtifactFingerprint? Fingerprint,
    string? Error)
{
    public bool IsAvailable => Fingerprint is not null;

    public static ArtifactFingerprintResult Unavailable(string error) =>
        new(null, error);
}

public interface IArtifactFingerprintService
{
    Task<ArtifactFingerprintResult> ComputeAsync(
        GameLocalArtifact artifact,
        CancellationToken cancellationToken);
}
