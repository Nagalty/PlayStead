using PlayStead.Core.Sessions;
using PlayStead.Core.Library;
using PlayStead.Core.Sessions.Discovery;

namespace PlayStead.Core.Tests.Sessions.Discovery;

public sealed class DiscoveredSignatureValidatorTests
{
    [Theory]
    [InlineData("PlayStead.Core.Sessions.IDiscoveredSignatureValidator")]
    [InlineData("PlayStead.Core.Sessions.DiscoveredSignatureValidationResult")]
    [InlineData("PlayStead.Core.Sessions.Discovery.DiscoveryInventoryContext")]
    [InlineData("PlayStead.Core.Sessions.Discovery.DiscoveredSignatureValidator")]
    public void Validator_API_is_available(string name)
        => Assert.NotNull(typeof(SessionRuntime).Assembly.GetType(name));

    [Fact]
    public async Task Persisted_valid_without_fresh_inventory_is_pending()
    {
        var f = new Fixture { Current = null };
        Assert.Equal(DiscoveredSignatureValidationResult.Pending, await f.Validate());
        Assert.Empty(f.Invalidations);
        Assert.Empty(f.Reads);
    }

    [Fact]
    public async Task Empty_discovered_entries_are_invalid()
    {
        var f = new Fixture();
        f.Input = f.Input with { Entries = [] };
        Assert.Equal(DiscoveredSignatureValidationResult.Invalid, await f.Validate());
    }

    [Fact]
    public async Task Fresh_inventory_replaced_during_revision_read_is_not_used()
    {
        var f = new Fixture();
        f.AfterRead = () => f.Corrupt("generation");
        Assert.Equal(DiscoveredSignatureValidationResult.Invalid, await f.Validate());
        Assert.Single(f.Invalidations);
    }

    [Fact]
    public async Task Inventory_becoming_pending_during_revision_read_does_not_invalidate()
    {
        var f = new Fixture();
        f.AfterRead = () => f.Current = null;
        Assert.Equal(DiscoveredSignatureValidationResult.Pending, await f.Validate());
        Assert.Empty(f.Invalidations);
    }

    [Fact]
    public async Task Matching_inventory_cannot_claim_an_executable_outside_its_root()
    {
        var f = new Fixture();
        var s = f.Current!.Inventory.Scope;
        var inv = new ExecutableInventory(new(s.GameId, s.InstallationId, @"C:\Games\On", s.GenerationId, true),
            InventoryCompleteness.Complete, f.Current.Inventory.Candidates, []);
        f.Current = new(inv, false);
        f.State = f.NewState(inv);
        Assert.Equal(DiscoveredSignatureValidationResult.Invalid, await f.Validate());
        Assert.Single(f.Invalidations);
    }

    [Fact]
    public async Task Missing_signature_argument_is_rejected()
    {
        var f = new Fixture();
        await Assert.ThrowsAsync<ArgumentNullException>(() => f.Validator().ValidateAsync(null!, default));
    }

