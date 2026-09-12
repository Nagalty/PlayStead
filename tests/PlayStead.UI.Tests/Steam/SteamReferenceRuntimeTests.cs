using PlayStead.Core.Steam;
using PlayStead.Providers.Steam.Evidence;
using PlayStead.Providers.Steam.Remote;
using PlayStead.UI.Steam;

namespace PlayStead.UI.Tests.Steam;

public sealed class SteamReferenceRuntimeTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 12, 20, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task LoadCachedAsync_evaluates_persisted_evidence_without_local_scan_or_remote_query()
    {
        var store = new FakeStore();

        await store.ReplaceLocalAsync(
            [
                Local(
                    "730",
                    "public",
                    "100",
                    ("731", "111"))
            ],
            CancellationToken.None);

        await store.UpsertRemoteAsync(
            Remote(
                "730",
                "public",
                "100",
                Now - TimeSpan.FromHours(2),
                ("731", "111")),
            CancellationToken.None);

        var localSource =
            new ThrowingLocalSource(
                new InvalidOperationException(
                    "LoadCachedAsync must not scan Steam."));

        var provider =
            new RecordingRemoteProvider(
                (_, _, _) =>
                    throw new InvalidOperationException(
                        "LoadCachedAsync must not query SteamCMD."));

        var sut = CreateRuntime(
            localSource,
            store,
            provider);

        var result = await sut.LoadCachedAsync(
            CancellationToken.None);

        var entry = Assert.Single(result.Entries);

        Assert.Equal("730", entry.AppId);
        Assert.Equal("public", entry.BranchName);
        Assert.Equal(
            SteamUpdateState.UpToDate,
            entry.Evaluation.State);
        Assert.Equal(
            SteamUpdateReason.DepotManifestsMatch,
            entry.Evaluation.Reason);

        Assert.Equal(0, localSource.ScanCount);
        Assert.Empty(provider.Calls);
        Assert.Same(result, sut.Current);
    }

    [Fact]
    public async Task LoadCachedAsync_without_remote_cache_is_unknown_without_network_access()
    {
        var store = new FakeStore();

        await store.ReplaceLocalAsync(
            [
                Local(
                    "730",
                    "public",
                    "100",
                    ("731", "111"))
            ],
            CancellationToken.None);

        var localSource =
            new ThrowingLocalSource(
                new InvalidOperationException(
                    "Cache load must not scan."));

        var provider =
            new RecordingRemoteProvider(
                (_, _, _) =>
                    throw new InvalidOperationException(
                        "Cache load must not query."));

        var sut = CreateRuntime(
            localSource,
            store,
            provider);

        var result = await sut.LoadCachedAsync(
            CancellationToken.None);

        var entry = Assert.Single(result.Entries);

        Assert.Equal(
            SteamUpdateState.Unknown,
            entry.Evaluation.State);

        Assert.Equal(
            SteamUpdateReason.RemoteRefreshFailedWithoutCache,
            entry.Evaluation.Reason);

        Assert.Empty(provider.Calls);
    }

    [Fact]
    public async Task RefreshStaleAsync_scans_local_replaces_snapshot_refreshes_remote_and_evaluates_result()
    {
        var localSource =
            new StubLocalSource(
                [
                    Local(
                        "730",
                        "public",
                        "100",
                        ("731", "111"))
                ]);

        var store = new FakeStore();

        await store.ReplaceLocalAsync(
            [
                Local(
                    "440",
                    "public",
                    "200",
                    ("441", "333"))
            ],
            CancellationToken.None);

        var provider =
            new RecordingRemoteProvider(
                (appId, branch, _) =>
                    Task.FromResult(
                        Success(
                            appId,
                            branch,
                            "101",
                            Now,
                            ("731", "999"))));

        var sut = CreateRuntime(
            localSource,
            store,
            provider);

        var result = await sut.RefreshStaleAsync(
            CancellationToken.None);

        Assert.Equal(1, localSource.ScanCount);

        var persistedLocal =
            await store.GetLocalAsync(
                CancellationToken.None);

        var local = Assert.Single(persistedLocal);
        Assert.Equal("730", local.AppId);

        Assert.Equal(
            [("730", "public")],
            provider.Calls);

        var entry = Assert.Single(result.Entries);

        Assert.Equal(
            SteamUpdateState.UpdateAvailable,
            entry.Evaluation.State);

        Assert.Equal(
            ["731"],
            entry.Evaluation.ChangedDepotIds);

        Assert.Same(result, sut.Current);
    }

    [Fact]
    public async Task RefreshAllAsync_forces_remote_query_even_when_cached_remote_is_fresh()
    {
        var local =
            Local(
                "730",
                "public",
                "100",
                ("731", "111"));

        var localSource =
            new StubLocalSource([local]);

        var store = new FakeStore();

        await store.ReplaceLocalAsync(
            [local],
            CancellationToken.None);

        await store.UpsertRemoteAsync(
            Remote(
                "730",
                "public",
                "100",
                Now - TimeSpan.FromMinutes(5),
                ("731", "111")),
            CancellationToken.None);

        var provider =
            new RecordingRemoteProvider(
                (appId, branch, _) =>
                    Task.FromResult(
                        Success(
                            appId,
                            branch,
                            "101",
                            Now,
                            ("731", "999"))));

        var sut = CreateRuntime(
            localSource,
            store,
            provider);

        var result = await sut.RefreshAllAsync(
            CancellationToken.None);

        Assert.Single(provider.Calls);

        Assert.Equal(
            SteamUpdateState.UpdateAvailable,
            Assert.Single(result.Entries).Evaluation.State);
    }

    [Fact]
    public async Task Unknown_local_branch_does_not_query_remote_and_stays_unknown()
    {
        var localSource =
            new StubLocalSource(
                [
                    Local(
                        "730",
                        branchName: null,
                        buildId: "100",
                        ("731", "111"))
                ]);

        var store = new FakeStore();

        var provider =
            new RecordingRemoteProvider(
                (appId, branch, _) =>
                    Task.FromResult(
                        Success(
                            appId,
                            branch,
                            "101",
                            Now,
                            ("731", "999"))));

        var sut = CreateRuntime(
            localSource,
            store,
            provider);

        var result = await sut.RefreshStaleAsync(
            CancellationToken.None);

        Assert.Empty(provider.Calls);

        var entry = Assert.Single(result.Entries);

        Assert.Equal(
            SteamUpdateState.Unknown,
            entry.Evaluation.State);

        Assert.Equal(
            SteamUpdateReason.LocalBranchUnknown,
            entry.Evaluation.Reason);
    }

    [Fact]
    public async Task Local_scan_failure_fails_open_to_last_cached_evaluations()
    {
        var store = new FakeStore();

        await store.ReplaceLocalAsync(
            [
                Local(
                    "730",
                    "public",
                    "100",
                    ("731", "111"))
            ],
            CancellationToken.None);

        await store.UpsertRemoteAsync(
            Remote(
                "730",
                "public",
                "100",
                Now - TimeSpan.FromHours(2),
                ("731", "111")),
            CancellationToken.None);

        var localSource =
            new ThrowingLocalSource(
                new IOException(
                    "Steam library temporarily unavailable."));

        var provider =
            new RecordingRemoteProvider(
                (_, _, _) =>
                    throw new InvalidOperationException(
                        "Remote query must not run after local scan failure."));

        var sut = CreateRuntime(
            localSource,
            store,
            provider);

        var result = await sut.RefreshStaleAsync(
            CancellationToken.None);

        Assert.Equal(1, localSource.ScanCount);
        Assert.Empty(provider.Calls);

        Assert.Equal(
            SteamUpdateState.UpToDate,
            Assert.Single(result.Entries).Evaluation.State);
    }

    [Fact]
    public async Task Entries_are_returned_in_stable_app_id_order()
    {
        var store = new FakeStore();

        await store.ReplaceLocalAsync(
            [
                Local(
                    "730",
                    "public",
                    "100",
                    ("731", "111")),
                Local(
                    "440",
                    "public",
                    "200",
                    ("441", "333"))
            ],
            CancellationToken.None);

        await store.UpsertRemoteAsync(
            Remote(
                "730",
                "public",
                "100",
                Now,
                ("731", "111")),
            CancellationToken.None);

        await store.UpsertRemoteAsync(
            Remote(
                "440",
                "public",
                "200",
                Now,
                ("441", "333")),
            CancellationToken.None);

        var sut = CreateRuntime(
            new ThrowingLocalSource(
                new InvalidOperationException()),
            store,
            new RecordingRemoteProvider(
                (_, _, _) =>
                    throw new InvalidOperationException()));

        var result = await sut.LoadCachedAsync(
            CancellationToken.None);

        Assert.Equal(
            ["440", "730"],
            result.Entries
                .Select(x => x.AppId)
                .ToArray());
    }

    private static SteamReferenceRuntime CreateRuntime(
        ISteamLocalEvidenceSource localSource,
        ISteamEvidenceStore store,
        ISteamRemoteEvidenceProvider provider)
    {
        var timeProvider =
            new FixedTimeProvider(Now);

        var coordinator =
            new SteamRemoteRefreshCoordinator(
                store,
                provider,
                new SteamRemoteEvidenceFreshnessPolicy(),
                timeProvider);

        return new SteamReferenceRuntime(
            localSource,
            store,
            coordinator,
            new SteamUpdateStateEvaluator(),
            timeProvider);
    }

    private static SteamLocalEvidence Local(
        string appId,
        string? branchName,
        string? buildId,
        params (string DepotId, string ManifestId)[] depots)
        => new(
            appId,
            buildId,
            branchName,
            DepotMap(depots),
            Now);

    private static SteamRemoteEvidence Remote(
        string appId,
        string branchName,
        string? buildId,
        DateTimeOffset observedAtUtc,
        params (string DepotId, string ManifestId)[] depots)
        => new(
            appId,
            branchName,
            buildId,
            DepotMap(depots),
            observedAtUtc,
            SteamRemoteEvidenceSource.SteamCmdAnonymous);

    private static SteamRemoteEvidenceResult Success(
        string appId,
        string branchName,
        string? buildId,
        DateTimeOffset observedAtUtc,
        params (string DepotId, string ManifestId)[] depots)
        => new(
            SteamRemoteEvidenceStatus.Success,
            Remote(
                appId,
                branchName,
                buildId,
                observedAtUtc,
                depots),
            FailureKind: null);

    private static IReadOnlyDictionary<string, string> DepotMap(
        params (string DepotId, string ManifestId)[] depots)
        => depots.ToDictionary(
            x => x.DepotId,
            x => x.ManifestId,
            StringComparer.Ordinal);

    private sealed class FixedTimeProvider :
        TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(
            DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow()
            => _utcNow;
    }

    private sealed class StubLocalSource :
        ISteamLocalEvidenceSource
    {
        private readonly IReadOnlyList<SteamLocalEvidence> _result;

        public StubLocalSource(
            IReadOnlyList<SteamLocalEvidence> result)
        {
            _result = result;
        }

        public int ScanCount { get; private set; }

        public Task<IReadOnlyList<SteamLocalEvidence>> ScanAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ScanCount++;
            return Task.FromResult(_result);
        }
    }

    private sealed class ThrowingLocalSource :
        ISteamLocalEvidenceSource
    {
        private readonly Exception _exception;

        public ThrowingLocalSource(
            Exception exception)
        {
            _exception = exception;
        }

        public int ScanCount { get; private set; }

        public Task<IReadOnlyList<SteamLocalEvidence>> ScanAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ScanCount++;

            return Task.FromException<IReadOnlyList<SteamLocalEvidence>>(
                _exception);
        }
    }

    private sealed class RecordingRemoteProvider :
        ISteamRemoteEvidenceProvider
    {
        private readonly Func<
            string,
            string,
            CancellationToken,
            Task<SteamRemoteEvidenceResult>> _handler;

        public RecordingRemoteProvider(
            Func<
                string,
                string,
                CancellationToken,
                Task<SteamRemoteEvidenceResult>> handler)
        {
            _handler = handler;
        }

        public List<(string AppId, string BranchName)> Calls { get; } = [];

        public async Task<SteamRemoteEvidenceResult> QueryAsync(
            string appId,
            string branchName,
            CancellationToken cancellationToken)
        {
            Calls.Add((appId, branchName));

            return await _handler(
                appId,
                branchName,
                cancellationToken);
        }
    }

    private sealed class FakeStore :
        ISteamEvidenceStore
    {
        private readonly object _gate = new();

        private IReadOnlyList<SteamLocalEvidence> _local = [];

        private readonly Dictionary<
            (string AppId, string BranchName),
            SteamRemoteEvidence> _remote = [];

        public Task ReplaceLocalAsync(
            IReadOnlyCollection<SteamLocalEvidence> evidence,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            lock (_gate)
            {
                _local = evidence
                    .OrderBy(x => x.AppId, StringComparer.Ordinal)
                    .ToArray();
            }

            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<SteamLocalEvidence>> GetLocalAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            lock (_gate)
            {
                return Task.FromResult(_local);
            }
        }

        public Task<SteamRemoteEvidence?> GetRemoteAsync(
            string appId,
            string branchName,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            lock (_gate)
            {
                _remote.TryGetValue(
                    (appId, branchName),
                    out var evidence);

                return Task.FromResult(evidence);
            }
        }

        public Task UpsertRemoteAsync(
            SteamRemoteEvidence evidence,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            lock (_gate)
            {
                _remote[
                    (evidence.AppId, evidence.BranchName)] =
                    evidence;
            }

            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<SteamRemoteEvidence>> GetAllRemoteAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            lock (_gate)
            {
                IReadOnlyList<SteamRemoteEvidence> result =
                    _remote.Values
                        .OrderBy(x => x.AppId, StringComparer.Ordinal)
                        .ThenBy(x => x.BranchName, StringComparer.Ordinal)
                        .ToArray();

                return Task.FromResult(result);
            }
        }

        public Task ClearRemoteAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            lock (_gate)
            {
                _remote.Clear();
            }

            return Task.CompletedTask;
        }
    }
}
