using PlayStead.Core.Library;
using PlayStead.Core.Sessions;

namespace PlayStead.Core.Sessions.Discovery;

public sealed class ProcessSignatureLearningCoordinator
{
    private readonly IProcessSignatureLearningStore _learningStore;
    private readonly IProcessSignatureStore _signatureStore;
    private readonly IExecutableRevisionSource _revisionSource;
    private readonly ProcessSignatureDiscoveryPolicy _policy;
    private readonly Dictionary<InstallationId, InstallationLearning> _installations = new();
    private readonly SemaphoreSlim _gate = new(1, 1);

    public ProcessSignatureLearningCoordinator(IProcessSignatureLearningStore learningStore,
        IProcessSignatureStore signatureStore, IExecutableRevisionSource revisionSource,
        ProcessSignatureDiscoveryPolicy policy)
    {
        _learningStore = learningStore ?? throw new ArgumentNullException(nameof(learningStore));
        _signatureStore = signatureStore ?? throw new ArgumentNullException(nameof(signatureStore));
        _revisionSource = revisionSource ?? throw new ArgumentNullException(nameof(revisionSource));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
    }

    public ProcessSignatureLearningState? GetState(InstallationId installationId)
    {
        lock (_installations)
            return _installations.TryGetValue(installationId, out var installation) &&
                installation.PendingInvalidation is null ? installation.State : null;
    }

    public async Task<ProcessSignatureLearningState> InitializeAsync(
        DiscoveryInventoryContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var id = context.Inventory.Scope.InstallationId;
        await EnterGateAsync(id, cancellationToken);
        try
        {
            var pending = TryFind(id);
            var pendingInventory = pending?.PendingInvalidation?.Inventory;
            var priorInventory = pendingInventory is null ? null : pending!.State.Inventory;
            if (pendingInventory is not null)
                await FlushPendingInvalidationAsync(pending!, cancellationToken);
            var loaded = await _learningStore.LoadAsync(id, cancellationToken);
            if (loaded is not null && loaded.Inventory.Scope.GameId != context.Inventory.Scope.GameId)
                throw new ArgumentException("An installation cannot change game identity.", nameof(context));
            var inventory = pendingInventory is not null && priorInventory is not null &&
                context.Inventory.Scope.GenerationId == priorInventory.Scope.GenerationId &&
                SameInventory(pendingInventory,
                    WithGeneration(context.Inventory, pendingInventory.Scope.GenerationId))
                    ? pendingInventory : AuthoritativeInventory(context.Inventory, loaded?.Inventory);
            var changed = loaded is not null && (loaded.PolicyVersion != ProcessSignatureDiscoveryPolicy.CurrentPolicyVersion ||
                loaded.HasAmbiguousInstallation != context.HasAmbiguousInstallation ||
                !SameInventory(loaded.Inventory, inventory));
            var state = loaded;
            if (state is null || changed)
            {
                var reason = loaded?.PolicyVersion != ProcessSignatureDiscoveryPolicy.CurrentPolicyVersion && loaded is not null
                    ? DiscoveryReason.PolicyVersionChanged
                    : loaded is not null ? ChangedReason(loaded.Inventory, inventory, loaded.HasAmbiguousInstallation,
                        context.HasAmbiguousInstallation) : (DiscoveryReason?)null;
                state = new ProcessSignatureLearningState(inventory,
                    ProcessSignatureDiscoveryPolicy.CurrentPolicyVersion, Guid.NewGuid(),
                    loaded?.LastSequenceNumber ?? 0, context.HasAmbiguousInstallation,
                    null, null, reason is null ? [] : [reason.Value]);
                if (changed && loaded is not null)
                {
                    var quarantined = new InstallationLearning(loaded)
                    {
                        PendingInvalidation = state
                    };
                    lock (_installations) _installations[id] = quarantined;
                }
                bool saved;
                try
                {
                    saved = await _learningStore.TrySaveAsync(state, loaded?.ConcurrencyToken,
                        cancellationToken);
                }
                catch (OperationCanceledException) { throw; }
                catch
                {
                    lock (_installations) _installations.Remove(id);
                    throw;
                }
                if (!saved)
                {
                    lock (_installations) _installations.Remove(id);
                    throw new InvalidOperationException("Learning state changed during initialization.");
                }
            }
            var installation = new InstallationLearning(state) { Prepared = true };
            lock (_installations) _installations[id] = installation;
            return state;
        }
        finally { _gate.Release(); }
    }

