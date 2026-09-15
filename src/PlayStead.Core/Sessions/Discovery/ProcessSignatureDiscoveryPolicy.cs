using PlayStead.Core.Sessions;

namespace PlayStead.Core.Sessions.Discovery;

public sealed class ProcessSignatureDiscoveryPolicy
{
    public const int CurrentPolicyVersion = 1;

    public DiscoveryDecision Evaluate(DiscoveryEvaluation evaluation)
    {
        ArgumentNullException.ThrowIfNull(evaluation);

        if (evaluation.ExistingSignatureOrigin is
            ProcessSignatureOrigin.Manual or ProcessSignatureOrigin.BuiltIn)
        {
            return new DiscoveryDecision(DiscoveryDecisionKind.InsufficientEvidence,
                null, [DiscoveryReason.ProtectedSignature]);
        }

        if (evaluation.Episodes.Count == 0)
        {
            return new DiscoveryDecision(DiscoveryDecisionKind.InsufficientEvidence,
                null, [DiscoveryReason.AwaitingIndependentEpisode]);
        }

        if (evaluation.Inventory.Completeness == InventoryCompleteness.Incomplete)
        {
            return new DiscoveryDecision(DiscoveryDecisionKind.InsufficientEvidence,
                null, [DiscoveryReason.IncompleteInventory]);
        }

        foreach (var episode in evaluation.Episodes)
        {
            foreach (var candidate in evaluation.Inventory.Candidates)
            {
                if (!episode.Candidates.Any(evidence =>
                        string.Equals(evidence.ExecutablePath, candidate.ExecutablePath,
                            StringComparison.OrdinalIgnoreCase)))
                {
                    return new DiscoveryDecision(DiscoveryDecisionKind.Ambiguous,
                        null, [DiscoveryReason.UnobservedCompetitor]);
                }
            }
        }

        return new DiscoveryDecision(DiscoveryDecisionKind.InsufficientEvidence,
            null, [DiscoveryReason.AwaitingIndependentEpisode]);
    }
}
