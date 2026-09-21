using PlayStead.Core.Sessions;
using PlayStead.Core.Library;
using PlayStead.Core.Sessions.Discovery;
using PlayStead.Data.Sessions;
using PlayStead.Data.Tests.Database;
using Microsoft.Data.Sqlite;
using System.Text.Json;

namespace PlayStead.Data.Tests.Sessions;

public sealed class SqliteProcessSignatureDiscoveryStoreTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static readonly DateTimeOffset T0 = new(2026, 9, 15, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Builtin_upsert_cannot_replace_manual()
    {
        using var fixture = await SeedAsync();
        var write = await ProofAsync(fixture);
        var store = Store(fixture);
        await store.UpsertAsync(Explicit(write, ProcessSignatureOrigin.Manual), Ct);
        var before = await SnapshotAsync(fixture);
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.UpsertAsync(Explicit(write, ProcessSignatureOrigin.BuiltIn), Ct));
        Assert.Equal(before, await SnapshotAsync(fixture));
    }

    [Theory]
    [InlineData(ProcessSignatureOrigin.Manual)]
    [InlineData(ProcessSignatureOrigin.BuiltIn)]
    [InlineData(ProcessSignatureOrigin.Discovered)]
    public async Task Legacy_discovered_upsert_cannot_replace_authority(ProcessSignatureOrigin origin)
    {
        using var fixture = await SeedAsync();
        var write = await ProofAsync(fixture);
        await Store(fixture).UpsertAsync(Explicit(write, origin), Ct);
        var before = await SnapshotAsync(fixture);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Store(fixture).UpsertAsync(Explicit(write, ProcessSignatureOrigin.Discovered), Ct));
        Assert.Equal(before, await SnapshotAsync(fixture));
    }

    [Fact]
    public async Task Validated_discovered_cannot_use_upsert()
    {
        using var fixture = await SeedAsync();
        var write = await ProofAsync(fixture);
        await Assert.ThrowsAsync<ArgumentException>(() => Store(fixture).UpsertAsync(write.Signature, Ct));
        Assert.Null(await Store(fixture).GetAsync(write.Signature.GameId, Ct));
    }

    [Fact]
    public async Task Reads_round_trip_path_revision_metadata_and_order()
    {
        using var fixture = await SeedAsync();
        var write = await ProofAsync(fixture);
        var signature = Explicit(write, ProcessSignatureOrigin.Manual) with { Entries = [
            new("Z.exe", ProcessSignatureEntryKind.Excluded, @"C:\Games\Z.exe", new FileRevision(42, T0)),
            new("A.exe", ProcessSignatureEntryKind.Main, @"C:\Games\A.exe", new FileRevision(123, T0.AddSeconds(1))) ] };
        await Store(fixture).UpsertAsync(signature, Ct);
        AssertSignature(signature, await Store(fixture).GetAsync(signature.GameId, Ct));
        AssertSignature(signature, Assert.Single(await Store(fixture).GetAllAsync(Ct)));
        await SqlAsync(fixture, "DELETE FROM process_signatures");
        await Store(fixture).UpsertAsync(Explicit(write, ProcessSignatureOrigin.Discovered), Ct);
        var legacy = await Store(fixture).GetAsync(signature.GameId, Ct);
        Assert.NotNull(legacy!.Discovery);
        Assert.Equal(ProcessSignatureValidationState.NeedsRevalidation, legacy.Discovery.ValidationState);
        Assert.Null(legacy.Discovery.InstallationId);
        Assert.Null(legacy.Discovery.GenerationId);
        Assert.Null(legacy.Discovery.PolicyVersion);
        Assert.NotEqual(Guid.Empty, legacy.Discovery.ConcurrencyToken);
    }

    [Fact]
    public async Task Explicit_replacement_clears_learning_proof()
    {
        using var fixture = await SeedAsync();
        var write = await ProofAsync(fixture);
        await Store(fixture).UpsertAsync(Explicit(write, ProcessSignatureOrigin.Manual), Ct);
        await AssertConsumedAsync(fixture, write);
    }

    [Fact]
    public async Task Half_revision_is_rejected_on_read()
    {
        using var fixture = await SeedAsync();
        var write = await ProofAsync(fixture);
        await Store(fixture).UpsertAsync(Explicit(write, ProcessSignatureOrigin.Manual), Ct);
        await SqlAsync(fixture, "UPDATE process_signature_entries SET validated_size_bytes=123");
        await Assert.ThrowsAsync<InvalidDataException>(() => Store(fixture).GetAsync(write.Signature.GameId, Ct));
        await Assert.ThrowsAsync<InvalidDataException>(() => Store(fixture).GetAllAsync(Ct));
    }

    private static SqliteProcessSignatureStore Store(DiscoveryDatabaseFixture fixture) => new(fixture.Options);
    [Fact]
    public async Task Revalidation_nonnull_scope_replaces_suspended_valid_signature()
    {
        using var fixture = await SeedAsync();
        var first = await ProofAsync(fixture);
        Assert.True(await Store(fixture).TryInsertDiscoveredIfAbsentAsync(first, Ct));
        Assert.True(await Store(fixture).TryInvalidateDiscoveredAsync(first.Signature.GameId, Expect(first.Signature), Ct));
        var write = await ProofAsync(fixture, first.Signature);
        var suspended = (await Store(fixture).GetAsync(first.Signature.GameId, Ct))!;
        Assert.NotNull(suspended.Discovery!.GenerationId);
        Assert.NotNull(suspended.Discovery.InstallationId);
        Assert.True(await Store(fixture).TryRevalidateDiscoveredAsync(write, Expect(suspended), Ct));
        AssertSignature(write.Signature, await Store(fixture).GetAsync(first.Signature.GameId, Ct));
        await AssertConsumedAsync(fixture, write);
    }

    [Fact]
    public async Task Structural_recovery_restores_only_the_expected_suspended_signature_once()
    {
        using var fixture = await SeedAsync();
        var store = Store(fixture);
        var write = await ProofAsync(fixture);
        Assert.True(await store.TryInsertDiscoveredIfAbsentAsync(write, Ct));
        var accepted = (await store.GetAsync(write.Signature.GameId, Ct))!;
        Assert.True(await store.TryInvalidateDiscoveredAsync(accepted.GameId, Expect(accepted), Ct));
        var suspended = (await store.GetAsync(accepted.GameId, Ct))!;
        Assert.Equal(ProcessSignatureValidationState.NeedsRevalidation,
            suspended.Discovery!.ValidationState);

        Assert.True(await store.TryRestoreDiscoveredValidationAsync(
            suspended.GameId, Expect(suspended), Ct));
        var restored = (await store.GetAsync(accepted.GameId, Ct))!;
        Assert.Equal(ProcessSignatureValidationState.Valid, restored.Discovery!.ValidationState);
        Assert.Equal(suspended.Entries, restored.Entries);
        Assert.Equal(suspended.UpdatedAtUtc, restored.UpdatedAtUtc);

        Assert.False(await store.TryRestoreDiscoveredValidationAsync(
            suspended.GameId, Expect(suspended), Ct));
        var unchanged = (await store.GetAsync(accepted.GameId, Ct))!;
        Assert.Equal(restored.Discovery, unchanged.Discovery);
        Assert.Equal(restored.Entries, unchanged.Entries);
        Assert.Equal(restored.UpdatedAtUtc, unchanged.UpdatedAtUtc);
    }
    [Fact]
    public async Task Zero_row_proof_consumption_rolls_back_parent_and_entries()
    {
        using var fixture = await SeedAsync();
        var write = await ProofAsync(fixture);
        await SqlAsync(fixture, "CREATE TRIGGER ignore_consume BEFORE UPDATE ON process_signature_learning BEGIN SELECT RAISE(IGNORE); END");
        var before = await SnapshotAsync(fixture);
        Assert.False(await Store(fixture).TryInsertDiscoveredIfAbsentAsync(write, Ct));
        Assert.Equal(before, await SnapshotAsync(fixture));
    }
    [Fact]
    public async Task Corrupt_learning_proof_is_not_accepted()
    {
        using var fixture = await SeedAsync();
        var write = await ProofAsync(fixture);
        await SqlAsync(fixture, "UPDATE process_signature_learning SET reference_json=json_set(reference_json,'$.Candidates[0].Revision.SizeBytes',999)");
        var before = await SnapshotAsync(fixture);
        await Assert.ThrowsAsync<InvalidDataException>(() => Store(fixture).TryInsertDiscoveredIfAbsentAsync(write, Ct));
        Assert.Equal(before, await SnapshotAsync(fixture));
    }
    [Fact]
    public async Task Signature_tokens_require_N_format_on_read()
    {
        using var fixture = await SeedAsync();
        var write = await ProofAsync(fixture);
        Assert.True(await Store(fixture).TryInsertDiscoveredIfAbsentAsync(write, Ct));
        await SqlAsync(fixture, "PRAGMA ignore_check_constraints=ON");
        // Invalid hex has the schema's required length but is not a token.
        await SqlAsync(fixture, "UPDATE process_signature_validation SET concurrency_token='zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz'");
        await Assert.ThrowsAsync<FormatException>(() => Store(fixture).GetAsync(write.Signature.GameId, Ct));
    }
    [Fact]
    public async Task Missing_discovery_metadata_is_ineligible()
    {
        using var fixture = await SeedAsync();
        var write = await ProofAsync(fixture);
        Assert.True(await Store(fixture).TryInsertDiscoveredIfAbsentAsync(write, Ct));
        await SqlAsync(fixture, "DELETE FROM process_signature_validation");
        var actual = await Store(fixture).GetAsync(write.Signature.GameId, Ct);
        Assert.Equal(ProcessSignatureOrigin.Discovered, actual!.Origin);
        Assert.Null(actual.Discovery);
    }
    [Fact]
    public async Task Invalidation_keeps_path_and_revision_but_rotates_token()
    {
        using var fixture = await SeedAsync();
        var write = await ProofAsync(fixture);
        Assert.True(await Store(fixture).TryInsertDiscoveredIfAbsentAsync(write, Ct));
        // Replenish same-generation proof to verify that invalidation consumes it.
        var learning = new SqliteProcessSignatureLearningStore(fixture.Options);
        var state = (await learning.LoadAsync(write.Signature.Discovery!.InstallationId!.Value, Ct))!;
        var scope = state.Inventory.Scope;
        LearningEpisodeSummary Episode(long sequence) => new(Guid.NewGuid(), sequence, scope,
            ProcessSignatureDiscoveryPolicy.CurrentPolicyVersion,
            T0.AddMinutes(sequence), T0.AddMinutes(sequence).AddSeconds(30), 0, 5, EpisodeQuality.Complete,
            [new CandidateEpisodeEvidence(write.Signature.Entries[0].ExecutablePath!, write.Signature.Entries[0].ValidatedRevision,
                true, true, [new SnapshotRange(0, 5)])]);
        var fresh = new ProcessSignatureLearningState(state.Inventory,
            ProcessSignatureDiscoveryPolicy.CurrentPolicyVersion, Guid.NewGuid(), 2, false, Episode(1), Episode(2), []);
        Assert.True(await learning.TrySaveAsync(fresh, state.ConcurrencyToken, Ct));
        Assert.True(await Store(fixture).TryInvalidateDiscoveredAsync(write.Signature.GameId, Expect(write.Signature), Ct));
        var actual = (await Store(fixture).GetAsync(write.Signature.GameId, Ct))!;
        Assert.Equal(write.Signature.Entries.ToArray(), actual.Entries.ToArray());
        Assert.Equal(write.Signature.UpdatedAtUtc, actual.UpdatedAtUtc);
        Assert.Equal(write.Signature.Origin, actual.Origin);
        Assert.Equal(write.Signature.Discovery with { ValidationState = ProcessSignatureValidationState.NeedsRevalidation,
            ConcurrencyToken = actual.Discovery!.ConcurrencyToken }, actual.Discovery);
        Assert.NotEqual(write.Signature.Discovery.ConcurrencyToken, actual.Discovery.ConcurrencyToken);
        await AssertConsumedAsync(fixture, write with { ExpectedLearningToken = fresh.ConcurrencyToken });
    }

    [Fact]
    public async Task Stale_invalidation_cannot_suspend_manual()
    {
        using var fixture = await SeedAsync();
        var write = await ProofAsync(fixture);
        Assert.True(await Store(fixture).TryInsertDiscoveredIfAbsentAsync(write, Ct));
        await Store(fixture).UpsertAsync(Explicit(write, ProcessSignatureOrigin.Manual), Ct);
        var before = await SnapshotAsync(fixture);
        Assert.False(await Store(fixture).TryInvalidateDiscoveredAsync(write.Signature.GameId, Expect(write.Signature), Ct));
        Assert.Equal(before, await SnapshotAsync(fixture));
    }
    [Theory]
    [InlineData("token")]
    [InlineData("generation")]
    [InlineData("installation")]
    [InlineData("state")]
    [InlineData("builtin")]
    public async Task Invalidation_requires_exact_current_expectation(string change)
    {
        using var fixture = await SeedAsync();
        var write = await ProofAsync(fixture);
        Assert.True(await Store(fixture).TryInsertDiscoveredIfAbsentAsync(write, Ct));
        var expected = Expect(write.Signature);
        if (change == "token") expected = expected with { ConcurrencyToken = Guid.NewGuid() };
        if (change == "generation") expected = expected with { GenerationId = Guid.NewGuid() };
        if (change == "installation") expected = expected with { InstallationId = InstallationId.New() };
        if (change == "state") expected = expected with { ValidationState = ProcessSignatureValidationState.NeedsRevalidation };
        if (change == "builtin") await Store(fixture).UpsertAsync(Explicit(write, ProcessSignatureOrigin.BuiltIn), Ct);
        var before = await SnapshotAsync(fixture);
        Assert.False(await Store(fixture).TryInvalidateDiscoveredAsync(write.Signature.GameId, expected, Ct));
        Assert.Equal(before, await SnapshotAsync(fixture));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Proof_consumption_failure_rolls_back_entire_acceptance(bool revalidate)
    {
        using var fixture = await SeedAsync();
        var pair = revalidate ? await RevalidationAsync(fixture) : (await ProofAsync(fixture), (ProcessSignature?)null);
        var write = pair.Item1;
        await SqlAsync(fixture, "CREATE TRIGGER fail_consume BEFORE UPDATE ON process_signature_learning BEGIN SELECT RAISE(ABORT,'consume failed'); END");
        var before = await SnapshotAsync(fixture);
        await Assert.ThrowsAsync<SqliteException>(() => revalidate
            ? Store(fixture).TryRevalidateDiscoveredAsync(write, Expect(pair.Item2!), Ct)
            : Store(fixture).TryInsertDiscoveredIfAbsentAsync(write, Ct));
        Assert.Equal(before, await SnapshotAsync(fixture));
    }
    [Fact]
    public async Task Invalidation_failure_rolls_back_metadata()
    {
        using var fixture = await SeedAsync();
        var write = await ProofAsync(fixture);
        Assert.True(await Store(fixture).TryInsertDiscoveredIfAbsentAsync(write, Ct));
        await SqlAsync(fixture, "CREATE TRIGGER fail_consume BEFORE UPDATE ON process_signature_learning BEGIN SELECT RAISE(ABORT,'consume failed'); END");
        var before = await SnapshotAsync(fixture);
        await Assert.ThrowsAsync<SqliteException>(() => Store(fixture).TryInvalidateDiscoveredAsync(write.Signature.GameId, Expect(write.Signature), Ct));
        Assert.Equal(before, await SnapshotAsync(fixture));
    }
    [Fact]
    public async Task Revalidation_requires_suspension_and_a_new_signature_token()
    {
        using var fixture = await SeedAsync();
        var (write, old) = await RevalidationAsync(fixture);
        var before = await SnapshotAsync(fixture);
        await Assert.ThrowsAsync<ArgumentException>(() => Store(fixture).TryRevalidateDiscoveredAsync(write,
            Expect(old) with { ValidationState = ProcessSignatureValidationState.Valid }, Ct));
        await Assert.ThrowsAsync<ArgumentException>(() => Store(fixture).TryRevalidateDiscoveredAsync(write with {
            Signature = write.Signature with { Discovery = write.Signature.Discovery! with { ConcurrencyToken = old.Discovery!.ConcurrencyToken } } }, Expect(old), Ct));
        Assert.Equal(before, await SnapshotAsync(fixture));
    }
    [Fact]
    public async Task Cancellation_does_not_commit_any_write()
    {
        using var fixture = await SeedAsync();
        var (write, old) = await RevalidationAsync(fixture);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var before = await SnapshotAsync(fixture);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Store(fixture).TryInsertDiscoveredIfAbsentAsync(write, cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Store(fixture).TryRevalidateDiscoveredAsync(write, Expect(old), cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Store(fixture).TryInvalidateDiscoveredAsync(old.GameId, Expect(old), cancellation.Token));
        Assert.Equal(before, await SnapshotAsync(fixture));
    }

    [Fact]
    public async Task Concurrent_read_never_combines_old_entries_with_new_metadata()
    {
        using var fixture = await SeedAsync();
        var write = await ProofAsync(fixture);
        Assert.True(await Store(fixture).TryInsertDiscoveredIfAbsentAsync(write, Ct));
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var writer = Task.Run(async () =>
        {
            await start.Task;
            await Store(fixture).UpsertAsync(Explicit(write, ProcessSignatureOrigin.Manual), Ct);
        });
        var reader = Task.Run(async () =>
        {
            await start.Task;
            for (var index = 0; index < 25; index++)
            {
                var actual = index % 2 == 0 ? await Store(fixture).GetAsync(write.Signature.GameId, Ct)
                    : Assert.Single(await Store(fixture).GetAllAsync(Ct));
                Assert.NotNull(actual);
                AssertSignature(actual.Origin == ProcessSignatureOrigin.Manual ? Explicit(write, ProcessSignatureOrigin.Manual)
                    : write.Signature, actual);
            }
        });
        start.SetResult();
        await Task.WhenAll(writer, reader);
    }
    [Fact]
    public async Task Insert_absent_discovered_consumes_proof_atomically()
    {
        using var fixture = await SeedAsync();
        var write = await ProofAsync(fixture);
        Assert.True(await Store(fixture).TryInsertDiscoveredIfAbsentAsync(write, Ct));
        AssertSignature(write.Signature, await Store(fixture).GetAsync(write.Signature.GameId, Ct));
        AssertSignature(write.Signature, Assert.Single(await Store(fixture).GetAllAsync(Ct)));
        await AssertConsumedAsync(fixture, write);
    }

    [Fact] public Task Insert_when_manual_exists_has_no_effect() => ProtectedInsertAsync(ProcessSignatureOrigin.Manual);
    [Fact] public Task Insert_when_builtin_exists_has_no_effect() => ProtectedInsertAsync(ProcessSignatureOrigin.BuiltIn);
    [Fact] public Task Insert_when_discovered_exists_returns_conflict() => ProtectedInsertAsync(ProcessSignatureOrigin.Discovered);
    [Fact] public Task Manual_inserted_after_stale_read_wins() => ProtectedInsertAsync(ProcessSignatureOrigin.Manual, true);
    [Fact] public Task Builtin_inserted_after_stale_read_wins() => ProtectedInsertAsync(ProcessSignatureOrigin.BuiltIn, true);
    private static async Task ProtectedInsertAsync(ProcessSignatureOrigin origin, bool staleProof = false)
    {
        using var fixture = await SeedAsync();
        var write = await ProofAsync(fixture);
        var staleStore = Store(fixture);
        Assert.Null(await staleStore.GetAsync(write.Signature.GameId, Ct));
        var learning = new SqliteProcessSignatureLearningStore(fixture.Options);
        var completed = (await learning.LoadAsync(write.Signature.Discovery!.InstallationId!.Value, Ct))!;
        await Store(fixture).UpsertAsync(Explicit(write, origin), Ct);
        if (!staleProof)
        {
            var cleared = (await learning.LoadAsync(completed.Inventory.Scope.InstallationId, Ct))!;
            var restored = new ProcessSignatureLearningState(completed.Inventory,
                ProcessSignatureDiscoveryPolicy.CurrentPolicyVersion, Guid.NewGuid(), 2, false,
                completed.Reference, completed.Confirmation, []);
            Assert.True(await learning.TrySaveAsync(restored, cleared.ConcurrencyToken, Ct));
            write = write with { ExpectedLearningToken = restored.ConcurrencyToken };
        }
        var before = await SnapshotAsync(fixture);
        Assert.False(await staleStore.TryInsertDiscoveredIfAbsentAsync(write, Ct));
        Assert.Equal(before, await SnapshotAsync(fixture));
        var actual = await staleStore.GetAsync(write.Signature.GameId, Ct);
        Assert.Equal(origin, actual!.Origin);
        Assert.Equal(new[] { "Chosen.exe", "Helper.exe" }, actual.Entries.Select(e => e.ExecutableName));
    }

    [Fact]
    public async Task Two_concurrent_inserts_have_exactly_one_winner()
    {
        using var fixture = await SeedAsync();
        var write = await ProofAsync(fixture);
        var results = await RaceAsync(() => Store(fixture).TryInsertDiscoveredIfAbsentAsync(write, Ct));
        Assert.Equal(new[] { false, true }, results.Order());
        Assert.Single(Assert.Single(await Store(fixture).GetAllAsync(Ct)).Entries);
        await AssertConsumedAsync(fixture, write);
    }
    private static async Task<bool[]> RaceAsync(Func<Task<bool>> action)
    {
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var workers = Enumerable.Range(0, 2).Select(_ => Task.Run(async () => { await start.Task; return await action(); })).ToArray();
        start.SetResult();
        return await Task.WhenAll(workers);
    }

    [Fact]
    public async Task Failure_after_parent_insert_rolls_back_parent_entries_and_proof()
    {
        using var fixture = await SeedAsync();
        var write = await ProofAsync(fixture);
        await SqlAsync(fixture, "CREATE TRIGGER fail_entry BEFORE INSERT ON process_signature_entries BEGIN SELECT RAISE(ABORT,'entry failed'); END");
        var before = await SnapshotAsync(fixture);
        await Assert.ThrowsAsync<SqliteException>(() => Store(fixture).TryInsertDiscoveredIfAbsentAsync(write, Ct));
        Assert.Equal(before, await SnapshotAsync(fixture));
    }

    private static DiscoveredSignatureExpectation Expect(ProcessSignature signature) => new(signature.Discovery!.ConcurrencyToken,
        signature.Discovery.GenerationId, signature.Discovery.ValidationState, signature.Discovery.InstallationId);
    private static async Task<(DiscoveredSignatureWrite Write, ProcessSignature Old)> RevalidationAsync(DiscoveryDatabaseFixture fixture)
    {
        var first = await ProofAsync(fixture);
        // Seed a suspended legacy signature independently, so the revalidation test does not rely on insertion.
        await Store(fixture).UpsertAsync(Explicit(first, ProcessSignatureOrigin.Discovered), Ct);
        var old = (await Store(fixture).GetAsync(first.Signature.GameId, Ct))!;
        var write = await ProofAsync(fixture, old);
        // The generation reset rotates the suspended signature token too; expect its current stored value.
        return (write, (await Store(fixture).GetAsync(first.Signature.GameId, Ct))!);
    }
    [Fact]
    public async Task Revalidation_replaces_only_expected_discovered()
    {
        using var fixture = await SeedAsync();
        var (write, old) = await RevalidationAsync(fixture);
        Assert.True(await Store(fixture).TryRevalidateDiscoveredAsync(write, Expect(old), Ct));
        AssertSignature(write.Signature, await Store(fixture).GetAsync(write.Signature.GameId, Ct));
        await AssertConsumedAsync(fixture, write);
    }
    [Fact] public Task Revalidation_stale_token_conflicts() => RevalidationConflictAsync("token");
    [Fact] public Task Revalidation_changed_generation_conflicts() => RevalidationConflictAsync("generation");
    [Fact] public Task Revalidation_changed_state_conflicts() => RevalidationConflictAsync("state");
    [Fact] public Task Revalidation_changed_installation_conflicts() => RevalidationConflictAsync("installation");
    [Fact] public Task Revalidation_replaced_by_manual_conflicts() => RevalidationConflictAsync("manual");
    [Fact] public Task Revalidation_replaced_by_builtin_conflicts() => RevalidationConflictAsync("builtin");
    private static async Task RevalidationConflictAsync(string change)
    {
        using var fixture = await SeedAsync();
        var (write, old) = await RevalidationAsync(fixture);
        var expected = Expect(old);
        if (change == "token") expected = expected with { ConcurrencyToken = Guid.NewGuid() };
        if (change == "generation") expected = expected with { GenerationId = Guid.NewGuid() };
        if (change == "installation") expected = expected with { InstallationId = InstallationId.New() };
        if (change == "state") await SqlAsync(fixture, "UPDATE process_signature_validation SET validation_state=1");
        if (change is "manual" or "builtin")
            await Store(fixture).UpsertAsync(Explicit(write, change == "manual" ? ProcessSignatureOrigin.Manual : ProcessSignatureOrigin.BuiltIn), Ct);
        var before = await SnapshotAsync(fixture);
        Assert.False(await Store(fixture).TryRevalidateDiscoveredAsync(write, expected, Ct));
        Assert.Equal(before, await SnapshotAsync(fixture));
    }
    [Fact]
    public async Task Two_concurrent_revalidations_have_one_winner()
    {
        using var fixture = await SeedAsync();
        var (write, old) = await RevalidationAsync(fixture);
        Assert.Equal(new[] { false, true }, (await RaceAsync(() => Store(fixture).TryRevalidateDiscoveredAsync(write, Expect(old), Ct))).Order());
        AssertSignature(write.Signature, Assert.Single(await Store(fixture).GetAllAsync(Ct)));
        await AssertConsumedAsync(fixture, write);
    }
    [Fact]
    public async Task Failed_revalidation_leaves_no_partial_entries()
    {
        using var fixture = await SeedAsync();
        var (write, old) = await RevalidationAsync(fixture);
        await SqlAsync(fixture, "CREATE TRIGGER fail_entry BEFORE INSERT ON process_signature_entries BEGIN SELECT RAISE(ABORT,'entry failed'); END");
        var before = await SnapshotAsync(fixture);
        await Assert.ThrowsAsync<SqliteException>(() => Store(fixture).TryRevalidateDiscoveredAsync(write, Expect(old), Ct));
        Assert.Equal(before, await SnapshotAsync(fixture));
    }
    [Fact]
    public async Task Learning_token_changed_before_acceptance_conflicts()
    {
        using var fixture = await SeedAsync();
        var write = await ProofAsync(fixture);
        await SqlAsync(fixture, "UPDATE process_signature_learning SET concurrency_token=lower(hex(randomblob(16)))");
        var before = await SnapshotAsync(fixture);
        Assert.False(await Store(fixture).TryInsertDiscoveredIfAbsentAsync(write, Ct));
        Assert.Equal(before, await SnapshotAsync(fixture));
    }
    [Theory]
    [InlineData("install_path='C:\\Moved'")]
    [InlineData("is_present=0")]
    public async Task Changed_db_root_or_presence_conflicts(string mutation)
    {
        using var fixture = await SeedAsync();
        var write = await ProofAsync(fixture);
        await SqlAsync(fixture, "UPDATE installations SET " + mutation);
        var before = await SnapshotAsync(fixture);
        Assert.False(await Store(fixture).TryInsertDiscoveredIfAbsentAsync(write, Ct));
        Assert.Equal(before, await SnapshotAsync(fixture));
    }
    [Theory]
    [InlineData(@"c:\GAMES\EXAMPLE")]
    [InlineData(@"C:\Games")]
    [InlineData(@"C:\Games\Example\Child")]
    [InlineData(@"C:\Games\Example\")]
    public async Task Overlapping_db_installations_prevent_acceptance(string root)
    {
        using var fixture = await SeedAsync();
        var write = await ProofAsync(fixture);
        await fixture.SeedInstallationAsync(GameId.New(), InstallationId.New(), root, Ct);
        var before = await SnapshotAsync(fixture);
        Assert.False(await Store(fixture).TryInsertDiscoveredIfAbsentAsync(write, Ct));
        Assert.Equal(before, await SnapshotAsync(fixture));
    }
    [Fact]
    public async Task Similar_sibling_root_is_not_ambiguous()
    {
        using var fixture = await SeedAsync();
        var write = await ProofAsync(fixture);
        await fixture.SeedInstallationAsync(GameId.New(), InstallationId.New(), @"C:\Games\Example2", Ct);
        Assert.True(await Store(fixture).TryInsertDiscoveredIfAbsentAsync(write, Ct));
    }

    [Theory]
    [InlineData("episode")]
    [InlineData("generation")]
    [InlineData("name")]
    [InlineData("path")]
    [InlineData("revision")]
    public async Task Proof_mismatch_conflicts(string change)
    {
        using var fixture = await SeedAsync();
        var write = await ProofAsync(fixture);
        var signature = write.Signature;
        if (change == "episode") write = write with { ReferenceEpisodeId = Guid.NewGuid() };
        if (change == "generation") signature = signature with { Discovery = signature.Discovery! with { GenerationId = Guid.NewGuid() } };
        if (change is "name" or "path" or "revision") signature = signature with { Entries = [signature.Entries[0] with {
            ExecutableName = change == "name" ? "Other.exe" : "Game.exe",
            ExecutablePath = change == "path" ? @"C:\Games\Example\Other.exe" : signature.Entries[0].ExecutablePath,
            ValidatedRevision = change == "revision" ? new FileRevision(999, T0) : signature.Entries[0].ValidatedRevision }] };
        var before = await SnapshotAsync(fixture);
        Assert.False(await Store(fixture).TryInsertDiscoveredIfAbsentAsync(write with { Signature = signature }, Ct));
        Assert.Equal(before, await SnapshotAsync(fixture));
    }

    [Theory]
    [InlineData("ambiguous")]
    [InlineData("incomplete")]
    [InlineData("quality")]
    [InlineData("policy")]
    [InlineData("missing-main")]
    public async Task Ineligible_proof_conflicts(string change)
    {
        using var fixture = await SeedAsync();
        var write = await ProofAsync(fixture);
        var learning = new SqliteProcessSignatureLearningStore(fixture.Options);
        var old = (await learning.LoadAsync(write.Signature.Discovery!.InstallationId!.Value, Ct))!;
        var policy = change == "policy" ? ProcessSignatureDiscoveryPolicy.CurrentPolicyVersion + 1 :
            ProcessSignatureDiscoveryPolicy.CurrentPolicyVersion;
        LearningEpisodeSummary CopyEpisode(LearningEpisodeSummary e) => new(e.EpisodeId, e.SequenceNumber, e.Scope, policy,
            e.StartedAtUtc, e.EndedAtUtc, e.FirstSnapshot, e.LastSnapshot,
            change == "quality" ? EpisodeQuality.Partial : e.Quality, change == "missing-main" ? [] : e.Candidates);
        var inventory = change == "incomplete" ? new ExecutableInventory(old.Inventory.Scope, InventoryCompleteness.Incomplete,
            old.Inventory.Candidates, [new InventoryIssue(old.Inventory.Scope.RootPath, InventoryIssueKind.AccessDenied)]) : old.Inventory;
        // Replace the row through the real persistence API; any invalidated evidence is itself ineligible.
        var next = new ProcessSignatureLearningState(inventory, policy, Guid.NewGuid(), 2, change == "ambiguous",
            CopyEpisode(old.Reference!), CopyEpisode(old.Confirmation!), []);
        if (change is "incomplete" or "ambiguous" or "policy")
        {
            await SqlAsync(fixture, "DELETE FROM process_signature_learning");
            Assert.True(await learning.TrySaveAsync(next, null, Ct));
        }
        else Assert.True(await learning.TrySaveAsync(next, old.ConcurrencyToken, Ct));
        write = write with { ExpectedLearningToken = next.ConcurrencyToken };
        if (change == "policy") write = write with { Signature = write.Signature with {
            Discovery = write.Signature.Discovery! with { PolicyVersion = policy } } };
        var before = await SnapshotAsync(fixture);
        Assert.False(await Store(fixture).TryInsertDiscoveredIfAbsentAsync(write, Ct));
        Assert.Equal(before, await SnapshotAsync(fixture));
    }

    [Theory]
    [InlineData("empty-token")]
    [InlineData("empty-episode")]
    [InlineData("empty-game")]
    [InlineData("origin")]
    [InlineData("metadata")]
    [InlineData("state")]
    [InlineData("path")]
    [InlineData("revision")]
    [InlineData("auxiliary")]
    [InlineData("two-main")]
    public async Task Invalid_request_throws_without_mutation(string change)
    {
        using var fixture = await SeedAsync();
        var write = await ProofAsync(fixture);
        var signature = write.Signature;
        if (change == "empty-token") write = write with { ExpectedLearningToken = Guid.Empty };
        if (change == "empty-episode") write = write with { ReferenceEpisodeId = Guid.Empty };
        if (change == "empty-game") signature = signature with { GameId = Guid.Empty };
        if (change == "origin") signature = signature with { Origin = ProcessSignatureOrigin.Manual };
        if (change == "metadata") signature = signature with { Discovery = null };
        if (change == "state") signature = signature with { Discovery = signature.Discovery! with { ValidationState = ProcessSignatureValidationState.NeedsRevalidation } };
        if (change == "path") signature = signature with { Entries = [signature.Entries[0] with { ExecutablePath = null }] };
        if (change == "revision") signature = signature with { Entries = [signature.Entries[0] with { ValidatedRevision = null }] };
        if (change == "auxiliary") signature = signature with { Entries = [signature.Entries[0] with { Kind = ProcessSignatureEntryKind.Auxiliary }] };
        if (change == "two-main") signature = signature with { Entries = [signature.Entries[0], signature.Entries[0]] };
        var before = await SnapshotAsync(fixture);
        await Assert.ThrowsAnyAsync<ArgumentException>(() => Store(fixture).TryInsertDiscoveredIfAbsentAsync(write with { Signature = signature }, Ct));
        Assert.Equal(before, await SnapshotAsync(fixture));
    }
    private static async Task<DiscoveryDatabaseFixture> SeedAsync()
    {
        var fixture = new DiscoveryDatabaseFixture();
        await fixture.InitializeAsync(Ct);
        return fixture;
    }
    private static async Task<DiscoveredSignatureWrite> ProofAsync(DiscoveryDatabaseFixture fixture, ProcessSignature? previous = null)
    {
        var game = previous is null ? GameId.New() : new GameId(previous.GameId);
        var installation = previous?.Discovery?.InstallationId ?? InstallationId.New();
        if (previous is not null && previous.Discovery?.InstallationId is null)
        {
            await using var connection = await fixture.OpenAsync(Ct);
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT installation_id FROM installations WHERE game_id=$game";
            command.Parameters.AddWithValue("$game", game.ToString());
            installation = new InstallationId(Guid.Parse((string)(await command.ExecuteScalarAsync(Ct))!));
        }
        if (previous is null)
            await fixture.SeedInstallationAsync(game, installation, @"C:\Games\Example", Ct);
        var scope = new InstallationScope(game, installation, @"C:\Games\Example", Guid.NewGuid(), true);
        var candidate = new ExecutableCandidate(@"C:\Games\Example\Game.exe", "Game.exe", new FileRevision(123, T0));
        var inventory = new ExecutableInventory(scope, InventoryCompleteness.Complete, [candidate], []);
        LearningEpisodeSummary Episode(long sequence) => new(Guid.NewGuid(), sequence, scope,
            ProcessSignatureDiscoveryPolicy.CurrentPolicyVersion,
            T0.AddMinutes(sequence), T0.AddMinutes(sequence).AddSeconds(30), 0, 5, EpisodeQuality.Complete,
            [new CandidateEpisodeEvidence(candidate.ExecutablePath, candidate.Revision, true, true, [new SnapshotRange(0, 5)])]);
        var state = new ProcessSignatureLearningState(inventory,
            ProcessSignatureDiscoveryPolicy.CurrentPolicyVersion, Guid.NewGuid(), 2, false, Episode(1), Episode(2), []);
        var learning = new SqliteProcessSignatureLearningStore(fixture.Options);
        var old = await learning.LoadAsync(installation, Ct);
        if (old is not null)
        {
            // A fresh generation resets evidence; save the completed pair only after that reset.
            var reset = new ProcessSignatureLearningState(inventory,
                ProcessSignatureDiscoveryPolicy.CurrentPolicyVersion, Guid.NewGuid(), 2, false, null, null, []);
            Assert.True(await learning.TrySaveAsync(reset, old.ConcurrencyToken, Ct));
            old = reset;
        }
        Assert.True(await learning.TrySaveAsync(state, old?.ConcurrencyToken, Ct));
        return new DiscoveredSignatureWrite(new ProcessSignature(game.Value,
            [new(candidate.ExecutableName, ProcessSignatureEntryKind.Main, candidate.ExecutablePath, candidate.Revision)],
            ProcessSignatureOrigin.Discovered, T0.AddHours(1),
            new DiscoveredSignatureMetadata(installation, scope.GenerationId,
                ProcessSignatureDiscoveryPolicy.CurrentPolicyVersion, ProcessSignatureValidationState.Valid, Guid.NewGuid())),
            state.ConcurrencyToken, state.Reference!.EpisodeId, state.Confirmation!.EpisodeId);
    }
    private static ProcessSignature Explicit(DiscoveredSignatureWrite write, ProcessSignatureOrigin origin) =>
        new(write.Signature.GameId, [new("Chosen.exe", ProcessSignatureEntryKind.Main), new("Helper.exe", ProcessSignatureEntryKind.Auxiliary)], origin, T0);
    private static void AssertSignature(ProcessSignature expected, ProcessSignature? actual)
    {
        Assert.NotNull(actual);
        Assert.Equal(expected.GameId, actual.GameId);
        Assert.Equal(expected.Origin, actual.Origin);
        Assert.Equal(expected.UpdatedAtUtc, actual.UpdatedAtUtc);
        Assert.Equal(expected.Discovery, actual.Discovery);
        Assert.Equal(expected.Entries.ToArray(), actual.Entries.ToArray());
    }
    private static async Task AssertConsumedAsync(DiscoveryDatabaseFixture fixture, DiscoveredSignatureWrite write)
    {
        var actual = await new SqliteProcessSignatureLearningStore(fixture.Options).LoadAsync(write.Signature.Discovery!.InstallationId!.Value, Ct);
        Assert.NotNull(actual);
        Assert.Null(actual.Reference);
        Assert.Null(actual.Confirmation);
        Assert.Empty(actual.Reasons);
        Assert.NotEqual(write.ExpectedLearningToken, actual.ConcurrencyToken);
        Assert.Equal(2, actual.LastSequenceNumber);
        Assert.Equal(write.Signature.Discovery.GenerationId, actual.Inventory.Scope.GenerationId);
        Assert.Single(actual.Inventory.Candidates);
    }
    private static async Task SqlAsync(DiscoveryDatabaseFixture fixture, string sql)
    {
        await using var connection = await fixture.OpenAsync(Ct);
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(Ct);
    }
    private static async Task<string> SnapshotAsync(DiscoveryDatabaseFixture fixture)
    {
        await using var connection = await fixture.OpenAsync(Ct);
        var rows = new List<object[]>();
        foreach (var table in new[] { "process_signatures", "process_signature_validation", "process_signature_entries", "process_signature_learning" })
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM " + table + " ORDER BY 1,2";
            await using var reader = await command.ExecuteReaderAsync(Ct);
            while (await reader.ReadAsync(Ct))
            {
                var row = new object[reader.FieldCount];
                reader.GetValues(row);
                rows.Add(row);
            }
        }
        return JsonSerializer.Serialize(rows);
    }
    [Fact]
    public void Conditional_discovery_contract_is_available()
    {
        Assert.NotNull(typeof(ProcessSignature).Assembly.GetType("PlayStead.Core.Sessions.Discovery.IProcessSignatureDiscoveryStore"));
    }
}