    public async Task<bool> PrepareEpisodeAsync(DiscoveryInventoryContext context,
        Guid expectedLearningToken, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var id = context.Inventory.Scope.InstallationId;
        await EnterGateAsync(id, cancellationToken);
        try
        {
            var installation = Find(id);
            await FlushPendingInvalidationAsync(installation, cancellationToken);
            if (installation.State.Inventory.Scope.GameId != context.Inventory.Scope.GameId)
                throw new ArgumentException("An installation cannot change game identity.", nameof(context));
            if (installation.State.ConcurrencyToken != expectedLearningToken)
                return false;
            var inventory = AuthoritativeInventory(context.Inventory, installation.State.Inventory);
            var changed = !SameInventory(installation.State.Inventory, inventory) ||
                installation.State.HasAmbiguousInstallation != context.HasAmbiguousInstallation ||
                installation.State.PolicyVersion != ProcessSignatureDiscoveryPolicy.CurrentPolicyVersion;
            if (installation.Current is not null && !changed)
                return false;
            if (changed)
            {
                var reason = installation.State.PolicyVersion != ProcessSignatureDiscoveryPolicy.CurrentPolicyVersion
                    ? DiscoveryReason.PolicyVersionChanged
                    : ChangedReason(installation.State.Inventory, inventory,
                        installation.State.HasAmbiguousInstallation, context.HasAmbiguousInstallation);
                var replacement = NewState(installation.State, inventory, context.HasAmbiguousInstallation,
                    null, null, installation.State.LastSequenceNumber, [reason]);
                await SaveAsync(installation, replacement, cancellationToken, invalidation: true);
                installation.ResetCapture();
            }
            installation.Prepared = true;
            return true;
        }
        finally { _gate.Release(); }
    }

