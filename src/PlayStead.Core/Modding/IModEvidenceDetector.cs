using PlayStead.Core.Library;

namespace PlayStead.Core.Modding;

public interface IModEvidenceDetector
{
    string DetectorId { get; }
    Task<IReadOnlyList<ModEvidence>> DetectAsync(GameInstallation installation, CancellationToken cancellationToken);
}
