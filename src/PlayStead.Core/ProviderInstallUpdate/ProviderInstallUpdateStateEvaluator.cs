using PlayStead.Core.Library;

namespace PlayStead.Core.ProviderInstallUpdate;

public sealed class ProviderInstallUpdateStateEvaluator
{
    public ProviderInstallUpdateState Evaluate(
        GameId gameId,
        ProviderKind provider,
        string providerGameId,
        ProviderInstallUpdateEvidence evidence,
        DateTimeOffset observedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        var toDownload = ParseNonNegative(evidence.BytesToDownload);
        var downloaded = ParseNonNegative(evidence.BytesDownloaded);
        var toStage = ParseNonNegative(evidence.BytesToStage);
        var staged = ParseNonNegative(evidence.BytesStaged);
        var stagingSize = ParseNonNegative(evidence.StagingSize);

        var status = DetermineStatus(
            evidence.InstalledBuildId,
            evidence.TargetBuildId,
            evidence.PublicBuildId,
            evidence.InstalledDepotManifests,
            evidence.PublicDepotManifests,
            toDownload,
            downloaded,
            toStage,
            staged,
            stagingSize);

        return new ProviderInstallUpdateState(
            gameId,
            provider,
            providerGameId,
            Normalize(evidence.InstalledBuildId),
            Normalize(evidence.TargetBuildId),
            status,
            toDownload,
            downloaded,
            toStage,
            staged,
            stagingSize,
            evidence.StateFlags,
            observedAtUtc);
    }

    private static ProviderInstallUpdateStatus DetermineStatus(
        string? installedBuildId,
        string? targetBuildId,
        string? publicBuildId,
        IReadOnlyDictionary<string, string>? installedDepotManifests,
        IReadOnlyDictionary<string, string>? publicDepotManifests,
        long? bytesToDownload,
        long? bytesDownloaded,
        long? bytesToStage,
        long? bytesStaged,
        long? stagingSize)
    {
        if (IsCoherentlyCurrent(
                installedBuildId,
                targetBuildId,
                publicBuildId,
                installedDepotManifests,
                publicDepotManifests))
        {
            return ProviderInstallUpdateStatus.UpToDate;
        }

        if (bytesToDownload is > 0 &&
            bytesDownloaded is > 0 &&
            bytesDownloaded < bytesToDownload)
        {
            return ProviderInstallUpdateStatus.Downloading;
        }

        if (bytesToStage is > 0 &&
            bytesStaged is >= 0 &&
            bytesStaged < bytesToStage)
        {
            return ProviderInstallUpdateStatus.Staging;
        }

        if (IsConfirmedPendingUpdate(
                installedBuildId,
                targetBuildId,
                publicBuildId,
                bytesToDownload,
                bytesDownloaded,
                bytesToStage,
                bytesStaged))
        {
            return ProviderInstallUpdateStatus.UpdateAvailable;
        }

        if (HasCurrentnessMismatch(
                installedBuildId,
                targetBuildId,
                publicBuildId,
                installedDepotManifests,
                publicDepotManifests))
        {
            return ProviderInstallUpdateStatus.VersionMismatch;
        }

        return ProviderInstallUpdateStatus.Unknown;
    }

    private static bool IsConfirmedPendingUpdate(
        string? installedBuildId,
        string? targetBuildId,
        string? publicBuildId,
        long? bytesToDownload,
        long? bytesDownloaded,
        long? bytesToStage,
        long? bytesStaged)
    {
        if (!long.TryParse(targetBuildId, out var target) || target <= 0 ||
            !long.TryParse(installedBuildId, out var installed) || installed == target)
        {
            return false;
        }

        if (long.TryParse(publicBuildId, out var publicBuild) && publicBuild > 0 && publicBuild != target)
        {
            return false;
        }

        var pendingDownload = bytesToDownload is > 0 && (bytesDownloaded is null or >= 0) && bytesDownloaded.GetValueOrDefault() < bytesToDownload;
        var pendingStaging = bytesToStage is > 0 && (bytesStaged is null or >= 0) && bytesStaged.GetValueOrDefault() < bytesToStage;
        return pendingDownload || pendingStaging;
    }

    private static bool IsCoherentlyCurrent(
        string? installedBuildId,
        string? targetBuildId,
        string? publicBuildId,
        IReadOnlyDictionary<string, string>? installedDepotManifests,
        IReadOnlyDictionary<string, string>? publicDepotManifests)
    {
        if (string.IsNullOrWhiteSpace(installedBuildId) ||
            string.IsNullOrWhiteSpace(targetBuildId) ||
            !string.Equals(installedBuildId.Trim(), targetBuildId.Trim(), StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(publicBuildId) &&
            !string.Equals(installedBuildId.Trim(), publicBuildId.Trim(), StringComparison.Ordinal))
        {
            return false;
        }

        if (installedDepotManifests is null || publicDepotManifests is null)
        {
            return true;
        }

        return DepotManifestsMatch(installedDepotManifests, publicDepotManifests);
    }

    private static bool HasCurrentnessMismatch(
        string? installedBuildId,
        string? targetBuildId,
        string? publicBuildId,
        IReadOnlyDictionary<string, string>? installedDepotManifests,
        IReadOnlyDictionary<string, string>? publicDepotManifests)
    {
        if (!string.IsNullOrWhiteSpace(installedBuildId) &&
            !string.IsNullOrWhiteSpace(targetBuildId) &&
            !string.Equals(installedBuildId.Trim(), targetBuildId.Trim(), StringComparison.Ordinal))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(installedBuildId) &&
            !string.IsNullOrWhiteSpace(publicBuildId) &&
            !string.Equals(installedBuildId.Trim(), publicBuildId.Trim(), StringComparison.Ordinal))
        {
            return true;
        }

        return installedDepotManifests is not null &&
            publicDepotManifests is not null &&
            installedDepotManifests.Any(pair =>
                publicDepotManifests.TryGetValue(pair.Key, out var publicManifest) &&
                !string.Equals(pair.Value, publicManifest, StringComparison.Ordinal));
    }

    private static bool DepotManifestsMatch(
        IReadOnlyDictionary<string, string> installed,
        IReadOnlyDictionary<string, string> published)
    {
        var comparable = installed.Where(pair => published.ContainsKey(pair.Key));
        return comparable.All(pair =>
            string.Equals(pair.Value, published[pair.Key], StringComparison.Ordinal));
    }

    private static long? ParseNonNegative(string? value) =>
        long.TryParse(value, out var parsed) && parsed >= 0
            ? parsed
            : null;

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