    public async Task<DiscoveryDecision?> ObserveAsync(InstallationId installationId,
        ProcessObservationBatch batch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(batch);
        await EnterGateAsync(installationId, cancellationToken);
        try
        {
            var installation = Find(installationId);
            await FlushPendingInvalidationAsync(installation, cancellationToken);
            var state = installation.State;
            if (installation.LastCaptureSequence is { } last &&
                (batch.SequenceNumber != last + 1 || batch.ObservedAtUtc <= installation.LastCaptureAt) ||
                batch.Quality != EpisodeQuality.Complete)
            {
                var reason = batch.Quality.HasFlag(EpisodeQuality.UnknownProcessIdentity)
                    ? DiscoveryReason.UnknownProcessIdentity : batch.Quality.HasFlag(EpisodeQuality.Partial)
                        ? DiscoveryReason.PartialEpisode : DiscoveryReason.CaptureGap;
                installation.LastCaptureSequence = batch.SequenceNumber;
                installation.LastCaptureAt = batch.ObservedAtUtc;
                return await InvalidateAsync(installation, reason, cancellationToken);
            }
            installation.LastCaptureSequence = batch.SequenceNumber;
            installation.LastCaptureAt = batch.ObservedAtUtc;

            if (!state.Inventory.Scope.IsPresent || state.HasAmbiguousInstallation ||
                state.Inventory.Completeness != InventoryCompleteness.Complete ||
                state.Inventory.Candidates.Count == 0)
            {
                installation.AbandonCurrent();
                return null;
            }

            var observed = new Dictionary<string, ProcessSnapshot>(StringComparer.OrdinalIgnoreCase);
            var startsEpisode = installation.Current is null && installation.Prepared &&
                installation.KnownAbsence >= 2 && batch.Processes.Any(process =>
                    state.Inventory.Candidates.Any(candidate => string.Equals(candidate.ExecutablePath,
                        process.ExecutablePath, StringComparison.OrdinalIgnoreCase)));
            foreach (var process in batch.Processes)
            {
                var candidate = state.Inventory.Candidates.FirstOrDefault(item =>
                    string.Equals(item.ExecutablePath, process.ExecutablePath, StringComparison.OrdinalIgnoreCase));
                if (candidate is null)
                {
                    if (UnderRoot(process.ExecutablePath, state.Inventory.Scope.RootPath))
                    {
                        if (process.ProcessId <= 0 || process.StartedAtUtc is null)
                            return await InvalidateAsync(installation,
                                DiscoveryReason.UnknownProcessIdentity, cancellationToken);
                        return await InvalidateInventoryAsync(installation,
                            new InventoryIssue(process.ExecutablePath!, InventoryIssueKind.RevisionChanged),
                            DiscoveryReason.IncompleteInventory, cancellationToken);
                    }
                    if (state.Inventory.Candidates.Any(item => string.Equals(item.ExecutableName,
                            process.ExecutableName, StringComparison.OrdinalIgnoreCase)) &&
                        (string.IsNullOrWhiteSpace(process.ExecutablePath) ||
                         installation.Current?.Identities.Values.Any(identity =>
                             identity.ProcessId == process.ProcessId) == true))
                        return await InvalidateAsync(installation, DiscoveryReason.UnreliablePath, cancellationToken);
                    if ((installation.Current is not null || startsEpisode) &&
                        (string.IsNullOrWhiteSpace(process.ExecutablePath) || process.ProcessId <= 0 ||
                         process.StartedAtUtc is null) &&
                        (process.ProcessId <= 0 ||
                         !installation.BaselineUnknown.Contains(UnknownIdentity(process))))
                        return await InvalidateAsync(installation, DiscoveryReason.UnknownProcessIdentity,
                            cancellationToken);
                    continue;
                }
                if (process.ProcessId <= 0 || process.StartedAtUtc is null)
                    return await InvalidateAsync(installation, DiscoveryReason.UnknownProcessIdentity, cancellationToken);
                if (!observed.TryAdd(candidate.ExecutablePath, process))
                    return await InvalidateAsync(installation, DiscoveryReason.UnknownProcessIdentity, cancellationToken);
                var revision = await _revisionSource.ReadAsync(state.Inventory.Scope, candidate.ExecutablePath,
                    cancellationToken);
                if (revision.Issue is not null)
                    return await InvalidateInventoryAsync(installation,
                        revision.Issue, DiscoveryReason.UnreliablePath, cancellationToken);
                if (revision.Revision != candidate.Revision)
                    return await InvalidateInventoryAsync(installation,
                        new InventoryIssue(candidate.ExecutablePath, InventoryIssueKind.RevisionChanged),
                        DiscoveryReason.RevisionChanged, cancellationToken);
            }

            if (installation.Current is not null || startsEpisode)
                installation.BaselineUnknown.IntersectWith(
                    BaselineUnknownIdentities(batch, state.Inventory));

            if (observed.Count == 0)
            {
                if (installation.Current is null)
                {
                    var unknown = BaselineUnknownIdentities(batch, state.Inventory);
                    if (installation.KnownAbsence == 0)
                    {
                        installation.BaselineUnknown.Clear();
                        installation.BaselineUnknown.UnionWith(unknown);
                    }
                    else installation.BaselineUnknown.IntersectWith(unknown);
                }
                installation.KnownAbsence = Math.Min(2, installation.KnownAbsence + 1);
                if (installation.Current is null) return null;
                installation.Current.FinalAbsences++;
                if (installation.Current.FinalAbsences < 2) return null;
                return await CompleteAsync(installation, batch, cancellationToken);
            }
            if (installation.Current is null)
            {
                if (!installation.Prepared || installation.KnownAbsence < 2)
                {
                    installation.KnownAbsence = 0;
                    return null;
                }
                var previous = state.Confirmation ?? state.Reference;
                if (previous is not null && batch.ObservedAtUtc <= previous.EndedAtUtc)
                    return await InvalidateAsync(installation, DiscoveryReason.OverlappingEpisodes, cancellationToken);
                installation.Current = new CurrentEpisode(state.LastSequenceNumber + 1,
                    batch.SequenceNumber - 2, batch.ObservedAtUtc, state.Inventory.Candidates);
                installation.Prepared = false;
            }
            var current = installation.Current;
            foreach (var (path, process) in observed)
            {
                var identity = (process.ProcessId, process.StartedAtUtc!.Value.ToUniversalTime());
                if (current!.Identities.TryGetValue(path, out var prior) && prior != identity)
                    return await InvalidateAsync(installation, DiscoveryReason.UnknownProcessIdentity, cancellationToken);
                current.Identities[path] = identity;
                var ranges = current.Ranges[path];
                if (ranges.Count > 0 && ranges[^1].Last == batch.SequenceNumber - 1)
                    ranges[^1] = new SnapshotRange(ranges[^1].First, batch.SequenceNumber);
                else ranges.Add(new SnapshotRange(batch.SequenceNumber, batch.SequenceNumber));
            }
            current!.FinalAbsences = 0;
            installation.KnownAbsence = 0;
            return null;
        }
        catch (OperationCanceledException)
        {
            lock (_installations)
                if (_installations.TryGetValue(installationId, out var installation))
                    installation.ResetCapture();
            throw;
        }
        finally { _gate.Release(); }
    }

