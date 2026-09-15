using PlayStead.Core.Library;

namespace PlayStead.Core.Sessions.Discovery;

public sealed class DiscoveredSignatureValidator : IDiscoveredSignatureValidator
{
    private readonly IProcessSignatureStore _signatureStore;
    private readonly IProcessSignatureLearningStore _learningStore;
    private readonly IProcessSignatureDiscoveryStore _discoveryStore;
    private readonly IExecutableRevisionSource _revisionSource;
    private readonly Func<InstallationId, DiscoveryInventoryContext?> _currentInventory;

    public DiscoveredSignatureValidator(
        IProcessSignatureStore signatureStore,
        IProcessSignatureLearningStore learningStore,
        IProcessSignatureDiscoveryStore discoveryStore,
        IExecutableRevisionSource revisionSource,
        Func<InstallationId, DiscoveryInventoryContext?> currentInventory)
    {
        ArgumentNullException.ThrowIfNull(signatureStore);
        ArgumentNullException.ThrowIfNull(learningStore);
        ArgumentNullException.ThrowIfNull(discoveryStore);
        ArgumentNullException.ThrowIfNull(revisionSource);
        ArgumentNullException.ThrowIfNull(currentInventory);
        _signatureStore = signatureStore;
        _learningStore = learningStore;
        _discoveryStore = discoveryStore;
        _revisionSource = revisionSource;
        _currentInventory = currentInventory;
    }

    public async Task<DiscoveredSignatureValidationResult> ValidateAsync(
        ProcessSignature signature, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(signature);
        cancellationToken.ThrowIfCancellationRequested();
        if (!ProcessSignatureMatcher.IsDiscoveredAdmissible(signature))
            return DiscoveredSignatureValidationResult.Invalid;

        var metadata = signature.Discovery!;
        var installationId = metadata.InstallationId!.Value;
        var current = _currentInventory(installationId);
        if (current is null) return DiscoveredSignatureValidationResult.Pending;

        var expected = new DiscoveredSignatureExpectation(metadata.ConcurrencyToken,
            metadata.GenerationId, metadata.ValidationState, metadata.InstallationId);
        var stored = await _signatureStore.GetAsync(signature.GameId, cancellationToken);
        var learning = await _learningStore.LoadAsync(installationId, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (!SignatureEquals(signature, stored) || !MatchesInventory(signature, learning, current))
            return await InvalidateAsync(signature.GameId, expected, cancellationToken);

        foreach (var entry in signature.Entries)
        {
            var revision = await _revisionSource.ReadAsync(current.Inventory.Scope,
                entry.ExecutablePath!, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (revision?.Revision is null || revision.Revision != entry.ValidatedRevision)
                return await InvalidateAsync(signature.GameId, expected, cancellationToken);
        }

        // Stores reconstruct DTOs on each load; collection identity is not authority.
        stored = await _signatureStore.GetAsync(signature.GameId, cancellationToken);
        var reloadedLearning = await _learningStore.LoadAsync(installationId, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (!SignatureEquals(signature, stored) || reloadedLearning?.ConcurrencyToken != learning!.ConcurrencyToken ||
            !MatchesInventory(signature, reloadedLearning, current))
            return await InvalidateAsync(signature.GameId, expected, cancellationToken);

        var latestInventory = _currentInventory(installationId);
        if (latestInventory is null) return DiscoveredSignatureValidationResult.Pending;
        if (!MatchesInventory(signature, reloadedLearning, latestInventory))
            return await InvalidateAsync(signature.GameId, expected, cancellationToken);

        return DiscoveredSignatureValidationResult.Valid;
    }

    private async Task<DiscoveredSignatureValidationResult> InvalidateAsync(Guid gameId,
        DiscoveredSignatureExpectation expected, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _discoveryStore.TryInvalidateDiscoveredAsync(gameId, expected, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return DiscoveredSignatureValidationResult.Invalid;
    }

    private static bool MatchesInventory(ProcessSignature signature, ProcessSignatureLearningState? learning,
        DiscoveryInventoryContext current)
    {
        if (learning is null || current.HasAmbiguousInstallation || learning.HasAmbiguousInstallation ||
            learning.PolicyVersion != signature.Discovery!.PolicyVersion ||
            current.Inventory.Completeness != InventoryCompleteness.Complete || !current.Inventory.Scope.IsPresent ||
            current.Inventory.Scope.GameId.Value != signature.GameId ||
            current.Inventory.Scope.InstallationId != signature.Discovery.InstallationId ||
            current.Inventory.Scope.GenerationId != signature.Discovery.GenerationId ||
            !InventoryEquals(current.Inventory, learning.Inventory)) return false;

        var rootPrefix = current.Inventory.Scope.RootPath.TrimEnd('\\') + '\\';
        return signature.Entries.All(entry => entry.ExecutablePath!.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase) &&
            current.Inventory.Candidates.Any(candidate =>
            Equal(candidate.ExecutablePath, entry.ExecutablePath) && Equal(candidate.ExecutableName, entry.ExecutableName) &&
            candidate.Revision == entry.ValidatedRevision));
    }

    private static bool SignatureEquals(ProcessSignature expected, ProcessSignature? actual) =>
        actual is not null && actual.GameId == expected.GameId && actual.Origin == expected.Origin &&
        actual.UpdatedAtUtc == expected.UpdatedAtUtc && actual.Discovery == expected.Discovery &&
        actual.Entries.Count == expected.Entries.Count && actual.Entries.Zip(expected.Entries).All(pair =>
            pair.First.Kind == pair.Second.Kind && Equal(pair.First.ExecutableName, pair.Second.ExecutableName) &&
            Equal(pair.First.ExecutablePath, pair.Second.ExecutablePath) && pair.First.ValidatedRevision == pair.Second.ValidatedRevision);

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
