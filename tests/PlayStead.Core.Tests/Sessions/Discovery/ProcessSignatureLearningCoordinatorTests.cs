using System.Diagnostics;
using System.Globalization;
using PlayStead.Core.Library;
using PlayStead.Core.Sessions;
using PlayStead.Core.Sessions.Discovery;

namespace PlayStead.Core.Tests.Sessions.Discovery;

public sealed class ProcessSignatureLearningCoordinatorTests
{
    private static readonly DateTimeOffset T0 = DateTimeOffset.UnixEpoch;

    [Fact]
    public async Task Observation_trace_reports_correlation_learning_entry_and_reference_start_without_changing_state()
    {
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var listener = new TextWriterTraceListener(output);
        Trace.Listeners.Add(listener);
        try
        {
            var driver = await Driver.CreateAsync();
            await driver.CaptureAsync(1, []);
            await driver.CaptureAsync(2, []);

            var result = await driver.CaptureAsync(3, [driver.Process(100)]);

            Assert.Null(result);
            Assert.Null(driver.Coordinator.GetState(driver.Scope.InstallationId)!.Reference);
            listener.Flush();
            var trace = output.ToString();
            Assert.Contains("[PROCESS-FORENSIC] Correlation", trace, StringComparison.Ordinal);
            Assert.Contains($"GameId={driver.Scope.GameId}", trace, StringComparison.Ordinal);
            Assert.Contains("ProcessName=Game.exe", trace, StringComparison.Ordinal);
            Assert.Contains("InventoryNameMatch=true", trace, StringComparison.Ordinal);
            Assert.Contains("InventoryPathMatch=true", trace, StringComparison.Ordinal);
            Assert.Contains("InstallRootContained=true", trace, StringComparison.Ordinal);
            Assert.Contains("SupportExcluded=false", trace, StringComparison.Ordinal);
            Assert.Contains("ObservationForwarded=true", trace, StringComparison.Ordinal);
            Assert.Contains("[PROCESS-FORENSIC] LearningEntry", trace, StringComparison.Ordinal);
            Assert.Contains("HasReliablePath=true", trace, StringComparison.Ordinal);
            Assert.Contains("HasReliableIdentity=true", trace, StringComparison.Ordinal);
            Assert.Contains("Action=ReferenceStarted", trace, StringComparison.Ordinal);
        }
        finally
        {
            Trace.Listeners.Remove(listener);
        }
    }

    [Fact]
    public async Task Complete_episode_emits_reference_and_actual_ranges()
    {
        var driver = await Driver.CreateAsync();
        var result = await driver.CaptureAsync(1, []);
        Assert.Null(result);
        await driver.CaptureAsync(2, []);
        await driver.CaptureAsync(3, [driver.Process(100)]);
        await driver.CaptureAsync(4, [driver.Process(100)]);
        await driver.CaptureAsync(5, []);
        result = await driver.CaptureAsync(6, []);
        Assert.Equal(DiscoveryDecisionKind.InsufficientEvidence, result!.Kind);
        Assert.Equal([DiscoveryReason.AwaitingIndependentEpisode], result.Reasons);
        var reference = driver.Coordinator.GetState(driver.Scope.InstallationId)!.Reference!;
        Assert.Equal(new SnapshotRange(3, 4), Assert.Single(reference.Candidates).PresenceRanges[0]);
        Assert.Equal(1, reference.FirstSnapshot);
        Assert.Equal(6, reference.LastSnapshot);
        Assert.Equal(T0.AddSeconds(6), reference.StartedAtUtc);
        Assert.Equal(T0.AddSeconds(12), reference.EndedAtUtc);
        Assert.Null(driver.Coordinator.GetState(driver.Scope.InstallationId)!.Confirmation);
    }

    [Fact]
    public async Task reference_survives_start_of_second_episode()
    {
        var driver = await Driver.CreateAsync();
        await driver.CompleteAsync(1, 100);
        var original = await driver.LearningStore.LoadAsync(driver.Scope.InstallationId, CancellationToken.None);
        var writes = driver.LearningStore.WriteCount;
        Assert.True(await driver.PrepareAsync());
        await driver.CaptureAsync(7, [driver.Process(200)]);
        var after = await driver.LearningStore.LoadAsync(driver.Scope.InstallationId, CancellationToken.None);
        Assert.Same(original!.Reference, after!.Reference);
        Assert.Null(after.Confirmation);
        Assert.Equal(original.LastSequenceNumber, after.LastSequenceNumber);
        Assert.Equal(original.ConcurrencyToken, after.ConcurrencyToken);
        Assert.Equal(writes, driver.LearningStore.WriteCount);
    }

    [Fact]
    public async Task second_qualifying_episode_confirms_persisted_reference()
    {
        var driver = await Driver.CreateAsync();
        await driver.CompleteAsync(1, 100);
        var first = driver.Coordinator.GetState(driver.Scope.InstallationId)!.Reference!;
        Assert.True(await driver.PrepareAsync());
        await driver.CaptureAsync(7, [driver.Process(200)]);
        await driver.CaptureAsync(8, [driver.Process(200)]);
        await driver.CaptureAsync(9, []);
        var result = await driver.CaptureAsync(10, []);
        Assert.Equal(DiscoveryDecisionKind.PromoteMain, result!.Kind);
        var state = await driver.LearningStore.LoadAsync(driver.Scope.InstallationId, CancellationToken.None);
        Assert.Same(first, state!.Reference);
        Assert.NotNull(state.Confirmation);
        Assert.NotEqual(first.EpisodeId, state.Confirmation.EpisodeId);
        Assert.Equal(first.SequenceNumber + 1, state.Confirmation.SequenceNumber);
        Assert.True(state.Confirmation.StartedAtUtc > first.EndedAtUtc);
        Assert.Equal(2, state.LastSequenceNumber);
    }

    [Fact]
    public async Task capture_gap_during_second_episode_does_not_promote()
    {
        var driver = await Driver.CreateAsync();
        await driver.CompleteAsync(1, 100);
        var oldToken = driver.Coordinator.GetState(driver.Scope.InstallationId)!.ConcurrencyToken;
        Assert.True(await driver.PrepareAsync());
        await driver.CaptureAsync(7, [driver.Process(200)]);
        var result = await driver.CaptureAsync(9, [driver.Process(200)]);
        Assert.Contains(DiscoveryReason.CaptureGap, result!.Reasons);
        var state = await driver.LearningStore.LoadAsync(driver.Scope.InstallationId, CancellationToken.None);
        Assert.Null(state!.Reference);
        Assert.Null(state.Confirmation);
        Assert.NotEqual(oldToken, state.ConcurrencyToken);
        Assert.False(await driver.Coordinator.PrepareEpisodeAsync(new DiscoveryInventoryContext(driver.Inventory, false), oldToken, CancellationToken.None));
    }