    private async Task EnterGateAsync(InstallationId id, CancellationToken cancellationToken)
    {
        try { await _gate.WaitAsync(cancellationToken); }
        catch (OperationCanceledException)
        {
            await _gate.WaitAsync(CancellationToken.None);
            try
            {
                lock (_installations)
                    if (_installations.TryGetValue(id, out var installation))
                        installation.ResetCapture();
            }
            finally { _gate.Release(); }
            throw;
        }
    }

    private InstallationLearning? TryFind(InstallationId id)
    {
        lock (_installations)
            return _installations.GetValueOrDefault(id);
    }

    private InstallationLearning Find(InstallationId id)
    {
        return TryFind(id) ?? throw new InvalidOperationException(
            "Installation learning is not initialized.");
    }

    private async Task FlushPendingInvalidationAsync(InstallationLearning installation,
        CancellationToken cancellationToken)
    {
        if (installation.PendingInvalidation is not { } pending) return;
        await SaveAsync(installation, pending, cancellationToken, invalidation: true);
        installation.ResetCapture();
    }

    private async Task<DiscoveryDecision> CompleteAsync(InstallationLearning installation,
        ProcessObservationBatch batch, CancellationToken cancellationToken)
    {
        var state = installation.State;
        var current = installation.Current!;
        var completed = new LearningEpisodeSummary(current.EpisodeId, current.Sequence,
            state.Inventory.Scope, state.PolicyVersion, current.StartedAt, batch.ObservedAtUtc,
            current.FirstSnapshot, batch.SequenceNumber, EpisodeQuality.Complete,
            state.Inventory.Candidates.Select(candidate => new CandidateEpisodeEvidence(
                candidate.ExecutablePath, candidate.Revision, true, true,
                current.Ranges[candidate.ExecutablePath])).ToArray());
        IReadOnlyList<LearningEpisodeSummary> episodes = state.Confirmation is not null
            ? [state.Confirmation, completed]
            : state.Reference is null ? [completed] : [state.Reference, completed];
        var existing = await _signatureStore.GetAsync(state.Inventory.Scope.GameId.Value, cancellationToken);
        var decision = _policy.Evaluate(new DiscoveryEvaluation(state.Inventory, episodes,
            state.HasAmbiguousInstallation, existing?.Origin));
        if (state.Confirmation is not null && decision.Kind == DiscoveryDecisionKind.PromoteMain)
        {
            installation.Current = null;
            installation.KnownAbsence = 2;
            return decision;
        }
        var retain = decision.Kind == DiscoveryDecisionKind.PromoteMain ||
            decision.Kind == DiscoveryDecisionKind.InsufficientEvidence &&
            decision.Reasons.SequenceEqual([DiscoveryReason.AwaitingIndependentEpisode]);
        if (!retain && state.Reference is null && state.Confirmation is null &&
            state.Reasons.SequenceEqual(decision.Reasons))
        {
            installation.Current = null;
            installation.KnownAbsence = 2;
            return decision;
        }
        var reference = retain ? state.Reference ?? completed : null;
        var confirmation = decision.Kind == DiscoveryDecisionKind.PromoteMain ? completed : null;
        var replacement = NewState(state, state.Inventory, state.HasAmbiguousInstallation,
            reference, confirmation, completed.SequenceNumber, decision.Reasons);
        await SaveAsync(installation, replacement, cancellationToken, invalidation: !retain);
        installation.Current = null;
        installation.KnownAbsence = 2;
        return decision;
    }

