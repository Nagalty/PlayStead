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
                await store.RemoveAsync(installation.GameId, detector.DetectorId, ModEvidenceKind.WorkshopContentPresent, cancellationToken).ConfigureAwait(false);
            else
                await store.UpsertAsync(matching, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<IReadOnlyDictionary<GameId, ModDetectionState>> GetStatesAsync(CancellationToken cancellationToken)
    {
        var all = await store.GetAllAsync(cancellationToken).ConfigureAwait(false);
        return all.GroupBy(evidence => evidence.GameId)
            .ToDictionary(group => group.Key, group => ModEvidenceAggregation.GetState(group));
    }
}
