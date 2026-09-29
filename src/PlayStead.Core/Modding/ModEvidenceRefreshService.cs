using PlayStead.Core.Library;

namespace PlayStead.Core.Modding;

public sealed class ModEvidenceRefreshService(
    IModEvidenceStore store,
    IEnumerable<IModEvidenceDetector> detectors)
{
    public async Task RefreshAsync(GameInstallation installation, CancellationToken cancellationToken)
    {
        var detectorList = detectors.ToArray();
        foreach (var detector in detectorList)
        {
            var evidences = await detector.DetectAsync(installation, cancellationToken).ConfigureAwait(false);
            var matching = evidences.FirstOrDefault(evidence => evidence.GameId == installation.GameId);
            if (matching is null)
            {
                foreach (var evidenceKind in Enum.GetValues<ModEvidenceKind>())
                    await store.RemoveAsync(installation.GameId, detector.DetectorId, evidenceKind, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                var current = evidences.Where(evidence => evidence.GameId == installation.GameId).ToArray();
                foreach (var evidence in current)
                    await store.UpsertAsync(evidence, cancellationToken).ConfigureAwait(false);

                var presentKinds = current.Select(evidence => evidence.EvidenceKind).ToHashSet();
                foreach (var evidenceKind in Enum.GetValues<ModEvidenceKind>().Where(kind => !presentKinds.Contains(kind)))
                    await store.RemoveAsync(installation.GameId, detector.DetectorId, evidenceKind, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public async Task<IReadOnlyDictionary<GameId, ModDetectionState>> GetStatesAsync(CancellationToken cancellationToken)
    {
        var all = await store.GetAllAsync(cancellationToken).ConfigureAwait(false);
        return all.GroupBy(evidence => evidence.GameId)
            .ToDictionary(group => group.Key, group => ModEvidenceAggregation.GetState(group));
    }
}