    private async Task<DiscoveryDecision> InvalidateAsync(InstallationLearning installation,
        DiscoveryReason reason, CancellationToken cancellationToken)
    {
        var state = installation.State;
        if (state.Reference is not null || state.Confirmation is not null ||
            !state.Reasons.SequenceEqual([reason]))
        {
            var replacement = NewState(state, state.Inventory, state.HasAmbiguousInstallation,
                null, null, state.LastSequenceNumber, [reason]);
            await SaveAsync(installation, replacement, cancellationToken, invalidation: true);
        }
        installation.AbandonCurrent();
        return new DiscoveryDecision(DiscoveryDecisionKind.InsufficientEvidence, null, [reason]);
    }

    private async Task<DiscoveryDecision> InvalidateInventoryAsync(InstallationLearning installation,
        InventoryIssue issue, DiscoveryReason reason, CancellationToken cancellationToken)
    {
        var state = installation.State;
        var scope = state.Inventory.Scope;
        var newScope = new InstallationScope(scope.GameId, scope.InstallationId,
            scope.RootPath, Guid.NewGuid(), scope.IsPresent);
        var inventory = new ExecutableInventory(newScope, InventoryCompleteness.Incomplete,
            state.Inventory.Candidates, state.Inventory.Issues.Concat([issue]).ToArray());
        var replacement = NewState(state, inventory, state.HasAmbiguousInstallation,
            null, null, state.LastSequenceNumber, [reason]);
        await SaveAsync(installation, replacement, cancellationToken, invalidation: true);
        installation.ResetCapture();
        return new DiscoveryDecision(DiscoveryDecisionKind.InsufficientEvidence, null, [reason]);
    }

