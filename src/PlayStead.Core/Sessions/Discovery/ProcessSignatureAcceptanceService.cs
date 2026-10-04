using PlayStead.Core.Library;

namespace PlayStead.Core.Sessions.Discovery;

public sealed class ProcessSignatureAcceptanceService
{
    private readonly IProcessSignatureLearningStore _learningStore;
    private readonly IProcessSignatureStore _signatureStore;
    private readonly IProcessSignatureDiscoveryStore _discoveryStore;
    private readonly IExecutableRevisionSource _revisionSource;
    private readonly ProcessSignatureDiscoveryPolicy _policy;
    private readonly Func<InstallationId, DiscoveryInventoryContext?> _currentInventory;
    private readonly TimeProvider _timeProvider;
    private readonly DiscoveryConfirmationSessionPromoter _sessionPromoter;
    public ProcessSignatureAcceptanceService(
        IProcessSignatureLearningStore learningStore,
        IProcessSignatureStore signatureStore,
        IProcessSignatureDiscoveryStore discoveryStore,
        IExecutableRevisionSource revisionSource,
        ProcessSignatureDiscoveryPolicy policy,
        Func<InstallationId, DiscoveryInventoryContext?> currentInventory,
        TimeProvider timeProvider,
        DiscoveryConfirmationSessionPromoter sessionPromoter)
    {
        _learningStore = learningStore ?? throw new ArgumentNullException(nameof(learningStore));
        _signatureStore = signatureStore ?? throw new ArgumentNullException(nameof(signatureStore));
        _discoveryStore = discoveryStore ?? throw new ArgumentNullException(nameof(discoveryStore));
        _revisionSource = revisionSource ?? throw new ArgumentNullException(nameof(revisionSource));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _currentInventory = currentInventory ?? throw new ArgumentNullException(nameof(currentInventory));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _sessionPromoter = sessionPromoter ?? throw new ArgumentNullException(nameof(sessionPromoter));
    }

