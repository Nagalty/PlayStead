namespace PlayStead.Core.Steam;

public sealed class SteamUpdateStateEvaluator
{
    public SteamUpdateEvaluation Evaluate(
        SteamLocalEvidence local,
        SteamRemoteEvidenceResult remote,
        DateTimeOffset evaluatedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(local);
        ArgumentNullException.ThrowIfNull(remote);

        if (string.IsNullOrWhiteSpace(local.BranchName))
        {
            return Unknown(
                SteamUpdateReason.LocalBranchUnknown,
                evaluatedAtUtc);
        }

        if (remote.Status == SteamRemoteEvidenceStatus.BranchUnavailable)
        {
            return Unknown(
                SteamUpdateReason.RemoteBranchUnavailable,
                evaluatedAtUtc);
        }

        if (remote.Status == SteamRemoteEvidenceStatus.RefreshFailed &&
            remote.Evidence is null)
        {
            return Unknown(
                SteamUpdateReason.RemoteRefreshFailedWithoutCache,
                evaluatedAtUtc);
        }

        var evidence = remote.Evidence;

        if (evidence is null)
        {
            return Unknown(
                SteamUpdateReason.EvidenceContradictory,
                evaluatedAtUtc);
        }

        if (!string.Equals(
                local.AppId,
                evidence.AppId,
                StringComparison.Ordinal))
        {
            return Unknown(
                SteamUpdateReason.EvidenceContradictory,
                evaluatedAtUtc);
        }

        if (!string.Equals(
                local.BranchName,
                evidence.BranchName,
                StringComparison.OrdinalIgnoreCase))
        {
            return Unknown(
                SteamUpdateReason.EvidenceContradictory,
                evaluatedAtUtc);
        }

        if (local.DepotManifestIds.Count == 0)
        {
            return Unknown(
                SteamUpdateReason.LocalDepotEvidenceMissing,
                evaluatedAtUtc);
        }

        var buildChanged = HasChangedBuild(
            local.BuildId,
            evidence.BuildId);

        if (evidence.DepotManifestIds.Count == 0)
        {
            if (buildChanged)
            {
                return new SteamUpdateEvaluation(
                    SteamUpdateState.NewVersionDetected,
                    SteamUpdateReason.RemoteBuildChangedWithIncompleteDepotEvidence,
                    evaluatedAtUtc,
                    Array.Empty<string>());
            }

            return Unknown(
                SteamUpdateReason.RemoteDepotEvidenceMissing,
                evaluatedAtUtc);
        }

        var missingRemoteDepot = local.DepotManifestIds.Keys.Any(
            depotId => !evidence.DepotManifestIds.ContainsKey(depotId));

        if (missingRemoteDepot)
        {
            return Unknown(
                SteamUpdateReason.RemoteDepotEvidenceMissing,
                evaluatedAtUtc);
        }

        var changedDepotIds = local.DepotManifestIds
            .Where(pair =>
                !string.Equals(
                    pair.Value,
                    evidence.DepotManifestIds[pair.Key],
                    StringComparison.Ordinal))
            .Select(pair => pair.Key)
            .OrderBy(depotId => depotId, StringComparer.Ordinal)
            .ToArray();

        if (changedDepotIds.Length > 0)
        {
            return new SteamUpdateEvaluation(
                SteamUpdateState.UpdateAvailable,
                SteamUpdateReason.DepotManifestMismatch,
                evaluatedAtUtc,
                changedDepotIds);
        }

        if (buildChanged)
        {
            return new SteamUpdateEvaluation(
                SteamUpdateState.NewVersionDetected,
                SteamUpdateReason.RemoteBuildChangedWithoutDepotDifference,
                evaluatedAtUtc,
                Array.Empty<string>());
        }

        return new SteamUpdateEvaluation(
            SteamUpdateState.UpToDate,
            SteamUpdateReason.DepotManifestsMatch,
            evaluatedAtUtc,
            Array.Empty<string>());
    }

    private static bool HasChangedBuild(
        string? localBuildId,
        string? remoteBuildId)
    {
        if (string.IsNullOrWhiteSpace(localBuildId) ||
            string.IsNullOrWhiteSpace(remoteBuildId))
        {
            return false;
        }

        return !string.Equals(
            localBuildId,
            remoteBuildId,
            StringComparison.Ordinal);
    }

    private static SteamUpdateEvaluation Unknown(
        SteamUpdateReason reason,
        DateTimeOffset evaluatedAtUtc)
        => new(
            SteamUpdateState.Unknown,
            reason,
            evaluatedAtUtc,
            Array.Empty<string>());
}
