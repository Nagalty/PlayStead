namespace PlayStead.Core.Sessions.Discovery;

[Flags]
public enum EpisodeQuality
{
    Complete = 0,
    Partial = 1,
    CaptureGap = 2,
    UnknownProcessIdentity = 4
}

public sealed record LearningEpisodeSummary
{
    public LearningEpisodeSummary(
        Guid episodeId,
        long sequenceNumber,
        InstallationScope scope,
        int policyVersion,
        DateTimeOffset startedAtUtc,
        DateTimeOffset endedAtUtc,
        long firstSnapshot,
        long lastSnapshot,
        EpisodeQuality quality,
        IReadOnlyList<CandidateEpisodeEvidence> candidates)
    {
        if (episodeId == Guid.Empty)
            throw new ArgumentException("Episode identity must not be empty.", nameof(episodeId));
        if (sequenceNumber <= 0)
            throw new ArgumentOutOfRangeException(nameof(sequenceNumber));
        ArgumentNullException.ThrowIfNull(scope);
        if (policyVersion <= 0)
            throw new ArgumentOutOfRangeException(nameof(policyVersion));
        if (startedAtUtc > endedAtUtc)
            throw new ArgumentException("Episode start must precede its end.", nameof(endedAtUtc));
        if (firstSnapshot < 0)
            throw new ArgumentOutOfRangeException(nameof(firstSnapshot));
        if (lastSnapshot < firstSnapshot)
            throw new ArgumentOutOfRangeException(nameof(lastSnapshot));
        if ((quality & ~(EpisodeQuality.Partial | EpisodeQuality.CaptureGap | EpisodeQuality.UnknownProcessIdentity)) != 0)
            throw new ArgumentOutOfRangeException(nameof(quality));
        ArgumentNullException.ThrowIfNull(candidates);

        var candidateCopy = candidates.ToArray();
        if (candidateCopy.Any(candidate => candidate is null))
            throw new ArgumentException("Candidates must not contain null.", nameof(candidates));
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in candidateCopy)
        {
            if (!paths.Add(candidate.ExecutablePath))
                throw new ArgumentException("Candidate paths must be unique.", nameof(candidates));
            if (candidate.PresenceRanges.Any(range => range.First < firstSnapshot || range.Last > lastSnapshot))
                throw new ArgumentException("Presence ranges must stay within episode snapshots.", nameof(candidates));
        }

        EpisodeId = episodeId;
        SequenceNumber = sequenceNumber;
        Scope = scope;
        PolicyVersion = policyVersion;
        StartedAtUtc = startedAtUtc.ToUniversalTime();
        EndedAtUtc = endedAtUtc.ToUniversalTime();
        FirstSnapshot = firstSnapshot;
        LastSnapshot = lastSnapshot;
        Quality = quality;
        Candidates = Array.AsReadOnly(candidateCopy);
    }

    public Guid EpisodeId { get; }
    public long SequenceNumber { get; }
    public InstallationScope Scope { get; }
    public int PolicyVersion { get; }
    public DateTimeOffset StartedAtUtc { get; }
    public DateTimeOffset EndedAtUtc { get; }
    public long FirstSnapshot { get; }
    public long LastSnapshot { get; }
    public EpisodeQuality Quality { get; }
    public IReadOnlyList<CandidateEpisodeEvidence> Candidates { get; }
}
