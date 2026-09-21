using PlayStead.Core.Sessions;

namespace PlayStead.Core.Sessions.Discovery;

public sealed class ProcessSignatureDiscoveryPolicy
{
    public const int CurrentPolicyVersion = 3;

    public DiscoveryDecision Evaluate(DiscoveryEvaluation evaluation)
    {
        ArgumentNullException.ThrowIfNull(evaluation);

        if (evaluation.ExistingSignatureOrigin is
            ProcessSignatureOrigin.Manual or ProcessSignatureOrigin.BuiltIn)
        {
            return new DiscoveryDecision(DiscoveryDecisionKind.InsufficientEvidence,
                null, [DiscoveryReason.ProtectedSignature]);
        }

        if (evaluation.HasAmbiguousInstallation)
            return new DiscoveryDecision(DiscoveryDecisionKind.Ambiguous,
                null, [DiscoveryReason.AmbiguousInstallation]);

        if (!evaluation.Inventory.Scope.IsPresent)
            return new DiscoveryDecision(DiscoveryDecisionKind.InsufficientEvidence,
                null, [DiscoveryReason.InstallationAbsent]);

        if (evaluation.Inventory.Completeness == InventoryCompleteness.Incomplete)
            return new DiscoveryDecision(DiscoveryDecisionKind.InsufficientEvidence,
                null, [DiscoveryReason.IncompleteInventory]);

        var unrealFamily = UnrealExecutableFamily.TryCreate(evaluation.Inventory);
        evaluation = ExecutableSupportClassifier.Project(evaluation);
        if (unrealFamily is not null)
            evaluation = unrealFamily.Project(evaluation);

        if (evaluation.Inventory.Candidates.Count == 0)
            return new DiscoveryDecision(DiscoveryDecisionKind.InsufficientEvidence,
                null, [DiscoveryReason.NoCandidates]);

        if (evaluation.Episodes.Count == 0)
        {
            return new DiscoveryDecision(DiscoveryDecisionKind.InsufficientEvidence,
                null, [DiscoveryReason.AwaitingIndependentEpisode]);
        }

        var episodeIds = new HashSet<Guid>();
        LearningEpisodeSummary? previous = null;
        foreach (var episode in evaluation.Episodes)
        {
            if (!episodeIds.Add(episode.EpisodeId))
                return Insufficient([DiscoveryReason.DuplicateEpisode]);
            if (previous is not null)
            {
                if (episode.SequenceNumber <= previous.SequenceNumber ||
                    episode.SequenceNumber - previous.SequenceNumber != 1)
                    return Insufficient([DiscoveryReason.CaptureGap]);
                if (episode.StartedAtUtc <= previous.EndedAtUtc)
                    return Insufficient([DiscoveryReason.OverlappingEpisodes]);
            }
            previous = episode;
        }

        ExecutableCandidate? reference = null;
        DiscoveryDecision result = new(DiscoveryDecisionKind.InsufficientEvidence,
            null, [DiscoveryReason.AwaitingIndependentEpisode]);
        foreach (var episode in evaluation.Episodes)
        {
            var scopeRefusal = RefuseScope(episode, evaluation.Inventory.Scope);
            if (scopeRefusal is not null)
            {
                reference = null;
                result = scopeRefusal;
                continue;
            }
            var qualityRefusal = RefuseQuality(episode, evaluation.Inventory);
            if (qualityRefusal is not null)
            {
                reference = null;
                result = qualityRefusal;
                continue;
            }

            if (evaluation.Inventory.Candidates.Any(candidate =>
                    FindEvidence(episode, candidate)?.PresenceRanges.Count is null or 0))
            {
                reference = null;
                result = new DiscoveryDecision(DiscoveryDecisionKind.Ambiguous,
                    null, [DiscoveryReason.UnobservedCompetitor]);
                continue;
            }

            var qualified = evaluation.Inventory.Candidates
                .Where(candidate => CanBeMain(candidate, episode, evaluation.Inventory.Candidates))
                .ToArray();
            if (qualified.Length != 1)
            {
                reference = null;
                result = Ambiguous(ClassifyCompetitors(episode));
                continue;
            }

            var current = qualified[0];
            if (reference is not null &&
                (!string.Equals(reference.ExecutablePath, current.ExecutablePath,
                    StringComparison.OrdinalIgnoreCase) || reference.Revision != current.Revision))
            {
                reference = null;
                result = Ambiguous(DiscoveryReason.ConflictingEpisodes);
                continue;
            }
            result = reference is not null
                ? new DiscoveryDecision(DiscoveryDecisionKind.PromoteMain,
                    current, [DiscoveryReason.RepeatedQualifiedEpisodes])
                : Insufficient([DiscoveryReason.AwaitingIndependentEpisode]);
            reference = current;
        }

        return result;
    }