    [Fact]
    public async Task Cancellation_before_pending_is_propagated()
    {
        var f = new Fixture { Current = null };
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.Validate(cts.Token));
        Assert.Empty(f.Invalidations);
    }

    [Fact]
    public async Task Fresh_matching_inventory_and_revision_are_valid()
    {
        var f = new Fixture();
        Assert.NotSame(f.State!.Inventory, f.Current!.Inventory);
        Assert.Equal(DiscoveredSignatureValidationResult.Valid, await f.Validate());
        var read = Assert.Single(f.Reads);
        Assert.Equal(f.Current.Inventory.Scope, read.Scope);
        Assert.Equal(ProcessSignaturePathMatcherTests.Path, read.Path);
        Assert.Empty(f.Invalidations);
    }

    [Fact]
    public async Task Revision_is_reread_each_use()
    {
        var f = new Fixture();
        var validator = f.Validator();
        Assert.Equal(DiscoveredSignatureValidationResult.Valid, await validator.ValidateAsync(f.Input, default));
        f.Revision = new(new FileRevision(11, ProcessSignaturePathMatcherTests.T0), null);
        Assert.Equal(DiscoveredSignatureValidationResult.Invalid, await validator.ValidateAsync(f.Input, default));
        Assert.Equal(2, f.Reads.Count);
        Assert.Single(f.Invalidations);
    }

    [Theory]
    [InlineData("size")]
    [InlineData("timestamp")]
    [InlineData("inaccessible")]
    [InlineData("null-result")]
    public async Task Revision_failure_invalidates_with_exact_expectation(string defect)
    {
        var f = new Fixture();
        f.Revision = defect switch {
            "size" => new(new FileRevision(11, ProcessSignaturePathMatcherTests.T0), null),
            "timestamp" => new(new FileRevision(10, ProcessSignaturePathMatcherTests.T0.AddSeconds(1)), null),
            "inaccessible" => new(null, new(ProcessSignaturePathMatcherTests.Path, InventoryIssueKind.AccessDenied)),
            _ => null!
        };
        f.InvalidationSucceeds = false;
        Assert.Equal(DiscoveredSignatureValidationResult.Invalid, await f.Validate());
        Assert.Equal((f.Input.GameId, new DiscoveredSignatureExpectation(f.Input.Discovery!.ConcurrencyToken,
            f.Input.Discovery.GenerationId, ProcessSignatureValidationState.Valid, f.Input.Discovery.InstallationId)),
            Assert.Single(f.Invalidations));
    }

    [Theory]
    [InlineData("generation")]
    [InlineData("policy")]
    [InlineData("absent")]
    [InlineData("ambiguous")]
    [InlineData("persisted-ambiguous")]
    [InlineData("root")]
    [InlineData("owner")]
    [InlineData("installation")]
    [InlineData("files")]
    [InlineData("revision")]
    [InlineData("incomplete")]
    [InlineData("missing-state")]
    [InlineData("missing-signature")]
    [InlineData("persisted-generation")]
    [InlineData("entry-not-in-inventory")]
    public async Task Fresh_or_persisted_inconsistency_invalidates(string defect)
    {
        var f = new Fixture();
        f.Corrupt(defect);
        Assert.Equal(DiscoveredSignatureValidationResult.Invalid, await f.Validate());
        Assert.Single(f.Invalidations);
    }

    [Theory]
    [InlineData("signature-token")]
    [InlineData("signature-entry")]
    [InlineData("signature-origin")]
    [InlineData("signature-deleted")]
    [InlineData("learning-token")]
    [InlineData("learning-deleted")]
    public async Task Stale_authority_after_revision_read_is_rejected(string change)
    {
        var f = new Fixture();
        f.AfterRead = () => {
            switch (change) {
                case "signature-token": f.Stored = f.Input with { Discovery = f.Input.Discovery! with { ConcurrencyToken = Guid.NewGuid() } }; break;
                case "signature-entry": f.Stored = f.Input with { Entries = [f.Input.Entries[0] with { ExecutablePath = @"C:\Games\Other\Game.exe" }] }; break;
                case "signature-origin": f.Stored = f.Input with { Origin = ProcessSignatureOrigin.Manual }; break;
                case "signature-deleted": f.Stored = null; break;
                case "learning-token": f.State = f.NewState(token: Guid.NewGuid()); break;
                case "learning-deleted": f.State = null; break;
            }
        };
        Assert.Equal(DiscoveredSignatureValidationResult.Invalid, await f.Validate());
        Assert.Single(f.Reads);
    }

    [Fact]
    public async Task Cancellation_propagates_without_invalidation()
    {
        var f = new Fixture();
        using var cts = new CancellationTokenSource();
        f.AfterRead = cts.Cancel;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.Validate(cts.Token));
        Assert.Empty(f.Invalidations);
    }

    [Theory]
    [InlineData("legacy")]
    [InlineData("state")]
    [InlineData("unknown-policy")]
    [InlineData("revision")]
    [InlineData("token")]
    public async Task Inadmissible_signature_has_no_read_or_write_loop(string defect)
    {
        var f = new Fixture();
        f.Input = ProcessSignaturePathMatcherTests.Malformed(f.Input, defect);
        for (var i = 0; i < 2; i++)
            Assert.Equal(DiscoveredSignatureValidationResult.Invalid, await f.Validate());
        Assert.Empty(f.Reads);
        Assert.Empty(f.Invalidations);
    }

    [Fact]
    public async Task Every_entry_revision_is_checked()
    {
        var f = new Fixture();
        var extra = new ProcessSignatureEntry("Helper.exe", ProcessSignatureEntryKind.Auxiliary,
            @"C:\Games\One\Helper.exe", new FileRevision(10, ProcessSignaturePathMatcherTests.T0));
        f.Input = f.Input with { Entries = [f.Input.Entries[0], extra] };
        f.Stored = f.Input;
        var inv = new ExecutableInventory(f.Current!.Inventory.Scope, InventoryCompleteness.Complete,
            [f.Current.Inventory.Candidates[0], new(extra.ExecutablePath!, extra.ExecutableName, extra.ValidatedRevision!)], []);
        f.Current = new(inv, false);
        f.State = f.NewState(inv);
        Assert.Equal(DiscoveredSignatureValidationResult.Valid, await f.Validate());
        Assert.Equal(new[] { ProcessSignaturePathMatcherTests.Path, extra.ExecutablePath }, f.Reads.Select(r => r.Path));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void Constructor_rejects_missing_dependency(int missing)
    {
        var f = new Fixture();
        Assert.Throws<ArgumentNullException>(() => new DiscoveredSignatureValidator(
            missing == 0 ? null! : f, missing == 1 ? null! : f, missing == 2 ? null! : f,
            missing == 3 ? null! : f, missing == 4 ? null! : _ => f.Current));
    }

    internal sealed class Fixture : IProcessSignatureStore, IProcessSignatureLearningStore,
        IProcessSignatureDiscoveryStore, IExecutableRevisionSource
    {
        internal ProcessSignature Input = ProcessSignaturePathMatcherTests.Signature();
        internal ProcessSignature? Stored;
        internal ProcessSignatureLearningState? State;
        internal DiscoveryInventoryContext? Current;
        internal ExecutableRevisionResult Revision = new(new FileRevision(10, ProcessSignaturePathMatcherTests.T0), null);
        internal Action? AfterRead;
        internal bool InvalidationSucceeds = true;
        internal List<(InstallationScope Scope, string Path)> Reads = [];
        internal List<(Guid Game, DiscoveredSignatureExpectation Expected)> Invalidations = [];
        internal Fixture()
        {
            Stored = Input;
            var scope = new InstallationScope(new GameId(Input.GameId), Input.Discovery!.InstallationId!.Value,
                @"C:\Games\One", Input.Discovery.GenerationId!.Value, true);
            var inventory = new ExecutableInventory(scope, InventoryCompleteness.Complete,
                [new(ProcessSignaturePathMatcherTests.Path, "Game.exe", new(10, ProcessSignaturePathMatcherTests.T0))], []);
            State = NewState(inventory);
            Current = new(new ExecutableInventory(scope, inventory.Completeness,
                [new(ProcessSignaturePathMatcherTests.Path, "Game.exe", new(10, ProcessSignaturePathMatcherTests.T0))], []), false);
        }
        internal ProcessSignatureLearningState NewState(ExecutableInventory? inventory = null, int policy = 1,
            Guid? token = null, bool ambiguous = false) => new(inventory ?? State!.Inventory, policy,
                token ?? State?.ConcurrencyToken ?? Guid.NewGuid(), 2, ambiguous, null, null, []);
        internal DiscoveredSignatureValidator Validator() => new(this, this, this, this, _ => Current);
        internal Task<DiscoveredSignatureValidationResult> Validate(CancellationToken ct = default) => Validator().ValidateAsync(Input, ct);
        internal void Corrupt(string defect)
        {
            var inv = Current!.Inventory;
            var s = inv.Scope;
            if (defect == "policy") { State = NewState(policy: 999); return; }
            if (defect == "missing-state") { State = null; return; }
            if (defect == "missing-signature") { Stored = null; return; }
            if (defect == "ambiguous") { Current = Current with { HasAmbiguousInstallation = true }; return; }
            if (defect == "persisted-ambiguous") { State = NewState(ambiguous: true); return; }
            if (defect == "entry-not-in-inventory") {
                Input = Input with { Entries = [Input.Entries[0] with { ExecutablePath = @"C:\Games\One\Sub\Game.exe" }] };
                Stored = Input; return;
            }
            var scope = new InstallationScope(defect == "owner" ? new GameId(Guid.NewGuid()) : s.GameId,
                defect == "installation" ? new InstallationId(Guid.NewGuid()) : s.InstallationId,
                defect == "root" ? @"C:\Games\Other" : s.RootPath,
                defect is "generation" or "persisted-generation" ? Guid.NewGuid() : s.GenerationId, defect != "absent");
            var changed = new ExecutableInventory(scope,
                defect == "incomplete" ? InventoryCompleteness.Incomplete : InventoryCompleteness.Complete,
                defect == "files" ? [] : [new(ProcessSignaturePathMatcherTests.Path, "Game.exe",
                    new(defect == "revision" ? 11 : 10, ProcessSignaturePathMatcherTests.T0))],
                defect == "incomplete" ? [new(s.RootPath, InventoryIssueKind.AccessDenied)] : []);
            if (defect == "persisted-generation") State = NewState(changed);
            else Current = new(changed, false);
        }
        public Task<ProcessSignature?> GetAsync(Guid gameId, CancellationToken ct)
            => Task.FromResult(Stored is null ? null : Stored with { Entries = Stored.Entries.Select(e => e with { }).ToArray() });
        public Task<IReadOnlyList<ProcessSignature>> GetAllAsync(CancellationToken ct)
            => Task.FromResult<IReadOnlyList<ProcessSignature>>(Stored is null ? [] : [Stored]);
        public Task UpsertAsync(ProcessSignature signature, CancellationToken ct) => throw new NotSupportedException();
        public Task<ProcessSignatureLearningState?> LoadAsync(InstallationId installationId, CancellationToken ct)
            => Task.FromResult(State is null ? null : NewState(new ExecutableInventory(State.Inventory.Scope,
                State.Inventory.Completeness, State.Inventory.Candidates.Select(c => new ExecutableCandidate(
                    c.ExecutablePath, c.ExecutableName, new FileRevision(c.Revision.SizeBytes, c.Revision.LastWriteTimeUtc))).ToArray(),
                State.Inventory.Issues.ToArray()), State.PolicyVersion, State.ConcurrencyToken, State.HasAmbiguousInstallation));
        public Task<bool> TrySaveAsync(ProcessSignatureLearningState state, Guid? expectedConcurrencyToken, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> TryInsertDiscoveredIfAbsentAsync(DiscoveredSignatureWrite write, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> TryRevalidateDiscoveredAsync(DiscoveredSignatureWrite write, DiscoveredSignatureExpectation expected, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> TryInvalidateDiscoveredAsync(Guid gameId, DiscoveredSignatureExpectation expected, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            Invalidations.Add((gameId, expected));
            return Task.FromResult(InvalidationSucceeds);
        }
        public async Task<ExecutableRevisionResult> ReadAsync(InstallationScope scope, string executablePath, CancellationToken ct)
        {
            Reads.Add((scope, executablePath));
            await Task.Yield();
            AfterRead?.Invoke();
            return Revision;
        }
    }
}
