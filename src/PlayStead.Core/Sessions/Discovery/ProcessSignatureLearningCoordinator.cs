using System.Diagnostics;
using PlayStead.Core.Library;
using PlayStead.Core.Sessions;

namespace PlayStead.Core.Sessions.Discovery;

public sealed class ProcessSignatureLearningCoordinator
{
    private const string AuthorityConflictMarker = "PlayStead.Discovery.LearningAuthorityConflict";

    public static bool IsLearningAuthorityConflict(Exception error) =>
        error is InvalidOperationException && error.Data[AuthorityConflictMarker] is true;

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
                    null, null, reason is null ? [] : [reason.Value],
                    loaded?.AbsenceBaselineEstablished == true &&
                    SameLearningIdentity(loaded.Inventory, inventory));
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
            installation.KnownAbsence = state.AbsenceBaselineEstablished ? 2 : 0;
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
            // Episode preparation runs outside the capture tick. A conditional
            // acceptance or explicit Upsert can consume proof and advance the
            // durable token while this coordinator still holds the older state.
            var durable = await _learningStore.LoadAsync(id, cancellationToken);
            if (durable is null) return false;
            if (durable.ConcurrencyToken != installation.State.ConcurrencyToken)
            {
                // Only an externally advanced token can signal a stale
                // publication. A deliberate next generation with the same
                // durable token is the normal B2 preparation contract.
                if (durable.Inventory.Scope.GenerationId !=
                    context.Inventory.Scope.GenerationId) return false;
                installation.State = durable;
                installation.PendingInvalidation = null;
                installation.ResetCapture();
            }
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
                    null, null, installation.State.LastSequenceNumber, [reason],
                    installation.State.AbsenceBaselineEstablished);
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
            var unrealFamily = UnrealExecutableFamily.TryCreate(state.Inventory);
            var forensicObservation = TraceObservation(state.Inventory, unrealFamily,
                installation.Current, batch);
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
                if (forensicObservation)
                    Trace.WriteLine(
                        $"[PROCESS-FORENSIC] LearningResult GameId={state.Inventory.Scope.GameId} " +
                        "Action=InventoryUnavailable");
                return null;
            }

            var observed = new Dictionary<string, ProcessSnapshot>(StringComparer.OrdinalIgnoreCase);
            var startsEpisode = installation.Current is null && installation.Prepared &&
                installation.KnownAbsence >= 2 && batch.Processes.Any(process =>
                {
                    var candidate = ResolveCandidate(state.Inventory, unrealFamily,
                        installation.Current, process);
                    return candidate is not null &&
                        !ExecutableSupportClassifier.IsSupportExecutable(
                            state.Inventory, candidate, unrealFamily);
                });
            var supportObservationCount = 0;
            foreach (var sourceProcess in batch.Processes)
            {
                var candidate = ResolveCandidate(state.Inventory, unrealFamily,
                    installation.Current, sourceProcess);
                if (candidate is null && string.IsNullOrWhiteSpace(sourceProcess.ExecutablePath) &&
                    ExecutableSupportClassifier.IsPathlessSupportObservation(state.Inventory,
                        unrealFamily, sourceProcess.ExecutableName))
                {
                    supportObservationCount++;
                    TraceAction(state.Inventory.Scope.GameId, sourceProcess, "IgnoredSupport");
                    continue;
                }
                var process = candidate is not null &&
                    string.IsNullOrWhiteSpace(sourceProcess.ExecutablePath)
                        ? sourceProcess with { ExecutablePath = candidate.ExecutablePath }
                        : sourceProcess;
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
                    {
                        var namedCandidates = state.Inventory.Candidates.Where(item =>
                            string.Equals(item.ExecutableName, process.ExecutableName,
                                StringComparison.OrdinalIgnoreCase)).ToArray();
                        if (string.IsNullOrWhiteSpace(process.ExecutablePath) &&
                            namedCandidates.Length == 1 &&
                            IsTransientRootBootstrap(state.Inventory, namedCandidates[0], installation.Current))
                        {
                            continue;
                        }
                        if (namedCandidates.Any(candidate =>
                                !ExecutableSupportClassifier.IsSupportExecutable(
                                    state.Inventory, candidate, unrealFamily)))
                            return await InvalidateAsync(installation,
                                DiscoveryReason.UnreliablePath, cancellationToken);
                    }
                    continue;
                }
                if (ExecutableSupportClassifier.IsSupportExecutable(
                        state.Inventory, candidate, unrealFamily))
                {
                    supportObservationCount++;
                    TraceAction(state.Inventory.Scope.GameId, process, "IgnoredSupport");
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
                    var absenceBaselineWasEstablished = installation.KnownAbsence >= 2;
                    var unknown = BaselineUnknownIdentities(batch, state.Inventory);
                    if (installation.KnownAbsence == 0)
                    {
                        installation.BaselineUnknown.Clear();
                        installation.BaselineUnknown.UnionWith(unknown);
                    }
                    else installation.BaselineUnknown.IntersectWith(unknown);
                    installation.KnownAbsence = Math.Min(2, installation.KnownAbsence + 1);
                    if (!absenceBaselineWasEstablished && installation.KnownAbsence >= 2 &&
                        installation.HadCandidateObservation && state.Reference is null &&
                        state.Confirmation is null)
                    {
                        var baselineState = NewState(state, state.Inventory,
                            state.HasAmbiguousInstallation, state.Reference, state.Confirmation,
                            state.LastSequenceNumber, state.Reasons, absenceBaselineEstablished: true);
                        await SaveAsync(installation, baselineState, cancellationToken);
                        Trace.WriteLine(
                            $"[DISCOVERY-ABSENCE] GameId={state.Inventory.Scope.GameId} " +
                            "RelevantObservationCount=0 " +
                            $"SupportObservationCount={supportObservationCount} " +
                            "AbsenceForwarded=true BaselineEstablished=true");
                    }
                }
                else installation.KnownAbsence = Math.Min(2, installation.KnownAbsence + 1);
                if (installation.Current is null)
                {
                    if (forensicObservation)
                        Trace.WriteLine(
                            $"[PROCESS-FORENSIC] LearningResult GameId={state.Inventory.Scope.GameId} " +
                            "Action=NoCandidateObserved");
                    return null;
                }
                installation.Current.FinalAbsences++;
                if (installation.Current.FinalAbsences < 2)
                {
                    Trace.WriteLine(
                        $"[PROCESS-FORENSIC] LearningResult GameId={state.Inventory.Scope.GameId} " +
                        "Action=AwaitingFinalAbsence");
                    return null;
                }
                return await CompleteAsync(installation, batch, cancellationToken);
            }
            else
            {
                installation.HadCandidateObservation = true;
            }
            if (installation.Current is null)
            {
                if (!installation.Prepared || installation.KnownAbsence < 2)
                {
                    installation.KnownAbsence = 0;
                    if (forensicObservation)
                        Trace.WriteLine(
                            $"[PROCESS-FORENSIC] LearningResult GameId={state.Inventory.Scope.GameId} " +
                            "Action=AwaitingAbsenceBaseline");
                    return null;
                }
                var previous = state.Confirmation ?? state.Reference;
                if (previous is not null && batch.ObservedAtUtc <= previous.EndedAtUtc)
                    return await InvalidateAsync(installation, DiscoveryReason.OverlappingEpisodes, cancellationToken);
                installation.Current = new CurrentEpisode(state.LastSequenceNumber + 1,
                    batch.SequenceNumber - 2, batch.ObservedAtUtc, state.Inventory.Candidates);
                installation.Prepared = false;
                TraceAction(state.Inventory.Scope.GameId, observed.Values.First(),
                    state.Reference is null ? "ReferenceStarted" : "ConfirmationStarted");
            }
            else if (observed.Count > 0)
            {
                TraceAction(state.Inventory.Scope.GameId, observed.Values.First(),
                    state.Reference is null ? "ReferenceUpdated" : "ConfirmationUpdated");
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
        var mainEvidence = completed.Candidates
            .Where(candidate => candidate.PresenceRanges.Count != 0)
            .OrderByDescending(candidate => candidate.PresenceRanges[^1].Last)
            .FirstOrDefault();
        var mainCandidate = mainEvidence is null ? null : state.Inventory.Candidates.FirstOrDefault(candidate =>
            string.Equals(candidate.ExecutablePath, mainEvidence.ExecutablePath,
                StringComparison.OrdinalIgnoreCase));
        var unrealFamily = UnrealExecutableFamily.TryCreate(state.Inventory);
        var competitorTrace = BuildPromotionCompetitorTrace(state.Inventory, completed,
            decision, unrealFamily);
        var observedCompetitors = state.Inventory.Candidates.Count(candidate =>
            completed.Candidates.FirstOrDefault(evidence =>
                string.Equals(evidence.ExecutablePath, candidate.ExecutablePath,
                    StringComparison.OrdinalIgnoreCase))?.PresenceRanges.Count > 0);
        var inventoryOnlyCompetitors = state.Inventory.Candidates.Count(candidate =>
            completed.Candidates.FirstOrDefault(evidence =>
                string.Equals(evidence.ExecutablePath, candidate.ExecutablePath,
                    StringComparison.OrdinalIgnoreCase))?.PresenceRanges.Count is null or 0);
        var trueAmbiguousCompetitors = decision.Kind == DiscoveryDecisionKind.Ambiguous
            ? Math.Max(0, observedCompetitors - 1) : 0;
        Trace.WriteLine(
            $"[PROCESS-FORENSIC] LearningResult GameId={state.Inventory.Scope.GameId} " +
            $"Action={(decision.Kind == DiscoveryDecisionKind.PromoteMain ? "PromoteMain" : decision.Reasons[0].ToString())}");
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
        // Persist only the runtime winner as the episode reference. The completed
        // summary contains every inventory candidate (including never-observed
        // support/install executables); persisting that raw inventory summary made
        // the first candidate look like the reference after a reload.
        var runtimeWinner = mainEvidence is null ? completed :
            new LearningEpisodeSummary(completed.EpisodeId, completed.SequenceNumber,
                completed.Scope, completed.PolicyVersion, completed.StartedAtUtc,
                completed.EndedAtUtc, completed.FirstSnapshot, completed.LastSnapshot,
                completed.Quality, [mainEvidence]);
        var reference = retain ? state.Reference ?? runtimeWinner : null;
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
        Trace.WriteLine(
            $"[PROCESS-FORENSIC] LearningResult GameId={state.Inventory.Scope.GameId} Action={reason}");
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
        Trace.WriteLine(
            $"[PROCESS-FORENSIC] LearningResult GameId={scope.GameId} Action={reason}");
        var newScope = new InstallationScope(scope.GameId, scope.InstallationId,
            scope.RootPath, Guid.NewGuid(), scope.IsPresent);
        var inventory = new ExecutableInventory(newScope, InventoryCompleteness.Incomplete,
            state.Inventory.Candidates, state.Inventory.Issues.Concat([issue]).ToArray());
        var replacement = NewState(state, inventory, state.HasAmbiguousInstallation,
            null, null, state.LastSequenceNumber, [reason], false);
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
            var durable = await _learningStore.LoadAsync(
                installation.State.Inventory.Scope.InstallationId, cancellationToken);
            var authorityChanged = durable is not null &&
                durable.ConcurrencyToken != installation.State.ConcurrencyToken;
            DropInstallation(installation);
            var error = new InvalidOperationException(
                "Learning state changed during observation.");
            if (authorityChanged) error.Data[AuthorityConflictMarker] = true;
            throw error;
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
        LearningEpisodeSummary? confirmation, long sequence, IReadOnlyList<DiscoveryReason> reasons,
        bool? absenceBaselineEstablished = null) =>
        new(inventory, ProcessSignatureDiscoveryPolicy.CurrentPolicyVersion, Guid.NewGuid(),
            sequence, ambiguous, reference, confirmation, reasons,
            absenceBaselineEstablished ?? old.AbsenceBaselineEstablished);

    private static bool SameLearningIdentity(ExecutableInventory left, ExecutableInventory right)
    {
        var a = left.Scope;
        var b = right.Scope;
        return a.GameId == b.GameId && a.InstallationId == b.InstallationId &&
            a.IsPresent == b.IsPresent &&
            string.Equals(a.RootPath, b.RootPath, StringComparison.OrdinalIgnoreCase);
    }

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

    private static bool IsTransientRootBootstrap(ExecutableInventory inventory,
        ExecutableCandidate candidate, CurrentEpisode? current)
    {
        if (current is null || !UnderRoot(candidate.ExecutablePath, inventory.Scope.RootPath))
            return false;
        var root = inventory.Scope.RootPath.TrimEnd('\\', '/');
        var relative = candidate.ExecutablePath[(root + "\\").Length..];
        if (relative.IndexOf('\\') >= 0)
            return false;
        return inventory.Candidates.Any(other =>
            !string.Equals(other.ExecutablePath, candidate.ExecutablePath,
                StringComparison.OrdinalIgnoreCase) &&
            UnderRoot(other.ExecutablePath, inventory.Scope.RootPath) &&
            other.ExecutablePath[(root + "\\").Length..].IndexOf('\\') >= 0 &&
            current.Identities.ContainsKey(other.ExecutablePath));
    }

    private static bool TraceObservation(
        ExecutableInventory inventory,
        UnrealExecutableFamily? unrealFamily,
        CurrentEpisode? current,
        ProcessObservationBatch batch)
    {
        var scope = inventory.Scope;
        var traced = false;
        foreach (var process in batch.Processes)
        {
            var nameMatches = inventory.Candidates.Where(candidate =>
                string.Equals(candidate.ExecutableName, process.ExecutableName,
                    StringComparison.OrdinalIgnoreCase)).ToArray();
            var pathCandidate = inventory.Candidates.FirstOrDefault(candidate =>
                string.Equals(candidate.ExecutablePath, process.ExecutablePath,
                    StringComparison.OrdinalIgnoreCase));
            var inventoryNameMatch = nameMatches.Length > 0;
            var inventoryPathMatch = pathCandidate is not null;
            var installRootContained = UnderRoot(process.ExecutablePath, scope.RootPath);
            if (!inventoryNameMatch && !inventoryPathMatch && !installRootContained) continue;
            traced = true;

            var supportExcluded = pathCandidate is not null &&
                ExecutableSupportClassifier.IsSupportExecutable(inventory, pathCandidate, unrealFamily) ||
                nameMatches.Length > 0 && nameMatches.All(candidate =>
                    ExecutableSupportClassifier.IsSupportExecutable(inventory, candidate, unrealFamily));
            var resolved = ResolveCandidate(inventory, unrealFamily, current, process);
            var dropReason = supportExcluded
                ? "IgnoredSupport"
                : resolved is not null
                    ? null
                    : installRootContained
                        ? "IncompleteInventory"
                        : inventoryNameMatch
                            ? "UnreliablePath"
                            : null;

            Trace.WriteLine(
                $"[PROCESS-FORENSIC] Correlation GameId={scope.GameId} " +
                $"ProcessName={process.ExecutableName} ResolvedPath={Format(process.ExecutablePath)} " +
                $"InventoryNameMatch={Bool(inventoryNameMatch)} InventoryPathMatch={Bool(inventoryPathMatch)} " +
                $"InstallRootContained={Bool(installRootContained)} SupportExcluded={Bool(supportExcluded)} " +
                $"ObservationForwarded=true DropReason={Format(dropReason)}");
            Trace.WriteLine(
                $"[PROCESS-FORENSIC] LearningEntry GameId={scope.GameId} " +
                $"ProcessName={process.ExecutableName} ProcessPath={Format(process.ExecutablePath)} " +
                $"HasReliablePath={Bool(!string.IsNullOrWhiteSpace(process.ExecutablePath))} " +
                $"HasReliableIdentity={Bool(process.ProcessId > 0 && process.StartedAtUtc is not null)}");
        }
        return traced;
    }

    private static void TraceAction(GameId gameId, ProcessSnapshot process, string action) =>
        Trace.WriteLine(
            $"[PROCESS-FORENSIC] LearningResult GameId={gameId} " +
            $"ProcessName={process.ExecutableName} Action={action}");

    private static string Bool(bool value) => value.ToString().ToLowerInvariant();

    private static string Format(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "<null>" : value;

    private static string BuildPromotionCompetitorTrace(ExecutableInventory inventory,
        LearningEpisodeSummary episode, DiscoveryDecision decision,
        UnrealExecutableFamily? unrealFamily)
    {
        var root = inventory.Scope.RootPath.TrimEnd('\\', '/');
        var prefix = root + "\\";
        return string.Join(';', inventory.Candidates.Select(candidate =>
        {
            var evidence = episode.Candidates.FirstOrDefault(item =>
                string.Equals(item.ExecutablePath, candidate.ExecutablePath,
                    StringComparison.OrdinalIgnoreCase));
            var positive = evidence?.PresenceRanges.Count > 0;
            var lastSeen = positive == true ? evidence!.PresenceRanges.Max(range => range.Last) : (long?)null;
            var seenCount = positive == true
                ? evidence!.PresenceRanges.Sum(range => range.Last - range.First + 1) : 0;
            var underRoot = candidate.ExecutablePath.StartsWith(prefix,
                StringComparison.OrdinalIgnoreCase);
            var supportExcluded = ExecutableSupportClassifier.IsSupportExecutable(
                inventory, candidate, unrealFamily);
            var eligible = decision.Main is not null && string.Equals(
                decision.Main.ExecutablePath, candidate.ExecutablePath,
                StringComparison.OrdinalIgnoreCase);
            return string.Join(',', [
                $"Name={TraceValue(candidate.ExecutableName)}",
                $"Path={TraceValue(candidate.ExecutablePath)}",
                $"ObservedThisEpisode={Bool(positive == true)}",
                $"SeenCount={seenCount}",
                $"LastSeen={lastSeen?.ToString() ?? "<none>"}",
                $"Reliable={Bool(evidence?.HasReliablePath == true && evidence.HasReliableIdentity)}",
                $"SupportExcluded={Bool(supportExcluded)}",
                $"UnderInstallRoot={Bool(underRoot)}",
                $"InventoryOnly={Bool(positive != true)}",
                $"EligibleAsMain={Bool(eligible)}"]);
        }));
    }

    private static string TraceValue(string value) =>
        string.IsNullOrWhiteSpace(value) ? "<empty>" : value.Replace(';', '_').Replace(',', '_');

    private static ExecutableCandidate? ResolveCandidate(ExecutableInventory inventory,
        UnrealExecutableFamily? unrealFamily, CurrentEpisode? current, ProcessSnapshot process)
    {
        var exact = inventory.Candidates.FirstOrDefault(item =>
            string.Equals(item.ExecutablePath, process.ExecutablePath,
                StringComparison.OrdinalIgnoreCase));
        if (exact is not null || !string.IsNullOrWhiteSpace(process.ExecutablePath))
            return exact;
        if (current is null || process.ProcessId <= 0 || process.StartedAtUtc is null)
            return null;

        var identity = (process.ProcessId, process.StartedAtUtc.Value.ToUniversalTime());
        var boundCandidates = inventory.Candidates.Where(candidate =>
            current.Identities.TryGetValue(candidate.ExecutablePath, out var established) &&
            established == identity).ToArray();
        if (boundCandidates.Length != 1) return null;

        var candidate = boundCandidates[0];
        var nameMatches = inventory.Candidates.Count(item => string.Equals(item.ExecutableName,
            process.ExecutableName, StringComparison.OrdinalIgnoreCase));
        return nameMatches == 1 && string.Equals(candidate.ExecutableName,
                process.ExecutableName, StringComparison.OrdinalIgnoreCase) &&
            !ExecutableSupportClassifier.IsSupportExecutable(inventory, candidate, unrealFamily)
                ? candidate : null;
    }

    private static (int ProcessId, string Name, DateTimeOffset? StartedAtUtc) UnknownIdentity(
        ProcessSnapshot process) =>
        (process.ProcessId, process.ExecutableName.ToUpperInvariant(), process.StartedAtUtc);

    private static HashSet<(int ProcessId, string Name, DateTimeOffset? StartedAtUtc)> BaselineUnknownIdentities(
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
        public bool HadCandidateObservation { get; set; }
        public long? LastCaptureSequence { get; set; }
        public DateTimeOffset LastCaptureAt { get; set; }
        public HashSet<(int ProcessId, string Name, DateTimeOffset? StartedAtUtc)> BaselineUnknown { get; } = [];

        public void AbandonCurrent()
        {
            Current = null;
            KnownAbsence = 0;
            HadCandidateObservation = false;
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
