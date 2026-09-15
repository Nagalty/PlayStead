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
    public ProcessSignatureAcceptanceService(
        IProcessSignatureLearningStore learningStore,
        IProcessSignatureStore signatureStore,
        IProcessSignatureDiscoveryStore discoveryStore,
        IExecutableRevisionSource revisionSource,
        ProcessSignatureDiscoveryPolicy policy,
        Func<InstallationId, DiscoveryInventoryContext?> currentInventory,
        TimeProvider timeProvider)
    {
        _learningStore = learningStore ?? throw new ArgumentNullException(nameof(learningStore));
        _signatureStore = signatureStore ?? throw new ArgumentNullException(nameof(signatureStore));
        _discoveryStore = discoveryStore ?? throw new ArgumentNullException(nameof(discoveryStore));
        _revisionSource = revisionSource ?? throw new ArgumentNullException(nameof(revisionSource));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _currentInventory = currentInventory ?? throw new ArgumentNullException(nameof(currentInventory));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<bool> TryAcceptAsync(InstallationId installationId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var state = await _learningStore.LoadAsync(installationId, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (state?.Reference is null || state.Confirmation is null) return false;
        var current = _currentInventory(installationId);
        if (current is null || current.HasAmbiguousInstallation != state.HasAmbiguousInstallation ||
            !InventoryEquals(current.Inventory, state.Inventory) ||
            state.PolicyVersion != ProcessSignatureDiscoveryPolicy.CurrentPolicyVersion) return false;

        var stored = await _signatureStore.GetAsync(state.Inventory.Scope.GameId.Value, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (stored is not null && (stored.Origin != ProcessSignatureOrigin.Discovered ||
            stored.Discovery?.ValidationState != ProcessSignatureValidationState.NeedsRevalidation)) return false;
        var expected = stored?.Discovery is { } metadata
            ? new DiscoveredSignatureExpectation(metadata.ConcurrencyToken, metadata.GenerationId,
                metadata.ValidationState, metadata.InstallationId) : null;
        var decision = _policy.Evaluate(new DiscoveryEvaluation(state.Inventory,
            [state.Reference, state.Confirmation], state.HasAmbiguousInstallation, stored?.Origin));
        if (decision.Kind != DiscoveryDecisionKind.PromoteMain) return false;

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
                    null, null, [DiscoveryReason.RevisionChanged]);
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

        var main = decision.Main ?? throw new InvalidOperationException("Promotion requires a main candidate.");
        var accepted = new ProcessSignature(state.Inventory.Scope.GameId.Value,
            [new ProcessSignatureEntry(main.ExecutableName, ProcessSignatureEntryKind.Main,
                main.ExecutablePath, main.Revision)], ProcessSignatureOrigin.Discovered, _timeProvider.GetUtcNow(),
            new DiscoveredSignatureMetadata(state.Inventory.Scope.InstallationId, state.Inventory.Scope.GenerationId,
                state.PolicyVersion, ProcessSignatureValidationState.Valid, Guid.NewGuid()));
        var write = new DiscoveredSignatureWrite(accepted, state.ConcurrencyToken,
            state.Reference.EpisodeId, state.Confirmation.EpisodeId);
        return expected is null
            ? await _discoveryStore.TryInsertDiscoveredIfAbsentAsync(write, cancellationToken)
            : await _discoveryStore.TryRevalidateDiscoveredAsync(write, expected, cancellationToken);
    }

    private static bool InventoryEquals(ExecutableInventory left, ExecutableInventory right) =>
        left.Scope.GameId == right.Scope.GameId && left.Scope.InstallationId == right.Scope.InstallationId &&
        left.Scope.GenerationId == right.Scope.GenerationId && left.Scope.IsPresent == right.Scope.IsPresent &&
        Equal(left.Scope.RootPath, right.Scope.RootPath) && left.Completeness == right.Completeness &&
        left.Candidates.Count == right.Candidates.Count && left.Candidates.Zip(right.Candidates).All(pair =>
            Equal(pair.First.ExecutablePath, pair.Second.ExecutablePath) && Equal(pair.First.ExecutableName, pair.Second.ExecutableName) &&
            pair.First.Revision == pair.Second.Revision) && left.Issues.Count == right.Issues.Count &&
        left.Issues.Zip(right.Issues).All(pair => pair.First.Kind == pair.Second.Kind && Equal(pair.First.Path, pair.Second.Path));

    private static bool Equal(string? left, string? right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
