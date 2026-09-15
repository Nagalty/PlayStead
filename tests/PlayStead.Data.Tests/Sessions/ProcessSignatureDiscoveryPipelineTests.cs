using Microsoft.Data.Sqlite;
using PlayStead.Core.Library;
using PlayStead.Core.Sessions;
using PlayStead.Core.Sessions.Discovery;
using PlayStead.Data.Sessions;
using PlayStead.Data.Tests.Database;

namespace PlayStead.Data.Tests.Sessions;

public sealed class ProcessSignatureDiscoveryPipelineTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static readonly DateTimeOffset T0 = new(2026, 9, 15, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Two_simulated_episodes_accept_exact_discovered_main()
    {
        using var d = await Driver.CreateAsync();
        await d.EpisodeAsync(100);
        Assert.Null(await d.Signatures.GetAsync(d.Scope.GameId.Value, Ct));
        var reference = (await d.LoadAsync())!.Reference!;
        await d.RestartAsync();
        await d.EpisodeAsync(200);
        var proof = (await d.LoadAsync())!;
        Assert.Equal(reference.EpisodeId, proof.Reference!.EpisodeId);
        Assert.NotEqual(reference.EpisodeId, proof.Confirmation!.EpisodeId);
        Assert.Equal(DiscoveryDecisionKind.PromoteMain, new ProcessSignatureDiscoveryPolicy().Evaluate(
            new(proof.Inventory, [proof.Reference, proof.Confirmation], false, null)).Kind);
        Assert.True(await d.AcceptAsync());
        var accepted = (await d.Signatures.GetAsync(d.Scope.GameId.Value, Ct))!;
        Assert.Equal(ProcessSignatureOrigin.Discovered, accepted.Origin);
        Assert.Equal(ProcessSignatureValidationState.Valid, accepted.Discovery!.ValidationState);
        Assert.Equal(d.Scope.InstallationId, accepted.Discovery.InstallationId);
        Assert.Equal(d.Scope.GenerationId, accepted.Discovery.GenerationId);
        Assert.Equal(1, accepted.Discovery.PolicyVersion);
        var main = Assert.Single(accepted.Entries);
        Assert.Equal(@"C:\Games\Example\Game.exe", main.ExecutablePath);
        Assert.Equal("Game.exe", main.ExecutableName);
        Assert.Equal(ProcessSignatureEntryKind.Main, main.Kind);
        Assert.Equal(new FileRevision(10, T0.AddDays(-1)), main.ValidatedRevision);
        var consumed = (await d.LoadAsync())!;
        Assert.Null(consumed.Reference);
        Assert.Null(consumed.Confirmation);
        Assert.NotEqual(proof.ConcurrencyToken, consumed.ConcurrencyToken);
        Assert.Empty(await d.Sessions.GetRecentAsync(10, Ct));
    }

    [Fact]
    public async Task Reference_survives_new_coordinator_and_store_instances()
    {
        using var d = await Driver.CreateAsync();
        await d.EpisodeAsync(100);
        var before = (await d.LoadAsync())!;
        await d.RestartAsync();
        var after = (await d.LoadAsync())!;
        Assert.Equal(before.ConcurrencyToken, after.ConcurrencyToken);
        Assert.Equal(before.Reference!.EpisodeId, after.Reference!.EpisodeId);
        Assert.Equal(before.Reference.StartedAtUtc, after.Reference.StartedAtUtc);
        Assert.Equal(before.Reference.EndedAtUtc, after.Reference.EndedAtUtc);
        Assert.Equal(before.LastSequenceNumber, after.LastSequenceNumber);
        Assert.Null(after.Confirmation);
        Assert.False(await d.AcceptAsync());
    }

    [Fact]
    public async Task Restart_mid_episode_never_completes_it()
    {
        using var d = await Driver.CreateAsync();
        await d.TickAsync();
        await d.TickAsync();
        var process = d.Process(100);
        await d.TickAsync(process);
        await d.RestartAsync();
        await d.TickAsync(process);
        await d.TickAsync();
        await d.TickAsync();
        var state = (await d.LoadAsync())!;
        Assert.Null(state.Reference);
        Assert.Null(state.Confirmation);
        Assert.Equal(0, state.LastSequenceNumber);
    }

    [Fact]
    public async Task restart_mid_second_episode_keeps_completed_reference()
    {
        using var d = await Driver.CreateAsync();
        await d.EpisodeAsync(100);
        var before = (await d.LoadAsync())!;
        await d.PrepareAsync();
        var process = d.Process(200);
        await d.TickAsync(process);
        var started = (await d.LoadAsync())!;
        Assert.Equal(before.ConcurrencyToken, started.ConcurrencyToken);
        Assert.Equal(before.Reference!.EpisodeId, started.Reference!.EpisodeId);
        await d.RestartAsync();
        Assert.Equal(before.LastSequenceNumber, (await d.LoadAsync())!.LastSequenceNumber);
        await d.TickAsync(process);
        await d.TickAsync();
        Assert.Null((await d.LoadAsync())!.Confirmation);
        await d.TickAsync();
        await d.EpisodeAsync(300);
        var after = (await d.LoadAsync())!;
        Assert.Equal(before.Reference.EpisodeId, after.Reference!.EpisodeId);
        Assert.Equal(before.LastSequenceNumber + 1, after.Confirmation!.SequenceNumber);
    }

    [Fact]
    public async Task Restart_after_reference_requires_known_absence()
    {
        using var d = await Driver.CreateAsync();
        await d.EpisodeAsync(100);
        var before = (await d.LoadAsync())!;
        await d.RestartAsync();
        var process = d.Process(200);
        await d.TickAsync(process);
        await d.TickAsync(process);
        await d.TickAsync();
        var after = (await d.LoadAsync())!;
        Assert.Equal(before.Reference!.EpisodeId, after.Reference!.EpisodeId);
        Assert.Equal(before.ConcurrencyToken, after.ConcurrencyToken);
        Assert.Null(after.Confirmation);
        await d.TickAsync();
        await d.EpisodeAsync(300);
        Assert.NotNull((await d.LoadAsync())!.Confirmation);
    }

    [Fact]
    public async Task restart_does_not_count_as_second_episode()
    {
        using var d = await Driver.CreateAsync();
        await d.EpisodeAsync(100);
        var before = (await d.LoadAsync())!;
        for (var restart = 0; restart < 3; restart++)
        {
            await d.RestartAsync();
            Assert.False(await d.AcceptAsync());
            var after = (await d.LoadAsync())!;
            Assert.Equal(1, after.LastSequenceNumber);
            Assert.Equal(before.ConcurrencyToken, after.ConcurrencyToken);
            Assert.Null(after.Confirmation);
        }
    }

    [Fact]
    public async Task old_reference_cannot_pair_with_success_after_intermediate_contradiction()
    {
        using var d = await Driver.CreateAsync(companion: true);
        await d.CompanionEpisodeAsync("Game.exe", 100);
        var old = (await d.LoadAsync())!;
        await d.PrepareAsync();
        await d.CompanionEpisodeAsync("Companion.exe", 200);
        var contradicted = (await d.LoadAsync())!;
        Assert.Null(contradicted.Reference);
        Assert.Null(contradicted.Confirmation);
        Assert.NotEqual(old.ConcurrencyToken, contradicted.ConcurrencyToken);
        Assert.Equal([DiscoveryReason.ConflictingEpisodes], contradicted.Reasons);
        await d.RestartAsync();
        var restarted = (await d.LoadAsync())!;
        Assert.Equal(contradicted.ConcurrencyToken, restarted.ConcurrencyToken);
        Assert.Null(restarted.Reference);
        await d.CompanionEpisodeAsync("Game.exe", 300);
        var fresh = (await d.LoadAsync())!;
        Assert.NotEqual(old.Reference!.EpisodeId, fresh.Reference!.EpisodeId);
        Assert.Null(fresh.Confirmation);
        Assert.False(await d.AcceptAsync());
        await d.PrepareAsync();
        await d.CompanionEpisodeAsync("Game.exe", 400);
        Assert.True(await d.AcceptAsync());
    }

    [Fact]
    public async Task Confirmation_survives_crash_before_acceptance()
    {
        using var d = await Driver.CreateAsync();
        await d.PairAsync();
        var before = (await d.LoadAsync())!;
        await d.RestartAsync();
        var after = (await d.LoadAsync())!;
        Assert.Equal(before.ConcurrencyToken, after.ConcurrencyToken);
        Assert.Equal(before.Reference!.EpisodeId, after.Reference!.EpisodeId);
        Assert.Equal(before.Confirmation!.EpisodeId, after.Confirmation!.EpisodeId);
        Assert.Equal(before.LastSequenceNumber, after.LastSequenceNumber);
        Assert.True(await d.AcceptAsync());
    }

    [Fact]
    public async Task Restart_after_acceptance_does_not_promote_twice()
    {
        using var d = await Driver.CreateAsync();
        await d.PairAsync();
        Assert.True(await d.AcceptAsync());
        var accepted = (await d.Signatures.GetAsync(d.Scope.GameId.Value, Ct))!;
        await d.RestartAsync();
        Assert.False(await d.AcceptAsync());
        Assert.Null((await d.LoadAsync())!.Reference);
        Assert.Null((await d.LoadAsync())!.Confirmation);
        Assert.Equal(accepted.Discovery, (await d.Signatures.GetAsync(d.Scope.GameId.Value, Ct))!.Discovery);
        Assert.Single(await d.Signatures.GetAllAsync(Ct));
    }

    [Fact]
    public async Task Revision_change_between_observation_and_acceptance_refuses()
    {
        using var d = await Driver.CreateAsync();
        await d.PairAsync();
        var proof = (await d.LoadAsync())!;
        d.RevisionResult = new(new FileRevision(99, T0), null);
        Assert.False(await d.AcceptAsync());
        await d.RestartAsync();
        Assert.Null(await d.Signatures.GetAsync(d.Scope.GameId.Value, Ct));
        var after = (await d.LoadAsync())!;
        Assert.Null(after.Reference);
        Assert.Null(after.Confirmation);
        Assert.NotEqual(proof.ConcurrencyToken, after.ConcurrencyToken);
    }

    [Fact]
    public async Task Generation_change_between_read_and_write_conflicts()
    {
        using var d = await Driver.CreateAsync();
        await d.PairAsync();
        d.BeforeRevisionReturns = async () =>
        {
            var state = (await d.LoadAsync())!;
            var scope = new InstallationScope(d.Scope.GameId, d.Scope.InstallationId, d.Scope.RootPath, Guid.NewGuid(), true);
            var inventory = new ExecutableInventory(scope, InventoryCompleteness.Complete, state.Inventory.Candidates, []);
            Assert.True(await new SqliteProcessSignatureLearningStore(d.Fixture.Options).TrySaveAsync(
                new(inventory, 1, Guid.NewGuid(), state.LastSequenceNumber, false, null, null, [DiscoveryReason.GenerationChanged]),
                state.ConcurrencyToken, Ct));
        };
        Assert.False(await d.AcceptAsync());
        Assert.Null(await new SqliteProcessSignatureStore(d.Fixture.Options).GetAsync(d.Scope.GameId.Value, Ct));
        Assert.Null((await d.LoadAsync())!.Reference);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Manual_edit_between_proof_and_acceptance_wins(bool duringRevision)
    {
        using var d = await Driver.CreateAsync();
        await d.PairAsync();
        var manual = new ProcessSignature(d.Scope.GameId.Value, [new("Manual.exe", ProcessSignatureEntryKind.Main)],
            ProcessSignatureOrigin.Manual, T0.AddHours(1));
        if (duringRevision) d.BeforeRevisionReturns = () => new SqliteProcessSignatureStore(d.Fixture.Options).UpsertAsync(manual, Ct);
        else await d.Signatures.UpsertAsync(manual, Ct);
        Assert.False(await d.AcceptAsync());
        var stored = (await new SqliteProcessSignatureStore(d.Fixture.Options).GetAsync(d.Scope.GameId.Value, Ct))!;
        Assert.Equal(ProcessSignatureOrigin.Manual, stored.Origin);
        Assert.Equal("Manual.exe", Assert.Single(stored.Entries).ExecutableName);
        Assert.Equal(manual.UpdatedAtUtc, stored.UpdatedAtUtc);
    }

    [Theory]
    [InlineData("ABORT")]
    [InlineData("IGNORE")]
    public async Task Acceptance_failure_rolls_back_signature_and_proof(string failure)
    {
        using var d = await Driver.CreateAsync();
        await d.PairAsync();
        var before = (await d.LoadAsync())!;
        await d.SqlAsync(failure == "ABORT"
            ? "CREATE TRIGGER fail_accept BEFORE UPDATE ON process_signature_learning BEGIN SELECT RAISE(ABORT, 'simulated failure'); END"
            : "CREATE TRIGGER fail_accept BEFORE UPDATE ON process_signature_learning BEGIN SELECT RAISE(IGNORE); END");
        if (failure == "ABORT") await Assert.ThrowsAsync<SqliteException>(() => d.AcceptAsync());
        else Assert.False(await d.AcceptAsync());
        var after = (await d.LoadAsync())!;
        Assert.Equal(before.ConcurrencyToken, after.ConcurrencyToken);
        Assert.Equal(before.Reference!.EpisodeId, after.Reference!.EpisodeId);
        Assert.Equal(before.Confirmation!.EpisodeId, after.Confirmation!.EpisodeId);
        Assert.Null(await new SqliteProcessSignatureStore(d.Fixture.Options).GetAsync(d.Scope.GameId.Value, Ct));
        await d.SqlAsync("DROP TRIGGER fail_accept");
        await d.RestartAsync();
        Assert.True(await d.AcceptAsync());
    }

    [Fact]
    public async Task Revalidated_path_requires_two_new_episodes()
    {
        using var d = await Driver.CreateAsync();
        await d.PairAsync();
        Assert.True(await d.AcceptAsync());
        var old = (await d.Signatures.GetAsync(d.Scope.GameId.Value, Ct))!;
        d.MoveInventory(@"C:\Games\Moved");
        await d.SqlAsync(@"UPDATE installations SET install_path='C:\Games\Moved'");
        await d.RestartAsync();
        var suspended = (await d.Signatures.GetAsync(d.Scope.GameId.Value, Ct))!;
        Assert.Equal(ProcessSignatureValidationState.NeedsRevalidation, suspended.Discovery!.ValidationState);
        Assert.Equal(@"C:\Games\Example\Game.exe", Assert.Single(suspended.Entries).ExecutablePath);
        Assert.False(await d.AcceptAsync());
        await d.EpisodeAsync(300);
        Assert.False(await d.AcceptAsync());
        await d.PrepareAsync();
        await d.EpisodeAsync(400);
        Assert.True(await d.AcceptAsync());
        var accepted = (await d.Signatures.GetAsync(d.Scope.GameId.Value, Ct))!;
        Assert.Equal(ProcessSignatureValidationState.Valid, accepted.Discovery!.ValidationState);
        Assert.Equal(@"C:\Games\Moved\Game.exe", Assert.Single(accepted.Entries).ExecutablePath);
        Assert.NotEqual(old.Discovery!.GenerationId, accepted.Discovery.GenerationId);
        Assert.NotEqual(suspended.Discovery.ConcurrencyToken, accepted.Discovery.ConcurrencyToken);
        Assert.Null((await d.LoadAsync())!.Reference);
    }

    [Fact]
    public async Task No_learning_row_update_for_stable_ticks()
    {
        using var d = await Driver.CreateAsync();
        await d.SqlAsync("CREATE TABLE learning_writes(n INTEGER); INSERT INTO learning_writes VALUES(0); CREATE TRIGGER count_learning AFTER UPDATE ON process_signature_learning BEGIN UPDATE learning_writes SET n=n+1; END");
        var before = (await d.LoadAsync())!;
        for (var tick = 0; tick < 20; tick++) await d.TickAsync();
        var process = d.Process(100);
        for (var tick = 0; tick < 20; tick++) await d.TickAsync(process);
        Assert.Equal(before.ConcurrencyToken, (await d.LoadAsync())!.ConcurrencyToken);
        Assert.Equal(0L, await d.ScalarAsync("SELECT n FROM learning_writes"));
        await d.TickAsync();
        await d.TickAsync();
        Assert.Equal(1L, await d.ScalarAsync("SELECT n FROM learning_writes"));
        Assert.NotNull((await d.LoadAsync())!.Reference);
    }

    [Fact]
    public async Task SessionRuntime_tracks_only_a_future_episode()
    {
        using var d = await Driver.CreateAsync();
        await d.PairAsync();
        Assert.True(await d.AcceptAsync());
        Assert.Empty(await d.Sessions.GetRecentAsync(10, Ct));
        var source = new Captures();
        var time = new Clock();
        var runtime = d.Runtime(source, time);
        source.Processes = [d.Process(300)];
        await runtime.RefreshAsync(Ct);
        Assert.Empty(await d.Sessions.GetRecentAsync(10, Ct));
        time.UtcNow = T0.AddHours(1).AddSeconds(2);
        await runtime.RefreshAsync(Ct);
        Assert.Single(await d.Sessions.GetRecentAsync(10, Ct));
        time.UtcNow = T0.AddHours(1).AddSeconds(8);
        await runtime.RefreshAsync(Ct);
        source.Processes = [];
        time.UtcNow = T0.AddHours(1).AddSeconds(10);
        await runtime.RefreshAsync(Ct);
        var session = Assert.Single(await d.Sessions.GetRecentAsync(10, Ct));
        Assert.Equal(T0.AddHours(1), session.ObservedStartedAtUtc);
        Assert.Equal(T0.AddHours(1).AddSeconds(8), session.ObservedEndedAtUtc);
        Assert.Equal(session.ObservedEndedAtUtc, session.LastSeenAtUtc);
        Assert.Equal(SessionState.Ended, session.State);
    }

    [Fact]
    public async Task Simultaneous_explicit_and_discovered_sessions_preserve_corrections()
    {
        using var d = await Driver.CreateAsync();
        await d.PairAsync();
        Assert.True(await d.AcceptAsync());
        var explicitGame = GameId.New();
        await d.Fixture.SeedInstallationAsync(explicitGame, InstallationId.New(), @"C:\Games\Explicit", Ct);
        await d.Signatures.UpsertAsync(new(explicitGame.Value, [new("Explicit.exe", ProcessSignatureEntryKind.Main)],
            ProcessSignatureOrigin.Manual, T0), Ct);
        var source = new Captures { Processes = [d.Process(300), new(400, "Explicit.exe", null, T0.AddHours(1))] };
        var time = new Clock();
        var runtime = d.Runtime(source, time);
        await runtime.RefreshAsync(Ct);
        time.UtcNow = T0.AddHours(1).AddSeconds(2);
        await runtime.RefreshAsync(Ct);
        Assert.Equal(2, (await d.Sessions.GetRecentAsync(10, Ct)).Count);
        time.UtcNow = T0.AddHours(1).AddSeconds(8);
        await runtime.RefreshAsync(Ct);
        source.Processes = [];
        time.UtcNow = T0.AddHours(1).AddSeconds(10);
        await runtime.RefreshAsync(Ct);
        var observed = await d.Sessions.GetRecentAsync(10, Ct);
        Assert.Equal(2, observed.Count);
        var corrections = new SqliteSessionCorrectionStore(d.Fixture.Options);
        foreach (var session in observed)
        {
            Assert.Equal(SessionState.Ended, session.State);
            await corrections.UpsertAsync(new(session.SessionId, T0.AddHours(1).AddSeconds(1),
                T0.AddHours(1).AddSeconds(7), T0.AddHours(2)), Ct);
        }
        // Another runtime/store instance must preserve observed data and manual overrides.
        await d.RestartAsync();
        await d.Runtime(source, time).RefreshAsync(Ct);
        var reloaded = await new SqliteSessionStore(d.Fixture.Options).GetRecentAsync(10, Ct);
        Assert.Equal(observed.OrderBy(s => s.SessionId), reloaded.OrderBy(s => s.SessionId));
        foreach (var session in reloaded)
        {
            var correction = await new SqliteSessionCorrectionStore(d.Fixture.Options).GetAsync(session.SessionId, Ct);
            var effective = new SessionCorrectionPolicy().Resolve(session, correction);
            Assert.Equal(T0.AddHours(1).AddSeconds(1), effective.StartedAtUtc);
            Assert.Equal(T0.AddHours(1).AddSeconds(7), effective.EndedAtUtc);
        }
    }

    private sealed class Driver : IDisposable, IExecutableInventorySource, IExecutableRevisionSource
    {
        private Driver(bool companion)
        {
            var scope = new InstallationScope(GameId.New(), InstallationId.New(), @"C:\Games\Example", Guid.NewGuid(), true);
            Inventory = new(scope, InventoryCompleteness.Complete,
                [new(@"C:\Games\Example\Game.exe", "Game.exe", new(10, T0.AddDays(-1)))], []);
            if (companion) Inventory = new(scope, InventoryCompleteness.Complete,
                [.. Inventory.Candidates, new(@"C:\Games\Example\Companion.exe", "Companion.exe", new(20, T0.AddDays(-1)))], []);
            Signatures = new(Fixture.Options);
            Learning = new(Fixture.Options);
            Sessions = new(Fixture.Options);
            Coordinator = new(Learning, Signatures, this, new());
        }
        public DiscoveryDatabaseFixture Fixture { get; } = new();
        public InstallationScope Scope => Inventory.Scope;
        public ExecutableInventory Inventory { get; private set; } = null!;
        public SqliteProcessSignatureStore Signatures { get; private set; }
        public SqliteProcessSignatureLearningStore Learning { get; private set; }
        public SqliteSessionStore Sessions { get; }
        public ProcessSignatureLearningCoordinator Coordinator { get; private set; }
        private long _capture;
        private long _time;
        public ExecutableRevisionResult? RevisionResult { get; set; }
        public Func<Task>? BeforeRevisionReturns { get; set; }
        public static async Task<Driver> CreateAsync(bool companion = false)
        {
            var d = new Driver(companion);
            await d.Fixture.InitializeAsync(Ct);
            await d.Fixture.SeedInstallationAsync(d.Scope.GameId, d.Scope.InstallationId, d.Scope.RootPath, Ct);
            await d.RestartAsync();
            return d;
        }
        public async Task RestartAsync()
        {
            Signatures = new(Fixture.Options);
            Learning = new(Fixture.Options);
            Coordinator = new(Learning, Signatures, this, new());
            var fresh = await InventoryAsync(Scope, Ct);
            var state = await Coordinator.InitializeAsync(new(fresh, false), Ct);
            Inventory = state.Inventory;
            _capture = 0;
            _time += 100;
        }
        public async Task EpisodeAsync(int pid)
        {
            await TickAsync();
            await TickAsync();
            var process = Process(pid);
            await TickAsync(process);
            await TickAsync(process);
            await TickAsync();
            await TickAsync();
        }
        public async Task PairAsync()
        {
            await EpisodeAsync(100);
            await RestartAsync();
            await EpisodeAsync(200);
        }
        public async Task PrepareAsync()
        {
            Assert.True(await Coordinator.PrepareEpisodeAsync(new(Inventory, false),
                Coordinator.GetState(Scope.InstallationId)!.ConcurrencyToken, Ct));
            Inventory = Coordinator.GetState(Scope.InstallationId)!.Inventory;
        }
        public async Task CompanionEpisodeAsync(string mainName, int pid)
        {
            await TickAsync();
            await TickAsync();
            var main = Process(pid, mainName);
            var companion = Process(pid + 1, mainName == "Game.exe" ? "Companion.exe" : "Game.exe");
            await TickAsync(companion);
            await TickAsync(companion, main);
            await TickAsync(main);
            await TickAsync(main);
            await TickAsync();
            await TickAsync();
        }
        public ProcessSnapshot Process(int pid, string name = "Game.exe") => new(pid, name,
            Inventory.Candidates.Single(c => c.ExecutableName == name).ExecutablePath, T0.AddSeconds(_time + 1));
        public Task<DiscoveryDecision?> TickAsync(params ProcessSnapshot[] processes) => Coordinator.ObserveAsync(Scope.InstallationId,
            new(++_capture, T0.AddSeconds(_time += 2), EpisodeQuality.Complete, processes), Ct);
        public Task<ProcessSignatureLearningState?> LoadAsync() => new SqliteProcessSignatureLearningStore(Fixture.Options).LoadAsync(Scope.InstallationId, Ct);
        public Task<bool> AcceptAsync() => new ProcessSignatureAcceptanceService(Learning, Signatures, Signatures,
            this, new(), _ => new(Inventory, false), new Clock()).TryAcceptAsync(Scope.InstallationId, Ct);
        public SessionRuntime Runtime(Captures source, Clock time) => new(source, Signatures, Sessions, new(), new(),
            new SqliteSessionCorrectionStore(Fixture.Options), new(), time,
            new DiscoveredSignatureValidator(Signatures, Learning, Signatures, this, _ => new(Inventory, false)));
        public Task<ExecutableInventory> InventoryAsync(InstallationScope scope, CancellationToken ct) => Task.FromResult(
            new ExecutableInventory(scope, Inventory.Completeness, Inventory.Candidates.ToArray(), Inventory.Issues.ToArray()));
        public async Task<ExecutableRevisionResult> ReadAsync(InstallationScope scope, string path, CancellationToken ct)
        {
            if (BeforeRevisionReturns is { } action)
            {
                BeforeRevisionReturns = null;
                await action();
            }
            return RevisionResult ?? new ExecutableRevisionResult(Inventory.Candidates.Single(c => c.ExecutablePath == path).Revision, null);
        }
        public void MoveInventory(string root)
        {
            var scope = new InstallationScope(Scope.GameId, Scope.InstallationId, root, Scope.GenerationId, true);
            Inventory = new(scope, InventoryCompleteness.Complete,
                Inventory.Candidates.Select(c => new ExecutableCandidate(root + "\\" + c.ExecutableName, c.ExecutableName, c.Revision)).ToArray(), []);
        }
        public async Task SqlAsync(string sql)
        {
            await using var connection = await Fixture.OpenAsync(Ct);
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync(Ct);
        }
        public async Task<object?> ScalarAsync(string sql)
        {
            await using var connection = await Fixture.OpenAsync(Ct);
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            return await command.ExecuteScalarAsync(Ct);
        }
        public void Dispose() => Fixture.Dispose();
    }

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = T0.AddHours(1);
        public override DateTimeOffset GetUtcNow() => UtcNow;
    }

    private sealed class Captures : IProcessSnapshotSource
    {
        public IReadOnlyList<ProcessSnapshot> Processes { get; set; } = [];
        public Task<IReadOnlyList<ProcessSnapshot>> CaptureAsync(CancellationToken ct) => Task.FromResult(Processes);
    }
}
