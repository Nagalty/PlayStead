using PlayStead.Core.Steam;
using PlayStead.Providers.Steam.Remote;

namespace PlayStead.Providers.Tests.Steam.Remote;

public sealed class SteamRemoteRefreshCoordinatorTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 12, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task RefreshStaleAsync_skips_fresh_cached_remote_evidence()
    {
        var store = new FakeStore();
        await store.UpsertRemoteAsync(
            Remote(
                "730",
                "public",
                Now - TimeSpan.FromHours(2),
                "101"),
            CancellationToken.None);

        var provider = new RecordingProvider(
            (_, _, _) => Task.FromResult(
                Success(
                    "730",
                    "public",
                    Now,
                    "102")));

        var sut = CreateCoordinator(
            store,
            provider);

        await sut.RefreshStaleAsync(
            [Local("730", "public")],
            CancellationToken.None);

        Assert.Empty(provider.Calls);

        var cached = await store.GetRemoteAsync(
            "730",
            "public",
            CancellationToken.None);

        Assert.Equal("101", cached?.BuildId);
    }

    [Fact]
    public async Task RefreshStaleAsync_queries_missing_remote_evidence_and_persists_success()
    {
        var store = new FakeStore();

        var provider = new RecordingProvider(
            (appId, branch, _) => Task.FromResult(
                Success(
                    appId,
                    branch,
                    Now,
                    "102")));

        var sut = CreateCoordinator(
            store,
            provider);

        await sut.RefreshStaleAsync(
            [Local("730", "public")],
            CancellationToken.None);

        Assert.Equal(
            [("730", "public")],
            provider.Calls);

        var cached = await store.GetRemoteAsync(
            "730",
            "public",
            CancellationToken.None);

        Assert.NotNull(cached);
        Assert.Equal("102", cached!.BuildId);
        Assert.Equal(Now, cached.ObservedAtUtc);
    }

    [Fact]
    public async Task RefreshStaleAsync_queries_evidence_at_the_six_hour_boundary()
    {
        var store = new FakeStore();
        await store.UpsertRemoteAsync(
            Remote(
                "730",
                "public",
                Now - TimeSpan.FromHours(6),
                "101"),
            CancellationToken.None);

        var provider = new RecordingProvider(
            (appId, branch, _) => Task.FromResult(
                Success(
                    appId,
                    branch,
                    Now,
                    "102")));

        var sut = CreateCoordinator(
            store,
            provider);

        await sut.RefreshStaleAsync(
            [Local("730", "public")],
            CancellationToken.None);

        Assert.Single(provider.Calls);

        var cached = await store.GetRemoteAsync(
            "730",
            "public",
            CancellationToken.None);

        Assert.Equal("102", cached?.BuildId);
    }

    [Fact]
    public async Task RefreshAllAsync_forces_refresh_even_when_cache_is_fresh()
    {
        var store = new FakeStore();
        await store.UpsertRemoteAsync(
            Remote(
                "730",
                "public",
                Now - TimeSpan.FromMinutes(5),
                "101"),
            CancellationToken.None);

        var provider = new RecordingProvider(
            (appId, branch, _) => Task.FromResult(
                Success(
                    appId,
                    branch,
                    Now,
                    "102")));

        var sut = CreateCoordinator(
            store,
            provider);

        await sut.RefreshAllAsync(
            [Local("730", "public")],
            CancellationToken.None);

        Assert.Single(provider.Calls);

        var cached = await store.GetRemoteAsync(
            "730",
            "public",
            CancellationToken.None);

        Assert.Equal("102", cached?.BuildId);
    }

    [Fact]
    public async Task Refresh_failure_preserves_last_good_cached_evidence()
    {
        var store = new FakeStore();

        var previous = Remote(
            "730",
            "public",
            Now - TimeSpan.FromHours(8),
            "101");

        await store.UpsertRemoteAsync(
            previous,
            CancellationToken.None);

        var provider = new RecordingProvider(
            (_, _, _) => Task.FromResult(
                new SteamRemoteEvidenceResult(
                    SteamRemoteEvidenceStatus.RefreshFailed,
                    Evidence: null,
                    SteamRemoteFailureKind.Timeout)));

        var sut = CreateCoordinator(
            store,
            provider);

        await sut.RefreshStaleAsync(
            [Local("730", "public")],
            CancellationToken.None);

        var cached = await store.GetRemoteAsync(
            "730",
            "public",
            CancellationToken.None);

        Assert.Equal(previous, cached);
    }

    [Fact]
    public async Task Branch_unavailable_preserves_last_good_cached_evidence()
    {
        var store = new FakeStore();

        var previous = Remote(
            "730",
            "experimental",
            Now - TimeSpan.FromHours(8),
            "101");

        await store.UpsertRemoteAsync(
            previous,
            CancellationToken.None);

        var provider = new RecordingProvider(
            (_, _, _) => Task.FromResult(
                new SteamRemoteEvidenceResult(
                    SteamRemoteEvidenceStatus.BranchUnavailable,
                    Evidence: null,
                    SteamRemoteFailureKind.BranchUnavailable)));

        var sut = CreateCoordinator(
            store,
            provider);

        await sut.RefreshStaleAsync(
            [Local("730", "experimental")],
            CancellationToken.None);

        var cached = await store.GetRemoteAsync(
            "730",
            "experimental",
            CancellationToken.None);

        Assert.Equal(previous, cached);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Local_evidence_without_exact_branch_is_not_queried(
        string? branchName)
    {
        var store = new FakeStore();

        var provider = new RecordingProvider(
            (appId, branch, _) => Task.FromResult(
                Success(
                    appId,
                    branch,
                    Now,
                    "102")));

        var sut = CreateCoordinator(
            store,
            provider);

        await sut.RefreshStaleAsync(
            [Local("730", branchName)],
            CancellationToken.None);

        Assert.Empty(provider.Calls);
    }

    [Fact]
    public async Task Duplicate_local_app_and_branch_pairs_are_queried_once()
    {
        var store = new FakeStore();

        var provider = new RecordingProvider(
            (appId, branch, _) => Task.FromResult(
                Success(
                    appId,
                    branch,
                    Now,
                    "102")));

        var sut = CreateCoordinator(
            store,
            provider);

        await sut.RefreshStaleAsync(
            [
                Local("730", "public"),
                Local("730", "public"),
                Local("730", "public")
            ],
            CancellationToken.None);

        Assert.Single(provider.Calls);
    }

    [Fact]
    public async Task Campaign_processes_targets_in_stable_app_and_branch_order()
    {
        var store = new FakeStore();

        var provider = new RecordingProvider(
            (appId, branch, _) => Task.FromResult(
                Success(
                    appId,
                    branch,
                    Now,
                    "102")));

        var sut = CreateCoordinator(
            store,
            provider);

        await sut.RefreshAllAsync(
            [
                Local("730", "public"),
                Local("440", "public"),
                Local("440", "experimental")
            ],
            CancellationToken.None);

        Assert.Equal(
            [
                ("440", "experimental"),
                ("440", "public"),
                ("730", "public")
            ],
            provider.Calls);
    }

    [Fact]
    public async Task Concurrent_stale_campaigns_do_not_overlap_or_duplicate_refresh()
    {
        var store = new FakeStore();
        var provider = new BlockingSuccessProvider(Now);
        var sut = CreateCoordinator(store, provider);

        var local =
            (IReadOnlyCollection<SteamLocalEvidence>)
            [Local("730", "public")];

        var first = sut.RefreshStaleAsync(
            local,
            CancellationToken.None);

        await provider.FirstCallStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(2));

        var second = sut.RefreshStaleAsync(
            local,
            CancellationToken.None);

        await Task.Delay(100);

        Assert.Equal(1, provider.CallCount);
        Assert.Equal(1, provider.MaxConcurrentCalls);

        provider.ReleaseFirstCall();

        await Task.WhenAll(first, second);

        Assert.Equal(1, provider.CallCount);
        Assert.Equal(1, provider.MaxConcurrentCalls);
    }

    [Fact]
    public async Task Caller_cancellation_is_propagated_and_does_not_persist_result()
    {
        var store = new FakeStore();

        var provider = new RecordingProvider(
            async (appId, branch, cancellationToken) =>
            {
                await Task.Delay(
                    Timeout.InfiniteTimeSpan,
                    cancellationToken);

                return Success(
                    appId,
                    branch,
                    Now,
                    "102");
            });

        var sut = CreateCoordinator(
            store,
            provider);

        using var cts =
            new CancellationTokenSource(
                TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => sut.RefreshAllAsync(
                [Local("730", "public")],
                cts.Token));

        Assert.Null(
            await store.GetRemoteAsync(
                "730",
                "public",
                CancellationToken.None));
    }

    private static SteamRemoteRefreshCoordinator CreateCoordinator(
        ISteamEvidenceStore store,
        ISteamRemoteEvidenceProvider provider)
        => new(
            store,
            provider,
            new SteamRemoteEvidenceFreshnessPolicy(),
            new FixedTimeProvider(Now));

    private static SteamLocalEvidence Local(
        string appId,
        string? branchName)
        => new(
            appId,
            "100",
            branchName,
            new Dictionary<string, string>
            {
                [$"{appId}1"] = "111"
            },
            Now);

    private static SteamRemoteEvidence Remote(
        string appId,
        string branchName,
        DateTimeOffset observedAtUtc,
        string buildId)
        => new(
            appId,
            branchName,
            buildId,
            new Dictionary<string, string>
            {
                [$"{appId}1"] = "111"
            },
            observedAtUtc,
            SteamRemoteEvidenceSource.SteamCmdAnonymous);

    private static SteamRemoteEvidenceResult Success(
        string appId,
        string branchName,
        DateTimeOffset observedAtUtc,
        string buildId)
        => new(
            SteamRemoteEvidenceStatus.Success,
            Remote(
                appId,
                branchName,
                observedAtUtc,
                buildId),
            FailureKind: null);

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

    private sealed class RecordingProvider :
        ISteamRemoteEvidenceProvider
    {
        private readonly Func<
            string,
            string,
            CancellationToken,
            Task<SteamRemoteEvidenceResult>> _handler;

        public RecordingProvider(
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

    private sealed class BlockingSuccessProvider :
        ISteamRemoteEvidenceProvider
    {
        private readonly DateTimeOffset _observedAtUtc;
        private readonly TaskCompletionSource _release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private int _activeCalls;
        private int _callCount;
        private int _maxConcurrentCalls;

        public BlockingSuccessProvider(
            DateTimeOffset observedAtUtc)
        {
            _observedAtUtc = observedAtUtc;
        }

        public TaskCompletionSource FirstCallStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int CallCount =>
            Volatile.Read(ref _callCount);

        public int MaxConcurrentCalls =>
            Volatile.Read(ref _maxConcurrentCalls);

        public void ReleaseFirstCall()
            => _release.TrySetResult();

        public async Task<SteamRemoteEvidenceResult> QueryAsync(
            string appId,
            string branchName,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _callCount);

            var active =
                Interlocked.Increment(ref _activeCalls);

            while (true)
            {
                var currentMax =
                    Volatile.Read(ref _maxConcurrentCalls);

                if (active <= currentMax ||
                    Interlocked.CompareExchange(
                        ref _maxConcurrentCalls,
                        active,
                        currentMax) == currentMax)
                {
                    break;
                }
            }

            FirstCallStarted.TrySetResult();

            try
            {
                await _release.Task.WaitAsync(
                    cancellationToken);

                return Success(
                    appId,
                    branchName,
                    _observedAtUtc,
                    "102");
            }
            finally
            {
                Interlocked.Decrement(ref _activeCalls);
            }
        }
    }

    private sealed class FakeStore :
        ISteamEvidenceStore
    {
        private readonly object _gate = new();

        private readonly Dictionary<
            (string AppId, string BranchName),
            SteamRemoteEvidence> _remote = [];

        private IReadOnlyList<SteamLocalEvidence> _local = [];

        public Task ReplaceLocalAsync(
            IReadOnlyCollection<SteamLocalEvidence> evidence,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            lock (_gate)
            {
                _local = evidence.ToArray();
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