    private static DiscoveryDecision? RefuseScope(LearningEpisodeSummary episode,
        InstallationScope scope)
    {
        if (episode.Scope.GameId != scope.GameId ||
            episode.Scope.InstallationId != scope.InstallationId ||
            !string.Equals(episode.Scope.RootPath, scope.RootPath,
                StringComparison.OrdinalIgnoreCase) || episode.Scope.IsPresent != scope.IsPresent)
            return Insufficient([DiscoveryReason.ScopeChanged]);
        if (episode.Scope.GenerationId != scope.GenerationId)
            return Insufficient([DiscoveryReason.GenerationChanged]);
        if (episode.PolicyVersion != CurrentPolicyVersion)
            return Insufficient([DiscoveryReason.PolicyVersionChanged]);
        return null;
    }

    private static DiscoveryDecision? RefuseQuality(LearningEpisodeSummary episode,
        ExecutableInventory inventory)
    {
        var reasons = new List<DiscoveryReason>();
        if (episode.Quality.HasFlag(EpisodeQuality.Partial))
            reasons.Add(DiscoveryReason.PartialEpisode);
        if (episode.Quality.HasFlag(EpisodeQuality.CaptureGap))
            reasons.Add(DiscoveryReason.CaptureGap);
        if (episode.Quality.HasFlag(EpisodeQuality.UnknownProcessIdentity))
            reasons.Add(DiscoveryReason.UnknownProcessIdentity);
        if (reasons.Count != 0)
            return Insufficient(reasons);

        foreach (var evidence in episode.Candidates)
        {
            var candidate = inventory.Candidates.FirstOrDefault(item =>
                string.Equals(item.ExecutablePath, evidence.ExecutablePath,
                    StringComparison.OrdinalIgnoreCase));
            if (candidate is null || !evidence.HasReliablePath)
                return Insufficient([DiscoveryReason.UnreliablePath]);
            if (!evidence.HasReliableIdentity)
                return Insufficient([DiscoveryReason.UnknownProcessIdentity]);
            if (evidence.Revision is null || evidence.Revision != candidate.Revision)
                return Insufficient([DiscoveryReason.RevisionChanged]);
        }

        var ranges = episode.Candidates.SelectMany(candidate => candidate.PresenceRanges)
            .OrderBy(range => range.First).ToArray();
        if (ranges.Length == 0)
            return Insufficient([DiscoveryReason.PartialEpisode]);
        var firstPositive = ranges[0].First;
        var lastPositive = ranges.Max(range => range.Last);
        if (firstPositive - episode.FirstSnapshot < 2 ||
            episode.LastSnapshot - lastPositive < 2)
            return Insufficient([DiscoveryReason.PartialEpisode]);
        var coveredUntil = ranges[0].Last;
        foreach (var range in ranges.Skip(1))
        {
            if (range.First > coveredUntil && range.First - coveredUntil > 2)
                return Insufficient([DiscoveryReason.PartialEpisode]);
            coveredUntil = Math.Max(coveredUntil, range.Last);
        }

        if (inventory.Candidates.Count == 1 &&
            !episode.Candidates[0].PresenceRanges.Any(range => range.Last - range.First >= 1))
            return Insufficient([DiscoveryReason.InsufficientPresence]);
        return null;
    }

