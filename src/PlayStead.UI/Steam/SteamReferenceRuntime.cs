using PlayStead.Core.Steam;
using PlayStead.Providers.Steam.Evidence;
using PlayStead.Providers.Steam.Remote;

namespace PlayStead.UI.Steam;

public sealed class SteamReferenceRuntime :
    ISteamReferenceRuntime
{
    private readonly ISteamLocalEvidenceSource _localEvidenceSource;
    private readonly ISteamEvidenceStore _evidenceStore;
    private readonly SteamRemoteRefreshCoordinator _refreshCoordinator;
    private readonly SteamUpdateStateEvaluator _evaluator;
    private readonly TimeProvider _timeProvider;

    public SteamReferenceRuntime(
        ISteamLocalEvidenceSource localEvidenceSource,
        ISteamEvidenceStore evidenceStore,
        SteamRemoteRefreshCoordinator refreshCoordinator,
        SteamUpdateStateEvaluator evaluator,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(localEvidenceSource);
        ArgumentNullException.ThrowIfNull(evidenceStore);
        ArgumentNullException.ThrowIfNull(refreshCoordinator);
        ArgumentNullException.ThrowIfNull(evaluator);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _localEvidenceSource = localEvidenceSource;
        _evidenceStore = evidenceStore;
        _refreshCoordinator = refreshCoordinator;
        _evaluator = evaluator;
        _timeProvider = timeProvider;
    }

    public SteamReferenceSnapshot Current { get; private set; } =
        SteamReferenceSnapshot.Empty;

    public Task<SteamReferenceSnapshot> LoadCachedAsync(
        CancellationToken cancellationToken)
        => BuildSnapshotFromCacheAsync(
            cancellationToken);

    public Task<SteamReferenceSnapshot> RefreshStaleAsync(
        CancellationToken cancellationToken)
        => RefreshAsync(
            forceRemoteRefresh: false,
            cancellationToken);

    public Task<SteamReferenceSnapshot> RefreshAllAsync(
        CancellationToken cancellationToken)
        => RefreshAsync(
            forceRemoteRefresh: true,
            cancellationToken);

    private async Task<SteamReferenceSnapshot> RefreshAsync(
        bool forceRemoteRefresh,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<SteamLocalEvidence> localEvidence;

        try
        {
            localEvidence =
                await _localEvidenceSource.ScanAsync(
                    cancellationToken)
                    .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return await BuildSnapshotFromCacheAsync(
                cancellationToken)
                .ConfigureAwait(false);
        }

        await _evidenceStore.ReplaceLocalAsync(
            localEvidence,
            cancellationToken)
            .ConfigureAwait(false);

        if (forceRemoteRefresh)
        {
            await _refreshCoordinator.RefreshAllAsync(
                localEvidence,
                cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            await _refreshCoordinator.RefreshStaleAsync(
                localEvidence,
                cancellationToken)
                .ConfigureAwait(false);
        }

        return await BuildSnapshotFromCacheAsync(
            cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<SteamReferenceSnapshot> BuildSnapshotFromCacheAsync(
        CancellationToken cancellationToken)
    {
        var localEvidence =
            await _evidenceStore.GetLocalAsync(
                cancellationToken)
                .ConfigureAwait(false);

        var evaluatedAtUtc =
            _timeProvider.GetUtcNow();

        var entries =
            new List<SteamReferenceEntry>(
                localEvidence.Count);

        foreach (var local in localEvidence
                     .OrderBy(
                         x => x.AppId,
                         StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var remoteResult =
                await CreateCachedRemoteResultAsync(
                    local,
                    cancellationToken)
                    .ConfigureAwait(false);

            var evaluation =
                _evaluator.Evaluate(
                    local,
                    remoteResult,
                    evaluatedAtUtc);

            entries.Add(
                new SteamReferenceEntry(
                    local.AppId,
                    local.BranchName,
                    evaluation));
        }

        var snapshot =
            new SteamReferenceSnapshot(
                entries);

        Current = snapshot;

        return snapshot;
    }

    private async Task<SteamRemoteEvidenceResult>
        CreateCachedRemoteResultAsync(
            SteamLocalEvidence local,
            CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(
                local.BranchName))
        {
            return MissingCachedRemote();
        }

        var cached =
            await _evidenceStore.GetRemoteAsync(
                local.AppId,
                local.BranchName,
                cancellationToken)
                .ConfigureAwait(false);

        return cached is null
            ? MissingCachedRemote()
            : new SteamRemoteEvidenceResult(
                SteamRemoteEvidenceStatus.Success,
                cached,
                FailureKind: null);
    }

    private static SteamRemoteEvidenceResult MissingCachedRemote()
        => new(
            SteamRemoteEvidenceStatus.RefreshFailed,
            Evidence: null,
            FailureKind: null);
}
