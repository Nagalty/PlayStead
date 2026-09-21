using System.Text.Json;
using Microsoft.Data.Sqlite;
using PlayStead.Core.Library;
using PlayStead.Core.Sessions.Discovery;
using PlayStead.Data.Database;
using PlayStead.Data.Sessions;
using PlayStead.Data.Tests.Database;

namespace PlayStead.Data.Tests.Sessions;

public sealed class SqliteProcessSignatureLearningStoreTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 15, 8, 0, 0, TimeSpan.Zero);
    private static readonly CancellationToken Ct = CancellationToken.None;

    [Fact]
    public async Task Absent_state_loads_null()
    {
        using var fixture = new DiscoveryDatabaseFixture();
        await fixture.InitializeAsync(Ct);
        Assert.Null(await Store(fixture).LoadAsync(InstallationId.New(), Ct));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Reference_round_trips_inventory_order_revisions_and_reasons(int candidateCount)
    {
        var state = Sample();
        if (candidateCount == 1)
        {
            var inventory = new ExecutableInventory(state.Inventory.Scope, InventoryCompleteness.Complete,
                [state.Inventory.Candidates[0]], []);
            state = new ProcessSignatureLearningState(inventory, 1, Guid.NewGuid(), 1, false,
                Episode(inventory, 1), null, [DiscoveryReason.AwaitingIndependentEpisode]);
        }
        using var fixture = await SeedAsync(state);
        Assert.True(await Store(fixture).TrySaveAsync(state, null, Ct));
        var actual = await Store(fixture).LoadAsync(state.Inventory.Scope.InstallationId, Ct);
        AssertState(state, actual);
        Assert.Equal(new SnapshotRange(3, 4), actual!.Reference!.Candidates[0].PresenceRanges[0]);
        Assert.Equal(candidateCount == 1 ? [@"C:\Games\Example\ZGame.exe"] :
            new[] { @"C:\Games\Example\ZGame.exe", @"C:\Games\Example\ALauncher.exe" },
            actual.Inventory.Candidates.Select(candidate => candidate.ExecutablePath));
    }

    [Fact]
    public async Task Confirmation_round_trips_both_exact_summaries()
    {
        var reference = Sample();
        var state = Copy(reference, confirmation: Episode(reference.Inventory, 2));
        using var fixture = await SeedAsync(state);
        Assert.True(await Store(fixture).TrySaveAsync(state, null, Ct));
        AssertState(state, await Store(fixture).LoadAsync(state.Inventory.Scope.InstallationId, Ct));
    }

    [Fact]
    public async Task New_store_instance_loads_completed_state()
    {
        var state = Sample();
        using var fixture = await SeedAsync(state);
        Assert.True(await Store(fixture).TrySaveAsync(state, null, Ct));
        AssertState(state, await Store(fixture).LoadAsync(state.Inventory.Scope.InstallationId, Ct));
    }

    [Fact]
    public void State_copies_reason_collection()
    {
        var reasons = new[] { DiscoveryReason.AwaitingIndependentEpisode };
        var sample = Sample();
        var state = new ProcessSignatureLearningState(sample.Inventory, 1, Guid.NewGuid(), 1, false,
            sample.Reference, null, reasons);
        reasons[0] = DiscoveryReason.CaptureGap;
        Assert.Equal(DiscoveryReason.AwaitingIndependentEpisode, state.Reasons[0]);
        Assert.Throws<NotSupportedException>(() => ((IList<DiscoveryReason>)state.Reasons)[0] = DiscoveryReason.CaptureGap);
    }

    [Fact]
    public void Confirmation_without_reference_is_rejected()
    {
        var sample = Sample();
        Assert.Throws<ArgumentException>(() => new ProcessSignatureLearningState(sample.Inventory, 1,
            Guid.NewGuid(), 1, false, null, sample.Reference, []));
    }

    [Fact]
    public void State_and_store_guard_invalid_arguments()
    {
        var sample = Sample();
        Assert.Throws<ArgumentNullException>(() => new SqliteProcessSignatureLearningStore(null!));
        Assert.Throws<ArgumentNullException>(() => new ProcessSignatureLearningState(null!, 1, Guid.NewGuid(), 0, false, null, null, []));
        Assert.Throws<ArgumentNullException>(() => new ProcessSignatureLearningState(sample.Inventory, 1, Guid.NewGuid(), 0, false, null, null, null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ProcessSignatureLearningState(sample.Inventory, 0, Guid.NewGuid(), 0, false, null, null, []));
        Assert.Throws<ArgumentException>(() => new ProcessSignatureLearningState(sample.Inventory, 1, Guid.Empty, 0, false, null, null, []));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ProcessSignatureLearningState(sample.Inventory, 1, Guid.NewGuid(), -1, false, null, null, []));
    }

    [Fact]
    public void Learning_store_contract_is_available()
    {
        Assert.NotNull(typeof(ExecutableInventory).Assembly.GetType("PlayStead.Core.Sessions.Discovery.ProcessSignatureLearningState"));
        Assert.NotNull(typeof(ExecutableInventory).Assembly.GetType("PlayStead.Core.Sessions.Discovery.IProcessSignatureLearningStore"));
        Assert.NotNull(typeof(DatabaseOptions).Assembly.GetType("PlayStead.Data.Sessions.SqliteProcessSignatureLearningStore"));
    }

    private static SqliteProcessSignatureLearningStore Store(DiscoveryDatabaseFixture fixture) => new(fixture.Options);

    [Fact]
    public async Task Mismatched_summary_scope_is_rejected()
    {
        var state = Sample();
        using var fixture = await SeedAsync(state);
        var other = Sample();
        var invalid = new ProcessSignatureLearningState(state.Inventory, 1, Guid.NewGuid(), 1,
            false, other.Reference, null, []);
        await Assert.ThrowsAsync<ArgumentException>(() => Store(fixture).TrySaveAsync(invalid, null, Ct));
        Assert.Null(await Store(fixture).LoadAsync(state.Inventory.Scope.InstallationId, Ct));
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("gap")]
    [InlineData("start")]
    [InlineData("overlap")]
    [InlineData("touching")]
    [InlineData("last-sequence")]
    public async Task Duplicate_or_nonadjacent_confirmation_is_rejected(string change)
    {
        var state = Sample();
        using var fixture = await SeedAsync(state);
        var confirmation = Episode(state.Inventory, change == "gap" ? 3 : 2,
            change == "duplicate" ? state.Reference!.EpisodeId : null,
            start: change switch
            {
                "start" => state.Reference!.StartedAtUtc,
                "overlap" => state.Reference!.StartedAtUtc.AddSeconds(5),
                "touching" => state.Reference!.EndedAtUtc,
                _ => null
            });
        var invalid = Copy(state, confirmation: confirmation, sequence: change == "last-sequence" ? 3 : null);
        await Assert.ThrowsAsync<ArgumentException>(() => Store(fixture).TrySaveAsync(invalid, null, Ct));
    }

    [Theory]
    [InlineData("revision")]
    [InlineData("membership")]
    [InlineData("policy")]
    [InlineData("sequence")]
    [InlineData("reason")]
    public async Task Malformed_summary_and_reason_values_are_rejected(string change)
    {
        var state = Sample();
        using var fixture = await SeedAsync(state);
        var inventory = state.Inventory;
        if (change is "revision" or "membership")
            inventory = new ExecutableInventory(inventory.Scope, inventory.Completeness,
                [new ExecutableCandidate(change == "membership" ? @"C:\Games\Example\Other.exe" : inventory.Candidates[0].ExecutablePath,
                    "ZGame.exe", new FileRevision(999, T0))], []);
        var invalid = new ProcessSignatureLearningState(inventory, change == "policy" ? 2 : 1,
            Guid.NewGuid(), change == "sequence" ? 2 : 1, false, state.Reference, null,
            change == "reason" ? [(DiscoveryReason)999] : []);
        await Assert.ThrowsAsync<ArgumentException>(() => Store(fixture).TrySaveAsync(invalid, null, Ct));
    }

    [Theory]
    [InlineData("inventory_json='{' ")]
    [InlineData("inventory_json='{}'")]
    [InlineData("inventory_json='null'")]
    [InlineData("inventory_json=json_remove(inventory_json,'$.Completeness')")]
    [InlineData("inventory_json=json_set(inventory_json,'$.completeness',0,'$.Completeness','Complete')")]
    [InlineData("inventory_json=json_set(inventory_json,'$.Scope.GameId.Value','00000000-0000-0000-0000-000000000000')")]
    [InlineData("root_path='C:\\Moved'")]
    [InlineData("generation_id='00000000000000000000000000000001'")]
    [InlineData("policy_version=2")]
    [InlineData("last_sequence_number=2")]
    [InlineData("reference_json=json_set(reference_json,'$.Candidates[0].PresenceRanges[0].Last',99)")]
    [InlineData("reference_json=json_set(reference_json,'$.Candidates[0].Revision.SizeBytes',99)")]
    [InlineData("reference_json=json_set(reference_json,'$.Scope.IsPresent',json('false'))")]
    [InlineData("reasons_json='[999]'")]
    [InlineData("reasons_json='[\"CaptureGap\"]'")]
    [InlineData("concurrency_token='zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz'")]
    [InlineData("concurrency_token='00000000-0000-0000-0000-000000000001'")]
    [InlineData("has_ambiguous_installation=2")]
    public async Task Corrupt_json_is_not_returned_as_evidence(string mutation)
    {
        var state = Sample();
        using var fixture = await SeedAsync(state);
        Assert.True(await Store(fixture).TrySaveAsync(state, null, Ct));
        await SqlAsync(fixture, "PRAGMA ignore_check_constraints=ON; UPDATE process_signature_learning SET " + mutation);
        await Assert.ThrowsAsync<InvalidDataException>(() => Store(fixture).LoadAsync(state.Inventory.Scope.InstallationId, Ct));
    }

    [Fact]
    public async Task Sql_identity_and_generation_use_D_while_tokens_use_lowercase_N()
    {
        var state = Sample();
        using var fixture = await SeedAsync(state);
        Assert.True(await Store(fixture).TrySaveAsync(state, null, Ct));
        await using var connection = await fixture.OpenAsync(Ct);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT installation_id,game_id,generation_id,concurrency_token,reasons_json FROM process_signature_learning";
        await using var reader = await command.ExecuteReaderAsync(Ct);
        Assert.True(await reader.ReadAsync(Ct));
        Assert.Equal(state.Inventory.Scope.InstallationId.Value.ToString("D"), reader.GetString(0));
        Assert.Equal(state.Inventory.Scope.GameId.Value.ToString("D"), reader.GetString(1));
        Assert.Equal(state.Inventory.Scope.GenerationId.ToString("D"), reader.GetString(2));
        Assert.Equal(state.ConcurrencyToken.ToString("N"), reader.GetString(3));
        using var reasons = JsonDocument.Parse(reader.GetString(4));
        Assert.Equal(JsonValueKind.Number, reasons.RootElement[0].ValueKind);
        Assert.Equal((int)DiscoveryReason.AwaitingIndependentEpisode, reasons.RootElement[0].GetInt32());
    }

    [Fact]
    public async Task Save_requires_expected_token()
    {
        var state = Sample();
        using var fixture = await SeedAsync(state);
        Assert.False(await Store(fixture).TrySaveAsync(state, Guid.NewGuid(), Ct));
        Assert.False(await Store(fixture).TrySaveAsync(state, Guid.Empty, Ct));
        Assert.True(await Store(fixture).TrySaveAsync(state, null, Ct));
        var next = Copy(state, confirmation: Episode(state.Inventory, 2));
        Assert.False(await Store(fixture).TrySaveAsync(next, null, Ct));
        Assert.False(await Store(fixture).TrySaveAsync(next, Guid.Empty, Ct));
        Assert.False(await Store(fixture).TrySaveAsync(next, Guid.NewGuid(), Ct));
        Assert.False(await Store(fixture).TrySaveAsync(state, state.ConcurrencyToken, Ct));
        AssertState(state, await Store(fixture).LoadAsync(state.Inventory.Scope.InstallationId, Ct));
        Assert.True(await Store(fixture).TrySaveAsync(next, state.ConcurrencyToken, Ct));
        Assert.False(await Store(fixture).TrySaveAsync(Copy(next), state.ConcurrencyToken, Ct));
        AssertState(next, await Store(fixture).LoadAsync(state.Inventory.Scope.InstallationId, Ct));
    }

    [Fact]
    public async Task Concurrent_state_saves_have_one_winner()
    {
        var state = Sample();
        using var fixture = await SeedAsync(state);
        Assert.True(await Store(fixture).TrySaveAsync(state, null, Ct));
        using var barrier = new Barrier(2);
        var proposals = new[] { Copy(state, confirmation: Episode(state.Inventory, 2)), Copy(state, clear: true) };
        var writes = proposals.Select(proposal => Task.Run(async () =>
        {
            var store = Store(fixture);
            Assert.True(barrier.SignalAndWait(TimeSpan.FromSeconds(10)));
            return await store.TrySaveAsync(proposal, state.ConcurrencyToken, Ct);
        })).ToArray();
        var results = await Task.WhenAll(writes);
        Assert.Single(results, result => result);
        AssertState(proposals[Array.IndexOf(results, true)], await Store(fixture).LoadAsync(state.Inventory.Scope.InstallationId, Ct));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("moved")]
    [InlineData("reassigned")]
    [InlineData("absent")]
    public async Task Stale_installation_identity_rejects_save(string change)
    {
        var state = Sample();
        using var fixture = await SeedAsync(state);
        if (change == "missing") await SqlAsync(fixture, "DELETE FROM installations");
        if (change == "moved") await SqlAsync(fixture, "UPDATE installations SET install_path='C:\\Moved'");
        if (change == "absent") await SqlAsync(fixture, "UPDATE installations SET is_present=0");
        if (change == "reassigned")
        {
            var other = Sample();
            await fixture.SeedInstallationAsync(other.Inventory.Scope.GameId, other.Inventory.Scope.InstallationId,
                @"C:\Other", Ct);
            await SqlAsync(fixture, $"UPDATE installations SET game_id='{other.Inventory.Scope.GameId}' WHERE installation_id='{state.Inventory.Scope.InstallationId}'");
        }
        Assert.False(await Store(fixture).TrySaveAsync(state, null, Ct));
        Assert.Null(await Store(fixture).LoadAsync(state.Inventory.Scope.InstallationId, Ct));
    }

    [Fact]
    public async Task Deliberately_absent_scope_requires_absent_installation()
    {
        var sample = Sample();
        using var fixture = await SeedAsync(sample);
        var old = sample.Inventory.Scope;
        var inventory = new ExecutableInventory(new InstallationScope(old.GameId, old.InstallationId,
            old.RootPath, Guid.NewGuid(), false), InventoryCompleteness.Incomplete, [],
            [new InventoryIssue(old.RootPath, InventoryIssueKind.MissingRoot)]);
        var absent = Copy(sample, inventory: inventory, clear: true);
        Assert.False(await Store(fixture).TrySaveAsync(absent, null, Ct));
        await SqlAsync(fixture, "UPDATE installations SET is_present=0");
        Assert.True(await Store(fixture).TrySaveAsync(absent, null, Ct));
        AssertState(absent, await Store(fixture).LoadAsync(old.InstallationId, Ct));
    }

    [Fact]
    public async Task Installation_deletion_removes_learning()
    {
        var state = Sample();
        using var fixture = await SeedAsync(state);
        Assert.True(await Store(fixture).TrySaveAsync(state, null, Ct));
        await SqlAsync(fixture, "DELETE FROM installations");
        Assert.Null(await Store(fixture).LoadAsync(state.Inventory.Scope.InstallationId, Ct));
        Assert.False(await Store(fixture).TrySaveAsync(Copy(state), state.ConcurrencyToken, Ct));
    }

    [Fact]
    public async Task Unchanged_state_is_not_written_by_load()
    {
        var state = Sample();
        using var fixture = await SeedAsync(state);
        Assert.True(await Store(fixture).TrySaveAsync(state, null, Ct));
        await SqlAsync(fixture, "CREATE TRIGGER forbid_load_write BEFORE UPDATE ON process_signature_learning BEGIN SELECT RAISE(ABORT,'load wrote'); END;");
        AssertState(state, await Store(fixture).LoadAsync(state.Inventory.Scope.InstallationId, Ct));
        AssertState(state, await Store(fixture).LoadAsync(state.Inventory.Scope.InstallationId, Ct));
        await SqlAsync(fixture, "DROP TRIGGER forbid_load_write");
        // A tentative current episode is never saved. Restart retains sequence 1, so the next completed episode is 2.
        var completed = Copy(state, confirmation: Episode(state.Inventory, 2));
        Assert.True(await Store(fixture).TrySaveAsync(completed, state.ConcurrencyToken, Ct));
        AssertState(completed, await Store(fixture).LoadAsync(state.Inventory.Scope.InstallationId, Ct));
    }

    [Fact]
    public async Task Cancellation_before_open_does_not_write()
    {
        var state = Sample();
        using var fixture = await SeedAsync(state);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Store(fixture).TrySaveAsync(state, null, cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Store(fixture).LoadAsync(state.Inventory.Scope.InstallationId, cancellation.Token));
        Assert.Null(await Store(fixture).LoadAsync(state.Inventory.Scope.InstallationId, Ct));
    }

    [Theory]
    [InlineData("order")]
    [InlineData("name")]
    [InlineData("path")]
    [InlineData("revision")]
    [InlineData("completeness")]
    [InlineData("issues")]
    [InlineData("root")]
    [InlineData("presence")]
    public async Task Changed_inventory_requires_new_generation(string change)
    {
        var original = Sample();
        if (change == "issues")
            original = Copy(original, inventory: new ExecutableInventory(original.Inventory.Scope,
                InventoryCompleteness.Incomplete, original.Inventory.Candidates,
                [new InventoryIssue(original.Inventory.Scope.RootPath, InventoryIssueKind.AccessDenied)]));
        using var fixture = await SeedAsync(original);
        Assert.True(await Store(fixture).TrySaveAsync(original, null, Ct));
        var inventory = ChangeInventory(original.Inventory, change);
        if (change == "root") await SqlAsync(fixture, "UPDATE installations SET install_path='C:\\Moved'");
        if (change == "presence") await SqlAsync(fixture, "UPDATE installations SET is_present=0");
        var invalid = Copy(original, inventory: inventory, clear: true);
        await Assert.ThrowsAsync<ArgumentException>(() => Store(fixture).TrySaveAsync(invalid, original.ConcurrencyToken, Ct));
        AssertState(original, await Store(fixture).LoadAsync(original.Inventory.Scope.InstallationId, Ct));
    }

    [Fact]
    public async Task Generation_change_clears_proof_and_invalidates_discovered()
    {
        var state = Sample();
        using var fixture = await SeedAsync(state);
        Assert.True(await Store(fixture).TrySaveAsync(state, null, Ct));
        var signatureToken = await SeedSignatureAsync(fixture, state, 0);
        var entries = await RowsAsync(fixture, "process_signature_entries");
        var generation = NewGeneration(state.Inventory);
        // Even internally consistent summaries supplied for the new generation cannot carry proof over.
        var proposed = new ProcessSignatureLearningState(generation, 1, Guid.NewGuid(), 2, false,
            Episode(generation, 1), Episode(generation, 2), [DiscoveryReason.GenerationChanged]);
        Assert.True(await Store(fixture).TrySaveAsync(proposed, state.ConcurrencyToken, Ct));
        var actual = await Store(fixture).LoadAsync(state.Inventory.Scope.InstallationId, Ct);
        AssertState(Copy(proposed, clear: true, token: proposed.ConcurrencyToken), actual);
        var metadata = await ValidationAsync(fixture);
        Assert.Equal(0, metadata.State);
        Assert.NotEqual(signatureToken, metadata.Token);
        Assert.Equal(entries, await RowsAsync(fixture, "process_signature_entries"));
    }

    [Fact]
    public async Task Policy_change_clears_learning_proof_without_invalidating_accepted_signature()
    {
        var state = Sample();
        using var fixture = await SeedAsync(state);
        Assert.True(await Store(fixture).TrySaveAsync(state, null, Ct));
        var signatureToken = await SeedSignatureAsync(fixture, state, 0);
        var proposed = new ProcessSignatureLearningState(state.Inventory,
            ProcessSignatureDiscoveryPolicy.CurrentPolicyVersion, Guid.NewGuid(), 1, false,
            Episode(state.Inventory, 1, policy: ProcessSignatureDiscoveryPolicy.CurrentPolicyVersion),
            null, [DiscoveryReason.PolicyVersionChanged]);
        Assert.True(await Store(fixture).TrySaveAsync(proposed, state.ConcurrencyToken, Ct));
        AssertState(Copy(proposed, clear: true, token: proposed.ConcurrencyToken),
            await Store(fixture).LoadAsync(state.Inventory.Scope.InstallationId, Ct));
        Assert.Equal(1, (await ValidationAsync(fixture)).State);
        Assert.Equal(signatureToken, (await ValidationAsync(fixture)).Token);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task Ambiguity_change_clears_proof(bool initial, bool changed)
    {
        var state = Copy(Sample(), ambiguous: initial);
        using var fixture = await SeedAsync(state);
        Assert.True(await Store(fixture).TrySaveAsync(state, null, Ct));
        var signatureToken = await SeedSignatureAsync(fixture, state, 0);
        var proposed = Copy(state, ambiguous: changed);
        Assert.True(await Store(fixture).TrySaveAsync(proposed, state.ConcurrencyToken, Ct));
        AssertState(Copy(proposed, clear: true, token: proposed.ConcurrencyToken),
            await Store(fixture).LoadAsync(state.Inventory.Scope.InstallationId, Ct));
        Assert.Equal(0, (await ValidationAsync(fixture)).State);
        Assert.NotEqual(signatureToken, (await ValidationAsync(fixture)).Token);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Manual_and_builtin_are_untouched_by_invalidation(int origin)
    {
        var state = Sample();
        using var fixture = await SeedAsync(state);
        Assert.True(await Store(fixture).TrySaveAsync(state, null, Ct));
        await SeedSignatureAsync(fixture, state, origin);
        var before = await SignatureRowsAsync(fixture);
        Assert.True(await Store(fixture).TrySaveAsync(Copy(state, inventory: NewGeneration(state.Inventory), clear: true), state.ConcurrencyToken, Ct));
        Assert.Equal(before, await SignatureRowsAsync(fixture));
    }

    [Fact]
    public async Task Completed_summary_with_equal_inventory_preserves_accepted_signature()
    {
        var state = Sample();
        using var fixture = await SeedAsync(state);
        Assert.True(await Store(fixture).TrySaveAsync(state, null, Ct));
        await SeedSignatureAsync(fixture, state, 0);
        var before = await SignatureRowsAsync(fixture);
        var old = state.Inventory;
        var scope = old.Scope;
        var equal = new ExecutableInventory(new InstallationScope(scope.GameId, scope.InstallationId,
            scope.RootPath.ToUpperInvariant(), scope.GenerationId, scope.IsPresent), old.Completeness,
            old.Candidates.Select(candidate => new ExecutableCandidate(candidate.ExecutablePath.ToUpperInvariant(),
                candidate.ExecutableName.ToUpperInvariant(), new FileRevision(candidate.Revision.SizeBytes, candidate.Revision.LastWriteTimeUtc))).ToArray(), []);
        var proposed = Copy(state, inventory: equal, confirmation: Episode(equal, 2));
        Assert.True(await Store(fixture).TrySaveAsync(proposed, state.ConcurrencyToken, Ct));
        AssertState(proposed, await Store(fixture).LoadAsync(scope.InstallationId, Ct));
        Assert.Equal(before, await SignatureRowsAsync(fixture));
    }

    [Fact]
    public async Task Failed_state_update_rolls_back_signature_invalidation()
    {
        var state = Sample();
        using var fixture = await SeedAsync(state);
        Assert.True(await Store(fixture).TrySaveAsync(state, null, Ct));
        await SeedSignatureAsync(fixture, state, 0);
        var before = await SignatureRowsAsync(fixture);
        await SqlAsync(fixture, "CREATE TRIGGER fail_learning BEFORE UPDATE ON process_signature_learning BEGIN SELECT RAISE(ABORT,'test failure'); END;");
        var proposed = Copy(state, inventory: NewGeneration(state.Inventory), clear: true);
        await Assert.ThrowsAsync<SqliteException>(() => Store(fixture).TrySaveAsync(proposed, state.ConcurrencyToken, Ct));
        AssertState(state, await Store(fixture).LoadAsync(state.Inventory.Scope.InstallationId, Ct));
        Assert.Equal(before, await SignatureRowsAsync(fixture));
    }

    [Fact]
    public async Task Failed_signature_invalidation_rolls_back_learning_update()
    {
        var state = Sample();
        using var fixture = await SeedAsync(state);
        Assert.True(await Store(fixture).TrySaveAsync(state, null, Ct));
        await SeedSignatureAsync(fixture, state, 0);
        var before = await SignatureRowsAsync(fixture);
        await SqlAsync(fixture, "CREATE TRIGGER fail_validation BEFORE UPDATE ON process_signature_validation BEGIN SELECT RAISE(ABORT,'test failure'); END;");
        var proposed = Copy(state, inventory: NewGeneration(state.Inventory), clear: true);
        await Assert.ThrowsAsync<SqliteException>(() => Store(fixture).TrySaveAsync(proposed, state.ConcurrencyToken, Ct));
        AssertState(state, await Store(fixture).LoadAsync(state.Inventory.Scope.InstallationId, Ct));
        Assert.Equal(before, await SignatureRowsAsync(fixture));
    }

    [Fact]
    public async Task Invalidated_state_retains_no_process_data()
    {
        var state = Sample();
        using var fixture = await SeedAsync(state);
        Assert.True(await Store(fixture).TrySaveAsync(state, null, Ct));
        var inventory = new ExecutableInventory(NewGeneration(state.Inventory).Scope, InventoryCompleteness.Complete, [], []);
        var invalidated = Copy(state, inventory: inventory, clear: true);
        Assert.True(await Store(fixture).TrySaveAsync(invalidated, state.ConcurrencyToken, Ct));
        AssertState(invalidated, await Store(fixture).LoadAsync(inventory.Scope.InstallationId, Ct));
        var persisted = await RowsAsync(fixture, "process_signature_learning");
        Assert.DoesNotContain("ZGame", persisted);
        Assert.DoesNotContain(state.Reference!.EpisodeId.ToString(), persisted);
        Assert.DoesNotContain("ProcessId", persisted);
        Assert.DoesNotContain("CurrentEpisode", persisted);
    }

    [Fact]
    public async Task Only_two_summary_slots_are_retained()
    {
        var state = Sample();
        using var fixture = await SeedAsync(state);
        Assert.True(await Store(fixture).TrySaveAsync(state, null, Ct));
        for (var sequence = 2; sequence <= 5; sequence++)
        {
            var next = new ProcessSignatureLearningState(state.Inventory, 1, Guid.NewGuid(), sequence, false,
                Episode(state.Inventory, sequence - 1), Episode(state.Inventory, sequence), []);
            Assert.True(await Store(fixture).TrySaveAsync(next, state.ConcurrencyToken, Ct));
            state = next;
        }
        AssertState(state, await Store(fixture).LoadAsync(state.Inventory.Scope.InstallationId, Ct));
        await using var connection = await fixture.OpenAsync(Ct);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*), json_extract(reference_json,'$.SequenceNumber'), json_extract(confirmation_json,'$.SequenceNumber') FROM process_signature_learning";
        await using var reader = await command.ExecuteReaderAsync(Ct);
        Assert.True(await reader.ReadAsync(Ct));
        Assert.Equal(1, reader.GetInt32(0));
        Assert.Equal(4, reader.GetInt32(1));
        Assert.Equal(5, reader.GetInt32(2));
    }

    private static ExecutableInventory NewGeneration(ExecutableInventory inventory)
    {
        var scope = inventory.Scope;
        return new(new InstallationScope(scope.GameId, scope.InstallationId, scope.RootPath, Guid.NewGuid(), scope.IsPresent),
            inventory.Completeness, inventory.Candidates, inventory.Issues);
    }

    private static ExecutableInventory ChangeInventory(ExecutableInventory original, string change)
    {
        var candidates = original.Candidates.ToArray();
        var scope = original.Scope;
        if (change == "order") Array.Reverse(candidates);
        if (change is "name" or "path" or "revision")
            candidates[0] = new ExecutableCandidate(change == "path" ? @"C:\Games\Example\Other.exe" : candidates[0].ExecutablePath,
                change == "name" ? "Other.exe" : candidates[0].ExecutableName,
                change == "revision" ? new FileRevision(999, T0) : candidates[0].Revision);
        if (change is "root" or "presence") scope = new InstallationScope(scope.GameId, scope.InstallationId,
            change == "root" ? @"C:\Moved" : scope.RootPath, scope.GenerationId, change != "presence");
        return new(scope, change is "completeness" or "issues" ? InventoryCompleteness.Incomplete : original.Completeness,
            candidates, change is "completeness" or "issues" ? [new InventoryIssue(scope.RootPath, InventoryIssueKind.IoFailure)] : original.Issues);
    }

    private static async Task<Guid> SeedSignatureAsync(DiscoveryDatabaseFixture fixture, ProcessSignatureLearningState state, int origin)
    {
        var scope = state.Inventory.Scope;
        var token = Guid.NewGuid();
        await using var connection = await fixture.OpenAsync(Ct);
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO process_signatures(game_id,origin,updated_at_utc) VALUES($game,$origin,$utc);
            INSERT INTO process_signature_entries(game_id,ordinal,executable_name,kind,executable_path,validated_size_bytes,validated_last_write_utc)
                VALUES($game,0,'ZGame.exe',0,$path,10,$utc);
            INSERT INTO process_signature_validation(game_id,installation_id,generation_id,policy_version,validation_state,concurrency_token)
                VALUES($game,$installation,$generation,1,1,$token);
            """;
        command.Parameters.AddWithValue("$game", scope.GameId.ToString());
        command.Parameters.AddWithValue("$origin", origin);
        command.Parameters.AddWithValue("$utc", T0.ToString("O"));
        command.Parameters.AddWithValue("$path", state.Inventory.Candidates[0].ExecutablePath);
        command.Parameters.AddWithValue("$installation", scope.InstallationId.ToString());
        command.Parameters.AddWithValue("$generation", scope.GenerationId.ToString("D"));
        command.Parameters.AddWithValue("$token", token.ToString("N"));
        await command.ExecuteNonQueryAsync(Ct);
        return token;
    }

    private static async Task<(int State, Guid Token)> ValidationAsync(DiscoveryDatabaseFixture fixture)
    {
        await using var connection = await fixture.OpenAsync(Ct);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT validation_state, concurrency_token FROM process_signature_validation";
        await using var reader = await command.ExecuteReaderAsync(Ct);
        Assert.True(await reader.ReadAsync(Ct));
        return (reader.GetInt32(0), Guid.Parse(reader.GetString(1)));
    }

    private static async Task<string> SignatureRowsAsync(DiscoveryDatabaseFixture fixture) =>
        await RowsAsync(fixture, "process_signatures") + await RowsAsync(fixture, "process_signature_entries") +
        await RowsAsync(fixture, "process_signature_validation");

    private static async Task<string> RowsAsync(DiscoveryDatabaseFixture fixture, string table)
    {
        await using var connection = await fixture.OpenAsync(Ct);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM " + table;
        await using var reader = await command.ExecuteReaderAsync(Ct);
        var rows = new List<object[]>();
        while (await reader.ReadAsync(Ct))
        {
            var row = new object[reader.FieldCount];
            reader.GetValues(row);
            rows.Add(row);
        }
        return JsonSerializer.Serialize(rows);
    }

    private static async Task SqlAsync(DiscoveryDatabaseFixture fixture, string sql)
    {
        await using var connection = await fixture.OpenAsync(Ct);
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(Ct);
    }

    private static ProcessSignatureLearningState Sample()
    {
        var scope = new InstallationScope(GameId.New(), InstallationId.New(), @"C:\Games\Example", Guid.NewGuid(), true);
        var inventory = new ExecutableInventory(scope, InventoryCompleteness.Complete,
            [new ExecutableCandidate(@"C:\Games\Example\ZGame.exe", "ZGame.exe", new FileRevision(10, T0)),
             new ExecutableCandidate(@"C:\Games\Example\ALauncher.exe", "ALauncher.exe", new FileRevision(20, T0.AddSeconds(1)))], []);
        return new(inventory, 1, Guid.NewGuid(), 1, false, Episode(inventory, 1), null,
            [DiscoveryReason.AwaitingIndependentEpisode]);
    }

    private static LearningEpisodeSummary Episode(ExecutableInventory inventory, long sequence, Guid? id = null,
        int policy = 1, DateTimeOffset? start = null) =>
        new(id ?? Guid.NewGuid(), sequence, inventory.Scope, policy, start ?? T0.AddMinutes(sequence),
            (start ?? T0.AddMinutes(sequence)).AddSeconds(10), 1, 6, EpisodeQuality.Complete,
            inventory.Candidates.Select(candidate => new CandidateEpisodeEvidence(candidate.ExecutablePath,
                candidate.Revision, true, true, [new SnapshotRange(3, 4)])).ToArray());

    private static ProcessSignatureLearningState Copy(ProcessSignatureLearningState state,
        ExecutableInventory? inventory = null, LearningEpisodeSummary? confirmation = null,
        int? policy = null, bool? ambiguous = null, bool clear = false, Guid? token = null,
        long? sequence = null) => new(inventory ?? state.Inventory, policy ?? state.PolicyVersion,
            token ?? Guid.NewGuid(), sequence ?? confirmation?.SequenceNumber ?? state.LastSequenceNumber,
            ambiguous ?? state.HasAmbiguousInstallation, clear ? null : state.Reference,
            clear ? null : confirmation ?? state.Confirmation, state.Reasons);

    private static async Task<DiscoveryDatabaseFixture> SeedAsync(ProcessSignatureLearningState state)
    {
        var fixture = new DiscoveryDatabaseFixture();
        await fixture.InitializeAsync(Ct);
        var scope = state.Inventory.Scope;
        await fixture.SeedInstallationAsync(scope.GameId, scope.InstallationId, scope.RootPath, Ct);
        return fixture;
    }

    private static void AssertState(ProcessSignatureLearningState expected, ProcessSignatureLearningState? actual)
    {
        Assert.NotNull(actual);
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
    }
}