    public async Task<bool> TryAcceptAsync(InstallationId installationId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var state = await _learningStore.LoadAsync(installationId, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (state?.Reference is null || state.Confirmation is null)
        {
            return false;
        }
        var current = _currentInventory(installationId);
        if (current is null || current.HasAmbiguousInstallation != state.HasAmbiguousInstallation ||
            !InventoryEquals(current.Inventory, state.Inventory) ||
            state.PolicyVersion != ProcessSignatureDiscoveryPolicy.CurrentPolicyVersion)
        {
            return false;
        }

        var stored = await _signatureStore.GetAsync(state.Inventory.Scope.GameId.Value, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (stored is not null && (stored.Origin != ProcessSignatureOrigin.Discovered ||
            stored.Discovery?.ValidationState != ProcessSignatureValidationState.NeedsRevalidation)) return false;
        var expected = stored?.Discovery is { } metadata
            ? new DiscoveredSignatureExpectation(metadata.ConcurrencyToken, metadata.GenerationId,
                metadata.ValidationState, metadata.InstallationId) : null;
        var decision = _policy.Evaluate(new DiscoveryEvaluation(state.Inventory,
            [state.Reference, state.Confirmation], state.HasAmbiguousInstallation, stored?.Origin));
        if (decision.Kind != DiscoveryDecisionKind.PromoteMain)
        {
            return false;
        }

        // The persisted learning reference may predate the current winner (for
        // example after an inventory refresh). Align it with the candidate that
        // the same policy has just accepted before writing MainProcess.
        var main = decision.Main ?? throw new InvalidOperationException("Promotion requires a main candidate.");
        var alignedReference = AlignReference(state.Reference, state.Confirmation, main);
        if (alignedReference is not null &&
            !string.Equals(state.Reference?.Candidates.FirstOrDefault()?.ExecutablePath,
                alignedReference.Candidates.FirstOrDefault()?.ExecutablePath,
                StringComparison.OrdinalIgnoreCase))
        {
            var alignedState = new ProcessSignatureLearningState(state.Inventory,
                state.PolicyVersion, Guid.NewGuid(), state.LastSequenceNumber,
                state.HasAmbiguousInstallation, alignedReference, state.Confirmation,
                state.Reasons, state.AbsenceBaselineEstablished);
            if (!await _learningStore.TrySaveAsync(alignedState, state.ConcurrencyToken,
                    cancellationToken))
            {
                return false;
            }
            state = alignedState;
        }

        // Every candidate contributes to the proof, including startup companions.
        // Filesystem reads happen outside the conditional database transaction.
        foreach (var candidate in state.Inventory.Candidates)
        {
            var revision = await _revisionSource.ReadAsync(state.Inventory.Scope, candidate.ExecutablePath, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (revision?.Revision != candidate.Revision)
            {
                var cleared = new ProcessSignatureLearningState(state.Inventory, state.PolicyVersion,
                    Guid.NewGuid(), state.LastSequenceNumber, state.HasAmbiguousInstallation,
                    null, null, [DiscoveryReason.RevisionChanged], state.AbsenceBaselineEstablished);
                await _learningStore.TrySaveAsync(cleared, state.ConcurrencyToken, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (expected is not null)
                    await _discoveryStore.TryInvalidateDiscoveredAsync(state.Inventory.Scope.GameId.Value, expected, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                return false;
            }
        }

        var latestInventory = _currentInventory(installationId);
        cancellationToken.ThrowIfCancellationRequested();
        if (latestInventory is null || latestInventory.HasAmbiguousInstallation != state.HasAmbiguousInstallation ||
            !InventoryEquals(latestInventory.Inventory, state.Inventory)) return false;

        var accepted = new ProcessSignature(state.Inventory.Scope.GameId.Value,
            [new ProcessSignatureEntry(main.ExecutableName, ProcessSignatureEntryKind.Main,
                main.ExecutablePath, main.Revision)], ProcessSignatureOrigin.Discovered, _timeProvider.GetUtcNow(),
            new DiscoveredSignatureMetadata(state.Inventory.Scope.InstallationId, state.Inventory.Scope.GenerationId,
                state.PolicyVersion, ProcessSignatureValidationState.Valid, Guid.NewGuid()));
        var reference = state.Reference ?? throw new InvalidOperationException("Promotion requires a reference episode.");
        var confirmation = state.Confirmation ?? throw new InvalidOperationException("Promotion requires a confirmation episode.");
        var write = new DiscoveredSignatureWrite(accepted, state.ConcurrencyToken,
            reference.EpisodeId, confirmation.EpisodeId);
        var acceptedDurably = expected is null
            ? await _discoveryStore.TryInsertDiscoveredIfAbsentAsync(write, cancellationToken)
            : await _discoveryStore.TryRevalidateDiscoveredAsync(write, expected, cancellationToken);
        if (!acceptedDurably)
        {
            return false;
        }

        await _sessionPromoter.PersistAsync(confirmation, cancellationToken);
        return true;
    }

    private static bool InventoryEquals(ExecutableInventory left, ExecutableInventory right) =>
        left.Scope.GameId == right.Scope.GameId && left.Scope.InstallationId == right.Scope.InstallationId &&
        left.Scope.GenerationId == right.Scope.GenerationId && left.Scope.IsPresent == right.Scope.IsPresent &&
        Equal(left.Scope.RootPath, right.Scope.RootPath) && left.Completeness == right.Completeness &&
        left.Candidates.Count == right.Candidates.Count && left.Candidates.Zip(right.Candidates).All(pair =>
            Equal(pair.First.ExecutablePath, pair.Second.ExecutablePath) && Equal(pair.First.ExecutableName, pair.Second.ExecutableName) &&
            pair.First.Revision == pair.Second.Revision) && left.Issues.Count == right.Issues.Count &&
        left.Issues.Zip(right.Issues).All(pair => pair.First.Kind == pair.Second.Kind && Equal(pair.First.Path, pair.Second.Path));

    private static LearningEpisodeSummary? AlignReference(
        LearningEpisodeSummary? reference, LearningEpisodeSummary? confirmation,
        ExecutableCandidate main)
    {
        if (reference is null) return null;
        var evidence = reference.Candidates.FirstOrDefault(candidate =>
            string.Equals(candidate.ExecutablePath, main.ExecutablePath,
                StringComparison.OrdinalIgnoreCase));
        if (evidence is null)
        {
            var confirmationEvidence = confirmation?.Candidates.FirstOrDefault(candidate =>
                string.Equals(candidate.ExecutablePath, main.ExecutablePath,
                    StringComparison.OrdinalIgnoreCase));
            if (confirmationEvidence is null) return null;
            var ranges = confirmationEvidence.PresenceRanges.Where(range =>
                range.First >= reference.FirstSnapshot && range.Last <= reference.LastSnapshot).ToArray();
            if (ranges.Length == 0) return null;
            evidence = new CandidateEpisodeEvidence(confirmationEvidence.ExecutablePath,
                confirmationEvidence.Revision, confirmationEvidence.HasReliablePath,
                confirmationEvidence.HasReliableIdentity, ranges);
        }
        return new LearningEpisodeSummary(reference.EpisodeId, reference.SequenceNumber,
            reference.Scope, reference.PolicyVersion, reference.StartedAtUtc,
            reference.EndedAtUtc, reference.FirstSnapshot, reference.LastSnapshot,
            reference.Quality, [evidence]);
    }

    private static bool Equal(string? left, string? right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