    private static DiscoveryDecision Insufficient(IReadOnlyList<DiscoveryReason> reasons) =>
        new(DiscoveryDecisionKind.InsufficientEvidence, null,
            reasons.Distinct().Order().ToArray());

    private static DiscoveryDecision Ambiguous(DiscoveryReason reason) =>
        new(DiscoveryDecisionKind.Ambiguous, null, [reason]);

    private static DiscoveryReason ClassifyCompetitors(LearningEpisodeSummary episode)
    {
        var positive = episode.Candidates.Where(evidence => evidence.PresenceRanges.Count != 0)
            .ToArray();
        var lastPositive = positive.Max(evidence => evidence.PresenceRanges[^1].Last);
        if (positive.Count(evidence => evidence.PresenceRanges[^1].Last == lastPositive) > 1)
            return DiscoveryReason.EquivalentCandidates;
        if (positive.Any(evidence => evidence.PresenceRanges.Count > 1 &&
            evidence.PresenceRanges[^1].Last < lastPositive))
            return DiscoveryReason.ReappearingCompetitor;
        var survivor = positive.Single(evidence => evidence.PresenceRanges[^1].Last == lastPositive);
        if (positive.Any(evidence => evidence != survivor &&
            evidence.PresenceRanges[0].First > survivor.PresenceRanges[0].First))
            return DiscoveryReason.LateCompetitor;
        return DiscoveryReason.EquivalentCandidates;
    }

    private static bool CanBeMain(ExecutableCandidate main, LearningEpisodeSummary episode,
        IReadOnlyList<ExecutableCandidate> candidates)
    {
        var mainEvidence = FindEvidence(episode, main);
        if (mainEvidence is null || !mainEvidence.PresenceRanges.Any(range => range.Last - range.First >= 1))
            return false;

        var positiveRanges = episode.Candidates.SelectMany(evidence => evidence.PresenceRanges)
            .OrderBy(range => range.First).ToArray();
        if (positiveRanges.Length == 0 || positiveRanges[0].First - episode.FirstSnapshot < 2)
            return false;
        var lastPositive = positiveRanges.Max(range => range.Last);
        if (episode.LastSnapshot - lastPositive < 2 ||
            mainEvidence.PresenceRanges[^1].Last != lastPositive)
            return false;

        var coveredUntil = positiveRanges[0].Last;
        foreach (var range in positiveRanges.Skip(1))
        {
            if (range.First > coveredUntil && range.First - coveredUntil > 2)
                return false;
            coveredUntil = Math.Max(coveredUntil, range.Last);
        }

        var mainFirst = mainEvidence.PresenceRanges[0].First;
        var mainLast = mainEvidence.PresenceRanges[^1].Last;
        foreach (var other in candidates)
        {
            if (string.Equals(other.ExecutablePath, main.ExecutablePath, StringComparison.OrdinalIgnoreCase))
                continue;
            var evidence = FindEvidence(episode, other);
            if (evidence?.PresenceRanges.Count != 1)
                return false;
            var range = evidence.PresenceRanges[0];
            if (range.First > mainFirst || range.Last >= mainLast ||
                !mainEvidence.PresenceRanges.Any(mainRange =>
                    mainRange.First > range.Last && mainRange.Last - mainRange.First >= 1 ||
                    mainRange.First <= range.Last && mainRange.Last - range.Last >= 2))
                return false;
        }
        return true;
    }

    private static CandidateEpisodeEvidence? FindEvidence(LearningEpisodeSummary episode,
        ExecutableCandidate candidate) => episode.Candidates.FirstOrDefault(evidence =>
        string.Equals(evidence.ExecutablePath, candidate.ExecutablePath,
            StringComparison.OrdinalIgnoreCase));
}