    [Fact]
    public async Task Cancellation_during_invalidation_save_quarantines_reference_until_durable()
    {
        var d = await Driver.CreateAsync();
        await d.CompleteAsync(1, 100);
        var old = d.Coordinator.GetState(d.Scope.InstallationId)!;
        using var cancellation = new CancellationTokenSource();
        d.LearningStore.CancelSaveWith = cancellation;
        var rupture = new ProcessObservationBatch(7, T0.AddSeconds(14), EpisodeQuality.Complete,
            [new ProcessSnapshot(200, "Game.exe", null, T0.AddSeconds(13))]);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => d.Coordinator.ObserveAsync(
            d.Scope.InstallationId, rupture, cancellation.Token));
        Assert.Null(d.Coordinator.GetState(d.Scope.InstallationId));
        Assert.Same(old.Reference,
            (await d.LearningStore.LoadAsync(d.Scope.InstallationId, CancellationToken.None))!.Reference);
        await d.CaptureAsync(8, []);
        var durable = await d.LearningStore.LoadAsync(d.Scope.InstallationId, CancellationToken.None);
        Assert.Null(durable!.Reference);
        Assert.Null(durable.Confirmation);
        Assert.NotEqual(old.ConcurrencyToken, durable.ConcurrencyToken);
    }

    [Fact]
    public async Task Cancellation_during_empty_proof_invalidation_keeps_reason_pending()
    {
        var d = await Driver.CreateAsync();
        using var cancellation = new CancellationTokenSource();
        d.LearningStore.CancelSaveWith = cancellation;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => d.Coordinator.ObserveAsync(
            d.Scope.InstallationId,
            new ProcessObservationBatch(1, T0.AddSeconds(2), EpisodeQuality.CaptureGap, []),
            cancellation.Token));
        Assert.Null(d.Coordinator.GetState(d.Scope.InstallationId));
        await d.CaptureAsync(2, []);
        Assert.Equal([DiscoveryReason.CaptureGap],
            d.Coordinator.GetState(d.Scope.InstallationId)!.Reasons);
    }

    [Fact]
    public async Task Cancellation_during_changed_inventory_initialization_hides_old_proof()
    {
        var d = await Driver.CreateAsync();
        await d.CompleteAsync(1, 100);
        var movedScope = new InstallationScope(d.Scope.GameId, d.Scope.InstallationId,
            @"C:\Games\Moved", d.Scope.GenerationId, true);
        var moved = new ExecutableInventory(movedScope, InventoryCompleteness.Complete,
            [new ExecutableCandidate(@"C:\Games\Moved\Game.exe", "Game.exe", new FileRevision(10, T0))], []);
        using var cancellation = new CancellationTokenSource();
        d.LearningStore.CancelSaveWith = cancellation;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => d.Coordinator.InitializeAsync(
            new DiscoveryInventoryContext(moved, false), cancellation.Token));
        Assert.Null(d.Coordinator.GetState(d.Scope.InstallationId));
        await d.CaptureAsync(8, []);
        var durable = await d.LearningStore.LoadAsync(d.Scope.InstallationId,
            CancellationToken.None);
        Assert.NotNull(durable);
        Assert.Null(durable.Reference);
        Assert.Equal([DiscoveryReason.ScopeChanged], durable.Reasons);
    }

    [Fact]
    public async Task Retried_pending_initialization_keeps_authoritative_new_generation()
    {
        var d = await Driver.CreateAsync();
        await d.CompleteAsync(1, 100);
        var movedScope = new InstallationScope(d.Scope.GameId, d.Scope.InstallationId,
            @"C:\Games\Moved", d.Scope.GenerationId, true);
        var moved = new ExecutableInventory(movedScope, InventoryCompleteness.Complete,
            [new ExecutableCandidate(@"C:\Games\Moved\Game.exe", "Game.exe", new FileRevision(10, T0))], []);
        using var cancellation = new CancellationTokenSource();
        d.LearningStore.CancelSaveWith = cancellation;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => d.Coordinator.InitializeAsync(
            new DiscoveryInventoryContext(moved, false), cancellation.Token));
        var retried = await d.Coordinator.InitializeAsync(new DiscoveryInventoryContext(moved, false),
            CancellationToken.None);
        Assert.NotEqual(d.Scope.GenerationId, retried.Inventory.Scope.GenerationId);
        Assert.Equal([DiscoveryReason.ScopeChanged], retried.Reasons);
        Assert.Null(retried.Reference);
    }

    [Fact]
    public async Task Cancellation_while_waiting_for_gate_abandons_only_current_episode()
    {
        var d = await Driver.CreateAsync();
        await d.CompleteAsync(1, 100);
        var reference = d.Coordinator.GetState(d.Scope.InstallationId)!.Reference;
        Assert.True(await d.PrepareAsync());
        await d.CaptureAsync(7, [d.Process(200)]);
        d.RevisionSource.HoldNextRead();
        var held = d.CaptureAsync(8, [d.Process(200)]);
        await d.RevisionSource.WaitForHeldReadAsync();
        using var cancellation = new CancellationTokenSource();
        var queued = d.Coordinator.ObserveAsync(d.Scope.InstallationId,
            new ProcessObservationBatch(9, T0.AddSeconds(18), EpisodeQuality.Complete, []),
            cancellation.Token);
        cancellation.Cancel();
        d.RevisionSource.ReleaseHeldRead();
        await held;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued);
        await d.CaptureAsync(10, [d.Process(200)]);
        await d.CaptureAsync(11, []);
        await d.CaptureAsync(12, []);
        Assert.Same(reference, d.Coordinator.GetState(d.Scope.InstallationId)!.Reference);
        Assert.Null(d.Coordinator.GetState(d.Scope.InstallationId)!.Confirmation);
    }

    [Fact]
    public async Task Contradictory_third_episode_invalidates_completed_pair()
    {
        var d = await Driver.CreateAsync("Game.exe", "Companion.exe");
        await QualifiedWithCompanionAsync(d, 1, "Game.exe", "Companion.exe", 100);
        Assert.True(await d.PrepareAsync());
        await QualifiedWithCompanionAsync(d, 7, "Game.exe", "Companion.exe", 200,
            leadingAbsences: false);
        var pair = d.Coordinator.GetState(d.Scope.InstallationId)!;
        Assert.NotNull(pair.Reference);
        Assert.NotNull(pair.Confirmation);
        Assert.True(await d.PrepareAsync());
        await QualifiedWithCompanionAsync(d, 13, "Companion.exe", "Game.exe", 300,
            leadingAbsences: false);
        var durable = await d.LearningStore.LoadAsync(d.Scope.InstallationId, CancellationToken.None);
        Assert.Null(durable!.Reference);
        Assert.Null(durable.Confirmation);
        Assert.Equal([DiscoveryReason.ConflictingEpisodes], durable.Reasons);
        Assert.NotEqual(pair.ConcurrencyToken, durable.ConcurrencyToken);
    }

    [Fact]
    public async Task Compatible_third_episode_does_not_rotate_completed_pair()
    {
        var d = await Driver.CreateAsync("Game.exe", "Companion.exe");
        await QualifiedWithCompanionAsync(d, 1, "Game.exe", "Companion.exe", 100);
        Assert.True(await d.PrepareAsync());
        await QualifiedWithCompanionAsync(d, 7, "Game.exe", "Companion.exe", 200,
            leadingAbsences: false);
        var pair = d.Coordinator.GetState(d.Scope.InstallationId)!;
        var writes = d.LearningStore.WriteCount;
        Assert.True(await d.PrepareAsync());
        await QualifiedWithCompanionAsync(d, 13, "Game.exe", "Companion.exe", 300,
            leadingAbsences: false);
        var after = await d.LearningStore.LoadAsync(d.Scope.InstallationId, CancellationToken.None);
        Assert.Same(pair.Reference, after!.Reference);
        Assert.Same(pair.Confirmation, after.Confirmation);
        Assert.Equal(pair.ConcurrencyToken, after.ConcurrencyToken);
        Assert.Equal(pair.LastSequenceNumber, after.LastSequenceNumber);
        Assert.Equal(writes, d.LearningStore.WriteCount);
    }

    [Fact]
    public async Task Two_absent_batches_are_required_before_episode()
    {
        var d = await Driver.CreateAsync();
        await d.CaptureAsync(1, []);
        await d.CaptureAsync(2, [d.Process(100)]);
        await d.CaptureAsync(3, [d.Process(100)]);
        await d.CaptureAsync(4, []);
        await d.CaptureAsync(5, []);
        Assert.Null(d.Coordinator.GetState(d.Scope.InstallationId)!.Reference);
        await d.CaptureAsync(6, [d.Process(200)]);
        await d.CaptureAsync(7, [d.Process(200)]);
        await d.CaptureAsync(8, []);
        await d.CaptureAsync(9, []);
        Assert.NotNull(d.Coordinator.GetState(d.Scope.InstallationId)!.Reference);
    }

    [Fact]
    public async Task One_absent_batch_is_not_enough()
    {
        var d = await Driver.CreateAsync();
        await d.CaptureAsync(1, []);
        await d.CaptureAsync(2, [d.Process(100)]);
        await d.CaptureAsync(3, []);
        Assert.Null(d.Coordinator.GetState(d.Scope.InstallationId)!.Reference);
    }

    [Fact]
    public async Task Already_running_at_startup_is_partial()
    {
        var d = await Driver.CreateAsync();
        await d.CaptureAsync(1, [d.Process(100)]);
        await d.CaptureAsync(2, [d.Process(100)]);
        await d.CaptureAsync(3, []);
        await d.CaptureAsync(4, []);
        Assert.Null(d.Coordinator.GetState(d.Scope.InstallationId)!.Reference);
        await d.CaptureAsync(5, [d.Process(200)]);
        await d.CaptureAsync(6, [d.Process(200)]);
        await d.CaptureAsync(7, []);
        await d.CaptureAsync(8, []);
        Assert.NotNull(d.Coordinator.GetState(d.Scope.InstallationId)!.Reference);
    }

    [Fact]
    public async Task Two_final_absences_close_once()
    {
        var d = await Driver.CreateAsync();
        await d.CompleteAsync(1, 100);
        var writes = d.LearningStore.WriteCount;
        await d.CaptureAsync(7, []);
        await d.CaptureAsync(8, []);
        Assert.Equal(writes, d.LearningStore.WriteCount);
    }

    [Fact]
    public async Task One_final_absence_does_not_complete()
    {
        var d = await Driver.CreateAsync();
        await d.CaptureAsync(1, []);
        await d.CaptureAsync(2, []);
        await d.CaptureAsync(3, [d.Process(100)]);
        await d.CaptureAsync(4, [d.Process(100)]);
        await d.CaptureAsync(5, []);
        Assert.Null(d.Coordinator.GetState(d.Scope.InstallationId)!.Reference);
        await d.CaptureAsync(6, [d.Process(100)]);
        await d.CaptureAsync(7, []);
        await d.CaptureAsync(8, []);
        Assert.Equal([new SnapshotRange(3, 4), new SnapshotRange(6, 6)],
            d.Coordinator.GetState(d.Scope.InstallationId)!.Reference!.Candidates[0].PresenceRanges);
    }

    [Fact]
    public async Task Trailing_absences_arm_next_episode_without_extra_threshold()
    {
        var d = await Driver.CreateAsync();
        await d.CompleteAsync(1, 100);
        Assert.True(await d.PrepareAsync());
        await d.CaptureAsync(7, [d.Process(200)]);
        await d.CaptureAsync(8, [d.Process(200)]);
        await d.CaptureAsync(9, []);
        var result = await d.CaptureAsync(10, []);
        Assert.Equal(DiscoveryDecisionKind.PromoteMain, result!.Kind);
        Assert.Equal(5, d.Coordinator.GetState(d.Scope.InstallationId)!.Confirmation!.FirstSnapshot);
    }

    [Fact]
    public async Task New_episode_requires_fresh_preparation()
    {
        var d = await Driver.CreateAsync();
        await d.CompleteAsync(1, 100);
        await d.CaptureAsync(7, [d.Process(200)]);
        await d.CaptureAsync(8, [d.Process(200)]);
        await d.CaptureAsync(9, []);
        await d.CaptureAsync(10, []);
        Assert.Null(d.Coordinator.GetState(d.Scope.InstallationId)!.Confirmation);
        Assert.NotNull(d.Coordinator.GetState(d.Scope.InstallationId)!.Reference);
    }

    [Fact]
    public async Task Stable_ticks_do_not_write_learning_state()
    {
        var d = await Driver.CreateAsync();
        var initial = d.LearningStore.WriteCount;
        await d.CaptureAsync(1, []);
        await d.CaptureAsync(2, []);
        await d.CaptureAsync(3, [d.Process(100)]);
        await d.CaptureAsync(4, [d.Process(100)]);
        await d.CaptureAsync(5, []);
        Assert.Equal(initial, d.LearningStore.WriteCount);
        await d.CaptureAsync(6, []);
        Assert.Equal(initial + 1, d.LearningStore.WriteCount);
    }

    [Fact]
    public async Task Nonmonotonic_capture_is_not_continuity()
    {
        var d = await Driver.CreateAsync();
        await d.CompleteAsync(1, 100);
        var result = await d.CaptureAsync(6, []);
        Assert.Equal([DiscoveryReason.CaptureGap], result!.Reasons);
        Assert.Null((await d.LearningStore.LoadAsync(d.Scope.InstallationId, CancellationToken.None))!.Reference);
    }

    [Fact]
    public async Task Unknown_path_invalidates()
    {
        var d = await Driver.CreateAsync();
        await d.CompleteAsync(1, 100);
        var result = await d.CaptureAsync(7, [new ProcessSnapshot(200, "Game.exe", null, T0.AddSeconds(200))]);
        Assert.NotNull(result);
        Assert.Equal([DiscoveryReason.UnreliablePath], result!.Reasons);
        Assert.Null(d.Coordinator.GetState(d.Scope.InstallationId)!.Reference);
    }

    [Fact]
    public async Task Pathless_unreal_battleye_bootstrap_does_not_invalidate_family_reference()
    {
        var d = await Driver.CreateUnrealAsync("DuneSandbox",
            @"DuneSandbox\Binaries\Win64\DuneSandbox_BE.exe");
        await d.CaptureAsync(1, []);
        await d.CaptureAsync(2, []);
        await d.CaptureAsync(3, [d.Process("DuneSandbox-Win64-Shipping.exe", 100)]);
        await d.CaptureAsync(4, [d.Process("DuneSandbox-Win64-Shipping.exe", 100)]);
        await d.CaptureAsync(5, []);
        await d.CaptureAsync(6, []);
        Assert.NotNull(d.Coordinator.GetState(d.Scope.InstallationId)!.Reference);

        var result = await d.CaptureAsync(7,
        [
            new ProcessSnapshot(200, "DuneSandbox_BE.exe", null, T0.AddSeconds(13))
        ]);

        Assert.Null(result);
        Assert.NotNull(d.Coordinator.GetState(d.Scope.InstallationId)!.Reference);
    }

    [Theory]
    [InlineData("DuneSandbox.exe")]
    [InlineData("DuneSandbox-Win64-Shipping.exe")]
    public async Task First_pathless_unreal_family_observation_remains_unreliable(
        string observedName)
    {
        var d = await Driver.CreateUnrealAsync("DuneSandbox");

        await d.CaptureAsync(1, []);
        await d.CaptureAsync(2, []);
        var result = await d.CaptureAsync(3,
            [new ProcessSnapshot(100, observedName, null, T0.AddSeconds(5))]);

        Assert.NotNull(result);
        Assert.Equal([DiscoveryReason.UnreliablePath], result!.Reasons);
        Assert.Null(d.Coordinator.GetState(d.Scope.InstallationId)!.Reference);
    }

    [Fact]
    public async Task Pathless_observation_continues_only_the_currently_bound_process_identity()
    {
        var d = await Driver.CreateAsync();
        var startedAt = T0.AddSeconds(5);
        var known = new ProcessSnapshot(100, "Game.exe", d.Inventory.Candidates[0].ExecutablePath, startedAt);
        await d.CaptureAsync(1, []);
        await d.CaptureAsync(2, []);
        await d.CaptureAsync(3, [known]);

        var continuity = await d.CaptureAsync(4,
            [new ProcessSnapshot(100, "Game.exe", null, startedAt)]);
        await d.CaptureAsync(5, [known]);
        await d.CaptureAsync(6, []);
        var completed = await d.CaptureAsync(7, []);

        Assert.Null(continuity);
        Assert.Equal(DiscoveryReason.AwaitingIndependentEpisode, Assert.Single(completed!.Reasons));
        Assert.NotNull(d.Coordinator.GetState(d.Scope.InstallationId)!.Reference);
    }

    [Fact]
    public async Task Pathless_observation_with_same_pid_and_different_start_time_is_unreliable()
    {
        var d = await Driver.CreateAsync();
        await d.CaptureAsync(1, []);
        await d.CaptureAsync(2, []);
        await d.CaptureAsync(3, [new ProcessSnapshot(100, "Game.exe",
            d.Inventory.Candidates[0].ExecutablePath, T0.AddSeconds(5))]);

        var result = await d.CaptureAsync(4,
            [new ProcessSnapshot(100, "Game.exe", null, T0.AddSeconds(6))]);

        Assert.Equal([DiscoveryReason.UnreliablePath], result!.Reasons);
    }

    [Fact]
    public async Task Duplicate_candidate_names_are_not_resolved_from_a_pathless_continuation()
    {
        const string root = @"C:\Games\Example";
        var d = await Driver.CreateWithCandidatesAsync(
            ("Game.exe", root + @"\Game.exe"),
            ("Game.exe", root + @"\Tools\Game.exe"));
        var startedAt = T0.AddSeconds(5);
        await d.CaptureAsync(1, []);
        await d.CaptureAsync(2, []);
        await d.CaptureAsync(3, [new ProcessSnapshot(100, "Game.exe",
            d.Inventory.Candidates[0].ExecutablePath, startedAt)]);

        var result = await d.CaptureAsync(4,
            [new ProcessSnapshot(100, "Game.exe", null, startedAt)]);

        Assert.Equal([DiscoveryReason.UnreliablePath], result!.Reasons);
    }

    [Fact]
    public async Task Identity_bound_to_multiple_candidates_is_not_resolved_pathlessly()
    {
        const string root = @"C:\Games\Example";
        var d = await Driver.CreateWithCandidatesAsync(
            ("Alpha.exe", root + @"\Alpha.exe"),
            ("Beta.exe", root + @"\Beta.exe"));
        var startedAt = T0.AddSeconds(5);
        await d.CaptureAsync(1, []);
        await d.CaptureAsync(2, []);
        await d.CaptureAsync(3,
        [
            new ProcessSnapshot(100, "Alpha.exe", d.Inventory.Candidates[0].ExecutablePath, startedAt),
            new ProcessSnapshot(100, "Beta.exe", d.Inventory.Candidates[1].ExecutablePath, startedAt)
        ]);

        var result = await d.CaptureAsync(4,
            [new ProcessSnapshot(100, "Alpha.exe", null, startedAt)]);

        Assert.Equal([DiscoveryReason.UnreliablePath], result!.Reasons);
    }

    [Fact]
    public async Task Pathless_generic_support_observation_is_ignored_without_starting_learning()
    {
        const string root = @"C:\Games\Example";
        var d = await Driver.CreateWithCandidatesAsync(
            ("SeaOfThieves.exe", root + @"\SeaOfThieves.exe"),
            ("UnrealCEFSubProcess.exe", root + @"\Engine\Binaries\Win64\UnrealCEFSubProcess.exe"));
        await d.CaptureAsync(1, []);
        await d.CaptureAsync(2, []);

        var result = await d.CaptureAsync(3,
            [new ProcessSnapshot(200, "UnrealCEFSubProcess.exe", null, T0.AddSeconds(8))]);

        Assert.Null(result);
        Assert.Null(d.Coordinator.GetState(d.Scope.InstallationId)!.Reference);
        Assert.Empty(d.Coordinator.GetState(d.Scope.InstallationId)!.Reasons);
    }

    [Theory]
    [InlineData(@"DuneSandbox\Binaries\Win64\DuneSandbox_BE.exe", "DuneSandbox_BE.exe")]
    [InlineData(@"Engine\Binaries\Win64\CrashReportClient.exe", "CrashReportClient.exe")]
    public async Task Pathless_unreal_support_process_cannot_start_an_episode(
        string relativePath, string observedName)
    {
        var d = await Driver.CreateUnrealAsync("DuneSandbox", relativePath);

        await d.CaptureAsync(1, []);
        await d.CaptureAsync(2, []);
        await d.CaptureAsync(3,
            [new ProcessSnapshot(100, observedName, null, T0.AddSeconds(5))]);
        await d.CaptureAsync(4,
            [new ProcessSnapshot(100, observedName, null, T0.AddSeconds(5))]);
        await d.CaptureAsync(5, []);
        var result = await d.CaptureAsync(6, []);

        Assert.Null(result);
        Assert.Null(d.Coordinator.GetState(d.Scope.InstallationId)!.Reference);
    }

    [Fact]
    public async Task Pathless_unreal_name_shared_by_unrelated_inventory_candidates_is_rejected()
    {
        var d = await Driver.CreateUnrealAsync("DuneSandbox", @"Tools\DuneSandbox.exe");
        await d.CaptureAsync(1, []);
        await d.CaptureAsync(2, []);

        var result = await d.CaptureAsync(3,
            [new ProcessSnapshot(100, "DuneSandbox.exe", null, T0.AddSeconds(5))]);

        Assert.Equal([DiscoveryReason.UnreliablePath], result!.Reasons);
        Assert.Null(d.Coordinator.GetState(d.Scope.InstallationId)!.Reference);
    }

    [Fact]
    public async Task Pathless_unreal_name_with_shipping_outside_install_root_is_rejected()
    {
        var d = await Driver.CreateWithCandidatesAsync(
            ("DuneSandbox.exe", @"C:\Games\Example\DuneSandbox.exe"),
            ("DuneSandbox-Win64-Shipping.exe",
                @"C:\Other\DuneSandbox\Binaries\Win64\DuneSandbox-Win64-Shipping.exe"));
        await d.CaptureAsync(1, []);
        await d.CaptureAsync(2, []);

        var result = await d.CaptureAsync(3,
            [new ProcessSnapshot(100, "DuneSandbox.exe", null, T0.AddSeconds(5))]);

        Assert.Equal([DiscoveryReason.UnreliablePath], result!.Reasons);
        Assert.Null(d.Coordinator.GetState(d.Scope.InstallationId)!.Reference);
    }

    [Fact]
    public async Task Two_reliably_observed_unreal_family_episodes_still_require_confirmation()
    {
        var d = await Driver.CreateUnrealAsync("Test_C");
        async Task<DiscoveryDecision?> CompleteAsync(long first, int pid)
        {
            await d.CaptureAsync(first, []);
            await d.CaptureAsync(first + 1, []);
            await d.CaptureAsync(first + 2, [d.Process("Test_C-Win64-Shipping.exe", pid)]);
            await d.CaptureAsync(first + 3, [d.Process("Test_C-Win64-Shipping.exe", pid)]);
            await d.CaptureAsync(first + 4, []);
            return await d.CaptureAsync(first + 5, []);
        }

        var reference = await CompleteAsync(1, 100);
        Assert.Equal(DiscoveryDecisionKind.InsufficientEvidence, reference!.Kind);
        Assert.Null(d.Coordinator.GetState(d.Scope.InstallationId)!.Confirmation);
        Assert.True(await d.PrepareAsync());

        var confirmation = await CompleteAsync(7, 200);

        Assert.Equal(DiscoveryDecisionKind.PromoteMain, confirmation!.Kind);
        Assert.Equal("Test_C-Win64-Shipping.exe", confirmation.Main!.ExecutableName);
        Assert.NotNull(d.Coordinator.GetState(d.Scope.InstallationId)!.Confirmation);
    }

    [Fact]
    public async Task Unknown_started_identity_invalidates()
    {
        var d = await Driver.CreateAsync();
        await d.CompleteAsync(1, 100);
        var result = await d.CaptureAsync(7, [new ProcessSnapshot(200, "Game.exe",
            d.Inventory.Candidates[0].ExecutablePath, null)]);
        Assert.Equal([DiscoveryReason.UnknownProcessIdentity], result!.Reasons);
        Assert.Null(d.Coordinator.GetState(d.Scope.InstallationId)!.Reference);
    }

    [Fact]
    public async Task Pid_reuse_is_not_stable_presence()
    {
        var d = await Driver.CreateAsync();
        await d.CaptureAsync(1, []);
        await d.CaptureAsync(2, []);
        await d.CaptureAsync(3, [d.Process(100)]);
        var result = await d.CaptureAsync(4, [new ProcessSnapshot(100, "Game.exe",
            d.Inventory.Candidates[0].ExecutablePath, T0.AddSeconds(101))]);
        Assert.Equal([DiscoveryReason.UnknownProcessIdentity], result!.Reasons);
        Assert.Null(d.Coordinator.GetState(d.Scope.InstallationId)!.Reference);
    }

    [Fact]
    public async Task Same_pid_new_started_time_is_not_stable_presence()
    {
        var d = await Driver.CreateAsync();
        await d.CaptureAsync(1, []);
        await d.CaptureAsync(2, []);
        await d.CaptureAsync(3, [d.Process(100)]);
        await d.CaptureAsync(4, []);
        var result = await d.CaptureAsync(5, [new ProcessSnapshot(100, "Game.exe",
            d.Inventory.Candidates[0].ExecutablePath, T0.AddSeconds(9))]);
        Assert.Equal([DiscoveryReason.UnknownProcessIdentity], result!.Reasons);
        Assert.Null(d.Coordinator.GetState(d.Scope.InstallationId)!.Reference);
    }

    [Fact]
    public async Task New_unassignable_identity_invalidates()
    {
        var d = await Driver.CreateAsync();
        await d.CaptureAsync(1, []);
        await d.CaptureAsync(2, []);
        await d.CaptureAsync(3, [d.Process(100)]);
        var result = await d.CaptureAsync(4, [d.Process(100),
            new ProcessSnapshot(200, "Game.exe", d.Inventory.Candidates[0].ExecutablePath, T0.AddSeconds(200))]);
        Assert.Equal([DiscoveryReason.UnknownProcessIdentity], result!.Reasons);
    }

    [Fact]
    public async Task Known_unrelated_process_is_not_a_candidate()
    {
        var d = await Driver.CreateAsync();
        var unrelated = new ProcessSnapshot(9, "Browser.exe", @"C:\Program Files\Browser\Browser.exe", T0);
        await d.CaptureAsync(1, [unrelated]);
        await d.CaptureAsync(2, [unrelated]);
        await d.CaptureAsync(3, [d.Process(100), unrelated]);
        await d.CaptureAsync(4, [d.Process(100), unrelated]);
        await d.CaptureAsync(5, [unrelated]);
        await d.CaptureAsync(6, [unrelated]);
        Assert.NotNull(d.Coordinator.GetState(d.Scope.InstallationId)!.Reference);
    }

    [Fact]
    public async Task New_path_under_root_requires_inventory()
    {
        var d = await Driver.CreateAsync();
        await d.CompleteAsync(1, 100);
        var result = await d.CaptureAsync(7, [new ProcessSnapshot(200, "New.exe",
            @"C:\Games\Example\New.exe", T0.AddSeconds(200))]);
        Assert.Equal([DiscoveryReason.IncompleteInventory], result!.Reasons);
        var state = d.Coordinator.GetState(d.Scope.InstallationId)!;
        Assert.Equal(InventoryCompleteness.Incomplete, state.Inventory.Completeness);
        Assert.NotEqual(d.Scope.GenerationId, state.Inventory.Scope.GenerationId);
        Assert.Equal(@"C:\Games\Example\New.exe", Assert.Single(state.Inventory.Issues).Path);
        Assert.Null(state.Reference);
    }

    [Fact]
    public async Task Unreliable_new_path_is_identity_failure_before_inventory_claim()
    {
        var d = await Driver.CreateAsync();
        await d.CompleteAsync(1, 100);
        var result = await d.CaptureAsync(7, [new ProcessSnapshot(200, "New.exe",
            @"C:\Games\Example\New.exe", null)]);
        Assert.Equal([DiscoveryReason.UnknownProcessIdentity], result!.Reasons);
        Assert.Equal(InventoryCompleteness.Complete,
            d.Coordinator.GetState(d.Scope.InstallationId)!.Inventory.Completeness);
    }

    [Fact]
    public async Task Changed_revision_discards_episode()
    {
        var d = await Driver.CreateAsync();
        await d.CompleteAsync(1, 100);
        d.RevisionSource.Results[d.Inventory.Candidates[0].ExecutablePath] =
            new ExecutableRevisionResult(new FileRevision(11, T0), null);
        var result = await d.CaptureAsync(7, [d.Process(200)]);
        Assert.Equal([DiscoveryReason.RevisionChanged], result!.Reasons);
        Assert.Equal(InventoryCompleteness.Incomplete, d.Coordinator.GetState(d.Scope.InstallationId)!.Inventory.Completeness);
        Assert.Null(d.Coordinator.GetState(d.Scope.InstallationId)!.Reference);
    }

    [Fact]
    public async Task Inaccessible_revision_discards_episode()
    {
        var d = await Driver.CreateAsync();
        await d.CompleteAsync(1, 100);
        d.RevisionSource.Results[d.Inventory.Candidates[0].ExecutablePath] =
            new ExecutableRevisionResult(null, new InventoryIssue(d.Inventory.Candidates[0].ExecutablePath,
                InventoryIssueKind.AccessDenied));
        var result = await d.CaptureAsync(7, [d.Process(200)]);
        Assert.Equal([DiscoveryReason.UnreliablePath], result!.Reasons);
        Assert.Equal(InventoryCompleteness.Incomplete, d.Coordinator.GetState(d.Scope.InstallationId)!.Inventory.Completeness);
    }

    [Fact]
    public async Task Changed_generation_discards_reference()
    {
        var d = await Driver.CreateAsync();
        await d.CompleteAsync(1, 100);
        var nextScope = new InstallationScope(d.Scope.GameId, d.Scope.InstallationId,
            d.Scope.RootPath, Guid.NewGuid(), true);
        var nextInventory = new ExecutableInventory(nextScope, InventoryCompleteness.Complete,
            d.Inventory.Candidates, []);
        Assert.True(await d.Coordinator.PrepareEpisodeAsync(new DiscoveryInventoryContext(nextInventory, false),
            d.Coordinator.GetState(d.Scope.InstallationId)!.ConcurrencyToken, CancellationToken.None));
        Assert.Null((await d.LearningStore.LoadAsync(d.Scope.InstallationId, CancellationToken.None))!.Reference);
    }

    [Fact]
    public async Task Stale_preparation_cannot_replace_state()
    {
        var d = await Driver.CreateAsync();
        var state = d.Coordinator.GetState(d.Scope.InstallationId)!;
        Assert.False(await d.Coordinator.PrepareEpisodeAsync(new DiscoveryInventoryContext(d.Inventory, false),
            Guid.NewGuid(), CancellationToken.None));
        Assert.Same(state, d.Coordinator.GetState(d.Scope.InstallationId));
    }

    [Fact]
    public async Task Conflicting_initialization_never_exposes_old_generation()
    {
        var d = await Driver.CreateAsync();
        await d.CompleteAsync(1, 100);
        var changed = new ExecutableInventory(d.Scope, InventoryCompleteness.Complete,
            [new ExecutableCandidate(d.Inventory.Candidates[0].ExecutablePath, "Game.exe",
                new FileRevision(11, T0))], []);
        d.LearningStore.RejectSaves = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => d.Coordinator.InitializeAsync(
            new DiscoveryInventoryContext(changed, false), CancellationToken.None));
        Assert.Null(d.Coordinator.GetState(d.Scope.InstallationId));
    }

    [Fact]
    public async Task Cancellation_discards_current_but_preserves_completed_reference()
    {
        var d = await Driver.CreateAsync();
        await d.CompleteAsync(1, 100);
        var reference = d.Coordinator.GetState(d.Scope.InstallationId)!.Reference;
        Assert.True(await d.PrepareAsync());
        await d.CaptureAsync(7, [d.Process(200)]);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => d.Coordinator.ObserveAsync(d.Scope.InstallationId,
            new ProcessObservationBatch(8, T0.AddSeconds(16), EpisodeQuality.Complete, []), cancellation.Token));
        Assert.Same(reference, d.Coordinator.GetState(d.Scope.InstallationId)!.Reference);
        Assert.Null(d.Coordinator.GetState(d.Scope.InstallationId)!.Confirmation);
        await d.CaptureAsync(9, [d.Process(200)]);
        await d.CaptureAsync(10, []);
        await d.CaptureAsync(11, []);
        Assert.Same(reference, d.Coordinator.GetState(d.Scope.InstallationId)!.Reference);
        Assert.Null(d.Coordinator.GetState(d.Scope.InstallationId)!.Confirmation);
    }

    [Fact]
    public async Task Unrelated_unknown_name_and_path_during_episode_does_not_break_qualification()
    {
        var d = await Driver.CreateAsync();
        var unrelated =
            new ProcessSnapshot(
                300,
                "Unknown.exe",
                null,
                null);

        await d.CaptureAsync(1, []);
        await d.CaptureAsync(2, []);
        await d.CaptureAsync(3, [d.Process(100)]);

        var result =
            await d.CaptureAsync(
                4,
                [d.Process(100), unrelated]);

        Assert.DoesNotContain(
            DiscoveryReason.UnknownProcessIdentity,
            result?.Reasons
            ?? Array.Empty<DiscoveryReason>());

        await d.CaptureAsync(5, [unrelated]);

        var completion =
            await d.CaptureAsync(
                6,
                [unrelated]);

        Assert.NotNull(
            d.Coordinator.GetState(
                d.Scope.InstallationId)!.Reference);

        Assert.Equal(
            [DiscoveryReason.AwaitingIndependentEpisode],
            completion!.Reasons);
    }

    [Fact]
    public async Task Known_candidate_name_with_changed_path_breaks_qualification()
    {
        var d = await Driver.CreateAsync();
        await d.CaptureAsync(1, []);
        await d.CaptureAsync(2, []);
        await d.CaptureAsync(3, [d.Process(100)]);
        var result = await d.CaptureAsync(4, [new ProcessSnapshot(100, "Game.exe",
            @"C:\Outside\Game.exe", T0.AddSeconds(5))]);
        Assert.Equal([DiscoveryReason.UnreliablePath], result!.Reasons);
    }

    [Fact]
    public async Task Same_content_new_generation_invalidates_proof()
    {
        var d = await Driver.CreateAsync();
        await d.CompleteAsync(1, 100);
        var newScope = new InstallationScope(d.Scope.GameId, d.Scope.InstallationId,
            d.Scope.RootPath, Guid.NewGuid(), true);
        var inventory = new ExecutableInventory(newScope, InventoryCompleteness.Complete,
            d.Inventory.Candidates, []);
        Assert.True(await d.Coordinator.PrepareEpisodeAsync(new DiscoveryInventoryContext(inventory, false),
            d.Coordinator.GetState(d.Scope.InstallationId)!.ConcurrencyToken, CancellationToken.None));
        Assert.Equal([DiscoveryReason.GenerationChanged],
            d.Coordinator.GetState(d.Scope.InstallationId)!.Reasons);
        Assert.Null(d.Coordinator.GetState(d.Scope.InstallationId)!.Reference);
    }

    [Fact]
    public async Task CAS_conflict_never_exposes_stale_reference()
    {
        var d = await Driver.CreateAsync();
        await d.CompleteAsync(1, 100);
        Assert.True(await d.PrepareAsync());
        await d.CaptureAsync(7, [d.Process(200)]);
        await d.CaptureAsync(8, [d.Process(200)]);
        var old = d.Coordinator.GetState(d.Scope.InstallationId)!;
        var externallyCleared = new ProcessSignatureLearningState(old.Inventory, old.PolicyVersion,
            Guid.NewGuid(), old.LastSequenceNumber, old.HasAmbiguousInstallation,
            null, null, [DiscoveryReason.CaptureGap]);
        Assert.True(await d.LearningStore.TrySaveAsync(externallyCleared, old.ConcurrencyToken,
            CancellationToken.None));
        await d.CaptureAsync(9, []);
        await Assert.ThrowsAsync<InvalidOperationException>(() => d.CaptureAsync(10, []));
        Assert.Null(d.Coordinator.GetState(d.Scope.InstallationId));
    }

    [Fact]
    public async Task Persistence_failure_after_invalidation_stops_stale_proof()
    {
        var d = await Driver.CreateAsync();
        await d.CompleteAsync(1, 100);
        d.LearningStore.FailSaves = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => d.CaptureAsync(7,
            [new ProcessSnapshot(200, "Game.exe", null, T0.AddSeconds(13))]));
        Assert.Null(d.Coordinator.GetState(d.Scope.InstallationId));
    }

    [Fact]
    public void Process_observation_batch_copies_processes_and_normalizes_time()
    {
        var input = new List<ProcessSnapshot> { new(100, "Game.exe", @"C:\Games\Example\Game.exe", T0) };
        var batch = new ProcessObservationBatch(1, T0.ToOffset(TimeSpan.FromHours(2)),
            EpisodeQuality.Complete, input);
        input.Clear();
        Assert.Single(batch.Processes);
        Assert.Equal(TimeSpan.Zero, batch.ObservedAtUtc.Offset);
    }

    [Fact]
    public async Task Repeated_identical_refusal_does_not_write()
    {
        var d = await Driver.CreateAsync("Game.exe", "Companion.exe");
        async Task RefuseAsync(long start, int pid, bool leading)
        {
            if (leading)
            {
                await d.CaptureAsync(start, []);
                await d.CaptureAsync(start + 1, []);
            }
            for (var sequence = start + 2; sequence <= start + 5; sequence++)
                await d.CaptureAsync(sequence, [d.Process("Game.exe", pid),
                    d.Process("Companion.exe", pid + 1)]);
            await d.CaptureAsync(start + 6, []);
            await d.CaptureAsync(start + 7, []);
        }
        await RefuseAsync(1, 100, true);
        var writes = d.LearningStore.WriteCount;
        Assert.True(await d.PrepareAsync());
        await RefuseAsync(7, 200, false);
        Assert.Equal(writes, d.LearningStore.WriteCount);
    }

    [Fact]
    public async Task Root_change_has_scope_reason_and_fresh_generation()
    {
        var d = await Driver.CreateAsync();
        await d.CompleteAsync(1, 100);
        var relocated = new InstallationScope(d.Scope.GameId, d.Scope.InstallationId,
            @"C:\Games\Moved", d.Scope.GenerationId, true);
        var inventory = new ExecutableInventory(relocated, InventoryCompleteness.Complete,
            [new ExecutableCandidate(@"C:\Games\Moved\Game.exe", "Game.exe", new FileRevision(10, T0))], []);
        Assert.True(await d.Coordinator.PrepareEpisodeAsync(new DiscoveryInventoryContext(inventory, false),
            d.Coordinator.GetState(d.Scope.InstallationId)!.ConcurrencyToken, CancellationToken.None));
        var state = d.Coordinator.GetState(d.Scope.InstallationId)!;
        Assert.NotEqual(d.Scope.GenerationId, state.Inventory.Scope.GenerationId);
        Assert.Equal([DiscoveryReason.ScopeChanged], state.Reasons);
        Assert.Null(state.Reference);
    }

    [Fact]
    public async Task One_coordinator_keeps_installation_episodes_independent()
    {
        var d = await Driver.CreateAsync();
        var otherScope = new InstallationScope(GameId.New(), InstallationId.New(), d.Scope.RootPath,
            Guid.NewGuid(), true);
        var otherInventory = new ExecutableInventory(otherScope, InventoryCompleteness.Complete,
            d.Inventory.Candidates, []);
        await d.Coordinator.InitializeAsync(new DiscoveryInventoryContext(otherInventory, false),
            CancellationToken.None);
        for (long sequence = 1; sequence <= 6; sequence++)
        {
            var processes = sequence is 3 or 4 ? new[] { d.Process(100) } : [];
            var batch = new ProcessObservationBatch(sequence, T0.AddSeconds(sequence * 2),
                EpisodeQuality.Complete, processes);
            await d.Coordinator.ObserveAsync(d.Scope.InstallationId, batch, CancellationToken.None);
            if (sequence <= 4)
                await d.Coordinator.ObserveAsync(otherScope.InstallationId, batch, CancellationToken.None);
        }
        Assert.NotNull(d.Coordinator.GetState(d.Scope.InstallationId)!.Reference);
        Assert.Null(d.Coordinator.GetState(otherScope.InstallationId)!.Reference);
        var otherFive = new ProcessObservationBatch(5, T0.AddSeconds(10), EpisodeQuality.Complete, []);
        var otherSix = new ProcessObservationBatch(6, T0.AddSeconds(12), EpisodeQuality.Complete, []);
        await d.Coordinator.ObserveAsync(otherScope.InstallationId, otherFive, CancellationToken.None);
        await d.Coordinator.ObserveAsync(otherScope.InstallationId, otherSix, CancellationToken.None);
        Assert.NotNull(d.Coordinator.GetState(otherScope.InstallationId)!.Reference);
    }

    [Fact]
    public async Task contradictory_second_episode_clears_reference()
    {
        var d = await Driver.CreateAsync("Game.exe", "Companion.exe");
        await QualifiedWithCompanionAsync(d, 1, "Game.exe", "Companion.exe", 100);
        var old = d.Coordinator.GetState(d.Scope.InstallationId)!;
        Assert.NotNull(old.Reference);
        Assert.True(await d.PrepareAsync());
        await QualifiedWithCompanionAsync(d, 7, "Companion.exe", "Game.exe", 200, leadingAbsences: false);
        var current = await d.LearningStore.LoadAsync(d.Scope.InstallationId, CancellationToken.None);
        Assert.Null(current!.Reference);
        Assert.Null(current.Confirmation);
        Assert.NotEqual(old.ConcurrencyToken, current.ConcurrencyToken);
        Assert.Equal([DiscoveryReason.ConflictingEpisodes], current.Reasons);
    }

    [Fact]
    public async Task Contradiction_requires_two_new_successes_after_old_pair_is_cleared()
    {
        var d = await Driver.CreateAsync("Game.exe", "Companion.exe");
        await QualifiedWithCompanionAsync(d, 1, "Game.exe", "Companion.exe", 100);
        var oldEpisodeId = d.Coordinator.GetState(d.Scope.InstallationId)!.Reference!.EpisodeId;
        Assert.True(await d.PrepareAsync());
        await QualifiedWithCompanionAsync(d, 7, "Companion.exe", "Game.exe", 200,
            leadingAbsences: false);
        Assert.Null(d.Coordinator.GetState(d.Scope.InstallationId)!.Reference);
        Assert.True(await d.PrepareAsync());
        await QualifiedWithCompanionAsync(d, 13, "Game.exe", "Companion.exe", 300,
            leadingAbsences: false);
        var newReference = d.Coordinator.GetState(d.Scope.InstallationId)!.Reference!;
        Assert.NotEqual(oldEpisodeId, newReference.EpisodeId);
        Assert.Null(d.Coordinator.GetState(d.Scope.InstallationId)!.Confirmation);
        Assert.True(await d.PrepareAsync());
        await QualifiedWithCompanionAsync(d, 19, "Game.exe", "Companion.exe", 400,
            leadingAbsences: false);
        var state = d.Coordinator.GetState(d.Scope.InstallationId)!;
        Assert.Same(newReference, state.Reference);
        Assert.NotNull(state.Confirmation);
        Assert.Equal(newReference.SequenceNumber + 1, state.Confirmation.SequenceNumber);
    }

    [Fact]
    public async Task Equivalent_candidates_remain_ambiguous()
    {
        var d = await Driver.CreateAsync("Game.exe", "Companion.exe");
        await d.CaptureAsync(1, []);
        await d.CaptureAsync(2, []);
        for (var sequence = 3; sequence <= 6; sequence++)
            await d.CaptureAsync(sequence, [d.Process("Game.exe", 100), d.Process("Companion.exe", 101)]);
        await d.CaptureAsync(7, []);
        var result = await d.CaptureAsync(8, []);
        Assert.Equal(DiscoveryDecisionKind.Ambiguous, result!.Kind);
        Assert.Equal([DiscoveryReason.EquivalentCandidates], result.Reasons);
        Assert.Null(d.Coordinator.GetState(d.Scope.InstallationId)!.Reference);
    }

    [Fact]
    public async Task Unobserved_competitor_blocks_promotion()
    {
        var d = await Driver.CreateAsync("Game.exe", "Companion.exe");
        await d.CompleteAsync(1, 100);
        Assert.Equal([DiscoveryReason.UnobservedCompetitor],
            d.Coordinator.GetState(d.Scope.InstallationId)!.Reasons);
        Assert.Null(d.Coordinator.GetState(d.Scope.InstallationId)!.Reference);
    }

    [Fact]
    public async Task Startup_companion_with_two_main_only_captures_qualifies()
    {
        var d = await Driver.CreateAsync("Game.exe", "Companion.exe");
        await QualifiedWithCompanionAsync(d, 1, "Game.exe", "Companion.exe", 100);
        Assert.Equal([DiscoveryReason.AwaitingIndependentEpisode],
            d.Coordinator.GetState(d.Scope.InstallationId)!.Reasons);
        Assert.True(await d.PrepareAsync());
        await QualifiedWithCompanionAsync(d, 7, "Game.exe", "Companion.exe", 200,
            leadingAbsences: false);
        Assert.NotNull(d.Coordinator.GetState(d.Scope.InstallationId)!.Confirmation);
    }

    [Fact]
    public async Task Companion_still_alive_blocks()
    {
        var d = await Driver.CreateAsync("Game.exe", "Companion.exe");
        await d.CaptureAsync(1, []);
        await d.CaptureAsync(2, []);
        await d.CaptureAsync(3, [d.Process("Companion.exe", 101)]);
        for (var sequence = 4; sequence <= 6; sequence++)
            await d.CaptureAsync(sequence, [d.Process("Game.exe", 100), d.Process("Companion.exe", 101)]);
        await d.CaptureAsync(7, []);
        var result = await d.CaptureAsync(8, []);
        Assert.Equal(DiscoveryDecisionKind.Ambiguous, result!.Kind);
        Assert.Equal([DiscoveryReason.EquivalentCandidates], result.Reasons);
    }

    [Fact]
    public async Task Companion_reappearing_blocks()
    {
        var d = await Driver.CreateAsync("Game.exe", "Companion.exe");
        await d.CaptureAsync(1, []);
        await d.CaptureAsync(2, []);
        await d.CaptureAsync(3, [d.Process("Companion.exe", 101)]);
        await d.CaptureAsync(4, [d.Process("Game.exe", 100), d.Process("Companion.exe", 101)]);
        await d.CaptureAsync(5, [d.Process("Game.exe", 100)]);
        await d.CaptureAsync(6, [d.Process("Game.exe", 100), d.Process("Companion.exe", 101)]);
        await d.CaptureAsync(7, [d.Process("Game.exe", 100)]);
        await d.CaptureAsync(8, []);
        var result = await d.CaptureAsync(9, []);
        Assert.Equal([DiscoveryReason.ReappearingCompetitor], result!.Reasons);
    }

    [Fact]
    public async Task Late_competitor_blocks()
    {
        var d = await Driver.CreateAsync("Game.exe", "Companion.exe");
        await d.CaptureAsync(1, []);
        await d.CaptureAsync(2, []);
        await d.CaptureAsync(3, [d.Process("Game.exe", 100)]);
        await d.CaptureAsync(4, [d.Process("Game.exe", 100), d.Process("Companion.exe", 101)]);
        await d.CaptureAsync(5, [d.Process("Game.exe", 100), d.Process("Companion.exe", 101)]);
        await d.CaptureAsync(6, [d.Process("Game.exe", 100)]);
        await d.CaptureAsync(7, [d.Process("Game.exe", 100)]);
        await d.CaptureAsync(8, []);
        var result = await d.CaptureAsync(9, []);
        Assert.Equal([DiscoveryReason.LateCompetitor], result!.Reasons);
    }

    [Fact]
    public async Task capture_quality_failure_clears_reference_once()
    {
        var d = await Driver.CreateAsync();
        await d.CompleteAsync(1, 100);
        var result = await d.CaptureAsync(7, [], EpisodeQuality.CaptureGap);
        Assert.Equal([DiscoveryReason.CaptureGap], result!.Reasons);
        var writes = d.LearningStore.WriteCount;
        await d.CaptureAsync(8, [], EpisodeQuality.CaptureGap);
        Assert.Equal(writes, d.LearningStore.WriteCount);
    }

    [Fact]
    public async Task Changed_policy_discards_reference()
    {
        var d = await Driver.CreateAsync();
        await d.CompleteAsync(1, 100);
        Assert.True(await d.PrepareAsync());
        await d.CaptureAsync(7, [d.Process(200)]);
        await d.CaptureAsync(8, [d.Process(200)]);
        await d.CaptureAsync(9, []);
        await d.CaptureAsync(10, []);
        var old = d.Coordinator.GetState(d.Scope.InstallationId)!;
        Assert.NotNull(old.Reference);
        Assert.NotNull(old.Confirmation);
        var stale = new ProcessSignatureLearningState(old.Inventory, 2,
            Guid.NewGuid(), old.LastSequenceNumber, old.HasAmbiguousInstallation,
            old.Reference, old.Confirmation, old.Reasons);
        Assert.True(await d.LearningStore.TrySaveAsync(stale, old.ConcurrencyToken, CancellationToken.None));
        var restarted = new ProcessSignatureLearningCoordinator(d.LearningStore, d.SignatureStore,
            d.RevisionSource, new ProcessSignatureDiscoveryPolicy());
        var state = await restarted.InitializeAsync(new DiscoveryInventoryContext(d.Inventory, false), CancellationToken.None);
        Assert.Equal(ProcessSignatureDiscoveryPolicy.CurrentPolicyVersion, state.PolicyVersion);
        Assert.Null(state.Reference);
        Assert.Null(state.Confirmation);
        Assert.Equal([DiscoveryReason.PolicyVersionChanged], state.Reasons);
    }

    [Fact]
    public async Task Absent_or_ambiguous_scope_refuses_learning()
    {
        var d = await Driver.CreateAsync();
        await d.CompleteAsync(1, 100);
        var state = d.Coordinator.GetState(d.Scope.InstallationId)!;
        Assert.True(await d.Coordinator.PrepareEpisodeAsync(new DiscoveryInventoryContext(d.Inventory, true),
            state.ConcurrencyToken, CancellationToken.None));
        state = d.Coordinator.GetState(d.Scope.InstallationId)!;
        Assert.True(state.HasAmbiguousInstallation);
        Assert.Null(state.Reference);
        Assert.Equal([DiscoveryReason.AmbiguousInstallation], state.Reasons);
        var absentScope = new InstallationScope(d.Scope.GameId, d.Scope.InstallationId,
            d.Scope.RootPath, Guid.NewGuid(), false);
        var absent = new ExecutableInventory(absentScope, InventoryCompleteness.Complete, d.Inventory.Candidates, []);
        Assert.True(await d.Coordinator.PrepareEpisodeAsync(new DiscoveryInventoryContext(absent, false),
            state.ConcurrencyToken, CancellationToken.None));
        Assert.False(d.Coordinator.GetState(d.Scope.InstallationId)!.Inventory.Scope.IsPresent);
    }

    [Fact]
    public async Task Existing_explicit_authority_refuses_learning()
    {
        var d = await Driver.CreateAsync();
        d.SignatureStore.Existing = new ProcessSignature(d.Scope.GameId.Value, [],
            ProcessSignatureOrigin.BuiltIn, T0);
        await d.CompleteAsync(1, 100);
        Assert.Equal([DiscoveryReason.ProtectedSignature],
            d.Coordinator.GetState(d.Scope.InstallationId)!.Reasons);
        Assert.Null(d.Coordinator.GetState(d.Scope.InstallationId)!.Reference);
    }

    [Fact]
    public async Task restart_mid_episode_preserves_reference_and_requires_new_absence()
    {
        var d = await Driver.CreateAsync();
        await d.CompleteAsync(1, 100);
        var reference = d.Coordinator.GetState(d.Scope.InstallationId)!.Reference!;
        Assert.True(await d.PrepareAsync());
        await d.CaptureAsync(7, [d.Process(200)]);
        var restarted = new ProcessSignatureLearningCoordinator(d.LearningStore, d.SignatureStore,
            d.RevisionSource, new ProcessSignatureDiscoveryPolicy());
        var state = await restarted.InitializeAsync(new DiscoveryInventoryContext(d.Inventory, false), CancellationToken.None);
        Assert.Same(reference, state.Reference);
        Assert.Equal(1, state.LastSequenceNumber);
        async Task<DiscoveryDecision?> Tick(long sequence, IReadOnlyList<ProcessSnapshot> processes) =>
            await restarted.ObserveAsync(d.Scope.InstallationId,
                new ProcessObservationBatch(sequence, T0.AddSeconds(100 + sequence * 2),
                    EpisodeQuality.Complete, processes), CancellationToken.None);
        await Tick(1, [d.Process(300)]);
        await Tick(2, []);
        Assert.Null(restarted.GetState(d.Scope.InstallationId)!.Confirmation);
        await Tick(3, []);
        await Tick(4, [d.Process(400)]);
        await Tick(5, [d.Process(400)]);
        await Tick(6, []);
        var decision = await Tick(7, []);
        Assert.Equal(DiscoveryDecisionKind.PromoteMain, decision!.Kind);
        Assert.Same(reference, restarted.GetState(d.Scope.InstallationId)!.Reference);
    }

    private static async Task QualifiedWithCompanionAsync(Driver d, long start,
        string main, string companion, int pid, bool leadingAbsences = true)
    {
        if (leadingAbsences)
        {
            await d.CaptureAsync(start, []);
            await d.CaptureAsync(start + 1, []);
        }
        await d.CaptureAsync(start + 2, [d.Process(companion, pid + 1)]);
        await d.CaptureAsync(start + 3, [d.Process(main, pid), d.Process(companion, pid + 1)]);
        await d.CaptureAsync(start + 4, [d.Process(main, pid)]);
        await d.CaptureAsync(start + 5, [d.Process(main, pid)]);
        await d.CaptureAsync(start + 6, []);
        await d.CaptureAsync(start + 7, []);
    }

    private sealed class Driver
    {
        private Driver(InstallationScope scope, ExecutableInventory inventory,
            FakeLearningStore learningStore, FakeSignatureStore signatureStore,
            FakeRevisionSource revisionSource,
            ProcessSignatureLearningCoordinator coordinator)
        {
            Scope = scope;
            Inventory = inventory;
            LearningStore = learningStore;
            SignatureStore = signatureStore;
            RevisionSource = revisionSource;
            Coordinator = coordinator;
        }

        public InstallationScope Scope { get; }
        public ExecutableInventory Inventory { get; }
        public FakeLearningStore LearningStore { get; }
        public FakeSignatureStore SignatureStore { get; }
        public FakeRevisionSource RevisionSource { get; }
        public ProcessSignatureLearningCoordinator Coordinator { get; }

        public static async Task<Driver> CreateAsync(params string[] names)
        {
            if (names.Length == 0) names = ["Game.exe"];
            var scope = new InstallationScope(GameId.New(), InstallationId.New(), @"C:\Games\Example", Guid.NewGuid(), true);
            var inventory = new ExecutableInventory(scope, InventoryCompleteness.Complete,
                names.Select(name => new ExecutableCandidate(@"C:\Games\Example\" + name,
                    name, new FileRevision(10, T0))).ToArray(), []);
            var learningStore = new FakeLearningStore();
            var signatureStore = new FakeSignatureStore();
            var revisionSource = new FakeRevisionSource(inventory);
            var coordinator = new ProcessSignatureLearningCoordinator(learningStore, signatureStore,
                revisionSource, new ProcessSignatureDiscoveryPolicy());
            await coordinator.InitializeAsync(new DiscoveryInventoryContext(inventory, false), CancellationToken.None);
            return new Driver(scope, inventory, learningStore, signatureStore, revisionSource, coordinator);
        }

        public static async Task<Driver> CreateUnrealAsync(string project,
            params string[] extraRelativePaths)
        {
            var scope = new InstallationScope(GameId.New(), InstallationId.New(),
                @"C:\Games\Example", Guid.NewGuid(), true);
            var paths = new[]
            {
                project + ".exe",
                $@"{project}\Binaries\Win64\{project}-Win64-Shipping.exe"
            }.Concat(extraRelativePaths);
            var inventory = new ExecutableInventory(scope, InventoryCompleteness.Complete,
                paths.Select(path => new ExecutableCandidate(scope.RootPath + "\\" + path,
                    Path.GetFileName(path), new FileRevision(10, T0))).ToArray(), []);
            var learningStore = new FakeLearningStore();
            var signatureStore = new FakeSignatureStore();
            var revisionSource = new FakeRevisionSource(inventory);
            var coordinator = new ProcessSignatureLearningCoordinator(learningStore,
                signatureStore, revisionSource, new ProcessSignatureDiscoveryPolicy());
            await coordinator.InitializeAsync(new DiscoveryInventoryContext(inventory, false),
                CancellationToken.None);
            return new Driver(scope, inventory, learningStore, signatureStore, revisionSource,
                coordinator);
        }

        public static async Task<Driver> CreateWithCandidatesAsync(
            params (string Name, string Path)[] candidates)
        {
            var scope = new InstallationScope(GameId.New(), InstallationId.New(),
                @"C:\Games\Example", Guid.NewGuid(), true);
            var inventory = new ExecutableInventory(scope, InventoryCompleteness.Complete,
                candidates.Select(candidate => new ExecutableCandidate(candidate.Path,
                    candidate.Name, new FileRevision(10, T0))).ToArray(), []);
            var learningStore = new FakeLearningStore();
            var signatureStore = new FakeSignatureStore();
            var revisionSource = new FakeRevisionSource(inventory);
            var coordinator = new ProcessSignatureLearningCoordinator(learningStore,
                signatureStore, revisionSource, new ProcessSignatureDiscoveryPolicy());
            await coordinator.InitializeAsync(new DiscoveryInventoryContext(inventory, false),
                CancellationToken.None);
            return new Driver(scope, inventory, learningStore, signatureStore, revisionSource,
                coordinator);
        }

        public ProcessSnapshot Process(int pid) => Process("Game.exe", pid);
        public ProcessSnapshot Process(string name, int pid) => new(pid, name,
            Inventory.Candidates.Single(item => item.ExecutableName == name).ExecutablePath,
            T0.AddSeconds(pid / 100 switch { 1 => 5, 2 => 13, 3 => 29, 4 => 41, _ => 1 }));

        public Task<DiscoveryDecision?> CaptureAsync(long sequence, IReadOnlyList<ProcessSnapshot> processes,
            EpisodeQuality quality = EpisodeQuality.Complete) => Coordinator.ObserveAsync(Scope.InstallationId,
            new ProcessObservationBatch(sequence, T0.AddSeconds(sequence * 2), quality, processes), CancellationToken.None);

        public async Task CompleteAsync(long firstSequence, int pid)
        {
            await CaptureAsync(firstSequence, []);
            await CaptureAsync(firstSequence + 1, []);
            await CaptureAsync(firstSequence + 2, [Process(pid)]);
            await CaptureAsync(firstSequence + 3, [Process(pid)]);
            await CaptureAsync(firstSequence + 4, []);
            await CaptureAsync(firstSequence + 5, []);
        }

        public Task<bool> PrepareAsync() => Coordinator.PrepareEpisodeAsync(
            new DiscoveryInventoryContext(Inventory, false),
            Coordinator.GetState(Scope.InstallationId)!.ConcurrencyToken, CancellationToken.None);
    }

    private sealed class FakeLearningStore : IProcessSignatureLearningStore
    {
        private readonly Dictionary<InstallationId, ProcessSignatureLearningState> _states = new();
        public int WriteCount { get; private set; }
        public bool FailSaves { get; set; }
        public bool RejectSaves { get; set; }
        public CancellationTokenSource? CancelSaveWith { get; set; }

        public Task<ProcessSignatureLearningState?> LoadAsync(InstallationId installationId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_states.GetValueOrDefault(installationId));
        }

        public Task<bool> TrySaveAsync(ProcessSignatureLearningState state, Guid? expectedConcurrencyToken,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (CancelSaveWith is { } source)
            {
                CancelSaveWith = null;
                source.Cancel();
                throw new OperationCanceledException(source.Token);
            }
            if (FailSaves) throw new InvalidOperationException("Simulated write failure.");
            if (RejectSaves) return Task.FromResult(false);
            var id = state.Inventory.Scope.InstallationId;
            var current = _states.GetValueOrDefault(id);
            if (current?.ConcurrencyToken != expectedConcurrencyToken)
                return Task.FromResult(false);
            _states[id] = state;
            WriteCount++;
            return Task.FromResult(true);
        }
    }

    private sealed class FakeSignatureStore : IProcessSignatureStore
    {
        public ProcessSignature? Existing { get; set; }
        public Task UpsertAsync(ProcessSignature signature, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Coordinator must never persist a signature.");
        public Task<ProcessSignature?> GetAsync(Guid gameId, CancellationToken cancellationToken) =>
            Task.FromResult(Existing);
        public Task<IReadOnlyList<ProcessSignature>> GetAllAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ProcessSignature>>([]);
    }

    private sealed class FakeRevisionSource(ExecutableInventory inventory) : IExecutableRevisionSource
    {
        public Dictionary<string, ExecutableRevisionResult> Results { get; } =
            new(StringComparer.OrdinalIgnoreCase);
        private TaskCompletionSource<bool>? _heldReadEntered;
        private TaskCompletionSource<bool>? _releaseHeldRead;
        private TaskCompletionSource<bool>? _heldReadReleaseHandle;

        public void HoldNextRead()
        {
            _heldReadEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _releaseHeldRead = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _heldReadReleaseHandle = _releaseHeldRead;
        }

        public Task WaitForHeldReadAsync() => _heldReadEntered!.Task.WaitAsync(TimeSpan.FromSeconds(5));
        public void ReleaseHeldRead() => _heldReadReleaseHandle!.TrySetResult(true);

        public async Task<ExecutableRevisionResult> ReadAsync(InstallationScope scope, string executablePath,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_releaseHeldRead is { } release)
            {
                _releaseHeldRead = null;
                _heldReadEntered!.TrySetResult(true);
                await release.Task.WaitAsync(cancellationToken);
            }
            if (Results.TryGetValue(executablePath, out var result)) return result;
            var candidate = inventory.Candidates.Single(item => string.Equals(item.ExecutablePath,
                executablePath, StringComparison.OrdinalIgnoreCase));
            return new ExecutableRevisionResult(candidate.Revision, null);
        }
    }
}