    private async Task SaveAsync(InstallationLearning installation,
        ProcessSignatureLearningState replacement, CancellationToken cancellationToken,
        bool invalidation = false)
    {
        if (invalidation)
            lock (_installations) installation.PendingInvalidation = replacement;
        bool saved;
        try
        {
            saved = await _learningStore.TrySaveAsync(replacement,
                installation.State.ConcurrencyToken, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            installation.ResetCapture();
            throw;
        }
        catch
        {
            DropInstallation(installation);
            throw;
        }
        if (!saved)
        {
            DropInstallation(installation);
            throw new InvalidOperationException("Learning state changed during observation.");
        }
        lock (_installations)
        {
            installation.State = replacement;
            installation.PendingInvalidation = null;
        }
    }

    private void DropInstallation(InstallationLearning installation)
    {
        installation.AbandonCurrent();
        lock (_installations)
            _installations.Remove(installation.State.Inventory.Scope.InstallationId);
    }

    private static ProcessSignatureLearningState NewState(ProcessSignatureLearningState old,
        ExecutableInventory inventory, bool ambiguous, LearningEpisodeSummary? reference,
        LearningEpisodeSummary? confirmation, long sequence, IReadOnlyList<DiscoveryReason> reasons) =>
        new(inventory, ProcessSignatureDiscoveryPolicy.CurrentPolicyVersion, Guid.NewGuid(),
            sequence, ambiguous, reference, confirmation, reasons);

    private static ExecutableInventory AuthoritativeInventory(ExecutableInventory incoming,
        ExecutableInventory? previous)
    {
        if (previous is null || incoming.Scope.GenerationId != previous.Scope.GenerationId ||
            SameInventory(previous, incoming)) return incoming;
        var scope = incoming.Scope;
        return new ExecutableInventory(new InstallationScope(scope.GameId, scope.InstallationId,
            scope.RootPath, Guid.NewGuid(), scope.IsPresent), incoming.Completeness,
            incoming.Candidates, incoming.Issues);
    }

    private static ExecutableInventory WithGeneration(ExecutableInventory inventory, Guid generation)
    {
        var scope = inventory.Scope;
        return new ExecutableInventory(new InstallationScope(scope.GameId, scope.InstallationId,
            scope.RootPath, generation, scope.IsPresent), inventory.Completeness,
            inventory.Candidates, inventory.Issues);
    }

    private static bool SameInventory(ExecutableInventory left, ExecutableInventory right)
    {
        var a = left.Scope;
        var b = right.Scope;
        return a.GameId == b.GameId && a.InstallationId == b.InstallationId &&
            a.GenerationId == b.GenerationId && a.IsPresent == b.IsPresent &&
            string.Equals(a.RootPath, b.RootPath, StringComparison.OrdinalIgnoreCase) &&
            left.Completeness == right.Completeness &&
            left.Candidates.Count == right.Candidates.Count &&
            left.Candidates.Zip(right.Candidates).All(pair =>
                string.Equals(pair.First.ExecutablePath, pair.Second.ExecutablePath, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(pair.First.ExecutableName, pair.Second.ExecutableName, StringComparison.OrdinalIgnoreCase) &&
                pair.First.Revision == pair.Second.Revision) &&
            left.Issues.Count == right.Issues.Count &&
            left.Issues.Zip(right.Issues).All(pair => pair.First.Kind == pair.Second.Kind &&
                string.Equals(pair.First.Path, pair.Second.Path, StringComparison.OrdinalIgnoreCase));
    }

    private static DiscoveryReason ChangedReason(ExecutableInventory before, ExecutableInventory after,
        bool oldAmbiguous, bool newAmbiguous) => oldAmbiguous != newAmbiguous
            ? DiscoveryReason.AmbiguousInstallation
            : !string.Equals(before.Scope.RootPath, after.Scope.RootPath,
                StringComparison.OrdinalIgnoreCase) || before.Scope.IsPresent != after.Scope.IsPresent
                ? DiscoveryReason.ScopeChanged
                : DiscoveryReason.GenerationChanged;

    private static bool UnderRoot(string? path, string root)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        var prefix = root.TrimEnd('\\', '/') + "\\";
        return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    private static (int ProcessId, string Name) UnknownIdentity(ProcessSnapshot process) =>
        (process.ProcessId, process.ExecutableName.ToUpperInvariant());

    private static HashSet<(int ProcessId, string Name)> BaselineUnknownIdentities(
        ProcessObservationBatch batch, ExecutableInventory inventory) => batch.Processes
        .Where(process => process.ProcessId > 0 &&
            (string.IsNullOrWhiteSpace(process.ExecutablePath) || process.StartedAtUtc is null) &&
            !inventory.Candidates.Any(candidate => string.Equals(candidate.ExecutableName,
                process.ExecutableName, StringComparison.OrdinalIgnoreCase)))
        .Select(UnknownIdentity).ToHashSet();

    private sealed class InstallationLearning(ProcessSignatureLearningState state)
    {
        public ProcessSignatureLearningState State { get; set; } = state;
        public ProcessSignatureLearningState? PendingInvalidation { get; set; }
        public CurrentEpisode? Current { get; set; }
        public bool Prepared { get; set; }
        public int KnownAbsence { get; set; }
        public long? LastCaptureSequence { get; set; }
        public DateTimeOffset LastCaptureAt { get; set; }
        public HashSet<(int ProcessId, string Name)> BaselineUnknown { get; } = [];

        public void AbandonCurrent()
        {
            Current = null;
            KnownAbsence = 0;
            Prepared = false;
            BaselineUnknown.Clear();
        }

        public void ResetCapture()
        {
            AbandonCurrent();
            LastCaptureSequence = null;
            LastCaptureAt = default;
        }
    }

    private sealed class CurrentEpisode
    {
        public CurrentEpisode(long sequence, long firstSnapshot, DateTimeOffset startedAt,
            IReadOnlyList<ExecutableCandidate> candidates)
        {
            Sequence = sequence;
            FirstSnapshot = firstSnapshot;
            StartedAt = startedAt;
            Ranges = candidates.ToDictionary(item => item.ExecutablePath,
                _ => new List<SnapshotRange>(), StringComparer.OrdinalIgnoreCase);
        }

        public Guid EpisodeId { get; } = Guid.NewGuid();
        public long Sequence { get; }
        public long FirstSnapshot { get; }
        public DateTimeOffset StartedAt { get; }
        public int FinalAbsences { get; set; }
        public Dictionary<string, List<SnapshotRange>> Ranges { get; }
        public Dictionary<string, (int ProcessId, DateTimeOffset StartedAt)> Identities { get; } =
            new(StringComparer.OrdinalIgnoreCase);
    }
}
