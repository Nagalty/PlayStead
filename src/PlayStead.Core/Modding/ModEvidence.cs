using PlayStead.Core.Library;

namespace PlayStead.Core.Modding;

public sealed record ModEvidence(
    GameId GameId,
    ProviderKind Provider,
    string DetectorId,
    ModEvidenceKind EvidenceKind,
    ModDetectionState State,
    DateTimeOffset ObservedAtUtc,
    string? Detail = null)
{
    public bool IsConfirmed => State == ModDetectionState.ConfirmedModded;
}

public static class ModEvidenceAggregation
{
    public static ModDetectionState GetState(IEnumerable<ModEvidence> evidences, DateTimeOffset? nowUtc = null)
    {
        ArgumentNullException.ThrowIfNull(evidences);
        var now = nowUtc ?? DateTimeOffset.UtcNow;
        var current = evidences.Where(evidence => evidence.ObservedAtUtc <= now).ToArray();
        if (current.Any(evidence => evidence.State == ModDetectionState.ConfirmedModded))
            return ModDetectionState.ConfirmedModded;
        if (current.Any(evidence => evidence.State == ModDetectionState.PossiblyModded))
            return ModDetectionState.PossiblyModded;
        return ModDetectionState.Unknown;
    }
}

public interface IModEvidenceStore
{
    Task<IReadOnlyList<ModEvidence>> GetByGameAsync(GameId gameId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ModEvidence>> GetAllAsync(CancellationToken cancellationToken);
    Task UpsertAsync(ModEvidence evidence, CancellationToken cancellationToken);
    Task RemoveAsync(GameId gameId, string detectorId, ModEvidenceKind evidenceKind, CancellationToken cancellationToken);
}
