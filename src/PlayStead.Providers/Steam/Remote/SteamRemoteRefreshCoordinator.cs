using PlayStead.Core.Steam;

namespace PlayStead.Providers.Steam.Remote;

public sealed class SteamRemoteRefreshCoordinator
{
    private readonly ISteamEvidenceStore _store;
    private readonly ISteamRemoteEvidenceProvider _provider;
    private readonly SteamRemoteEvidenceFreshnessPolicy _freshnessPolicy;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _campaignGate =
        new(1, 1);

    public SteamRemoteRefreshCoordinator(
        ISteamEvidenceStore store,
        ISteamRemoteEvidenceProvider provider,
        SteamRemoteEvidenceFreshnessPolicy freshnessPolicy,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(freshnessPolicy);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _store = store;
        _provider = provider;
        _freshnessPolicy = freshnessPolicy;
        _timeProvider = timeProvider;
    }

    public Task RefreshStaleAsync(
        IReadOnlyCollection<SteamLocalEvidence> localEvidence,
        CancellationToken cancellationToken)
        => RefreshAsync(
            localEvidence,
            force: false,
            cancellationToken);

    public Task RefreshAllAsync(
        IReadOnlyCollection<SteamLocalEvidence> localEvidence,
        CancellationToken cancellationToken)
        => RefreshAsync(
            localEvidence,
            force: true,
            cancellationToken);

    private async Task RefreshAsync(
        IReadOnlyCollection<SteamLocalEvidence> localEvidence,
        bool force,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(localEvidence);

        await _campaignGate.WaitAsync(
            cancellationToken)
            .ConfigureAwait(false);

        try
        {
            var targets =
                BuildTargets(localEvidence);

            foreach (var target in targets)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!force)
                {
                    var cached =
                        await _store.GetRemoteAsync(
                            target.AppId,
                            target.BranchName,
                            cancellationToken)
                            .ConfigureAwait(false);

                    if (cached is not null &&
                        _freshnessPolicy.IsFresh(
                            cached,
                            _timeProvider.GetUtcNow()))
                    {
                        continue;
                    }
                }

                var result =
                    await _provider.QueryAsync(
                        target.AppId,
                        target.BranchName,
                        cancellationToken)
                        .ConfigureAwait(false);

                if (result.Status !=
                        SteamRemoteEvidenceStatus.Success ||
                    result.Evidence is null)
                {
                    continue;
                }

                await _store.UpsertRemoteAsync(
                    result.Evidence,
                    cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        finally
        {
            _campaignGate.Release();
        }
    }

    private static IReadOnlyList<RefreshTarget> BuildTargets(
        IReadOnlyCollection<SteamLocalEvidence> localEvidence)
        => localEvidence
            .Where(
                evidence =>
                    !string.IsNullOrWhiteSpace(
                        evidence.BranchName))
            .Select(
                evidence =>
                    new RefreshTarget(
                        evidence.AppId,
                        evidence.BranchName!))
            .Distinct()
            .OrderBy(
                target => target.AppId,
                StringComparer.Ordinal)
            .ThenBy(
                target => target.BranchName,
                StringComparer.Ordinal)
            .ToArray();

    private sealed record RefreshTarget(
        string AppId,
        string BranchName);
}
