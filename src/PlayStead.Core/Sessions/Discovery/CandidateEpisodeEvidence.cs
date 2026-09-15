namespace PlayStead.Core.Sessions.Discovery;

public sealed record CandidateEpisodeEvidence
{
    public CandidateEpisodeEvidence(
        string executablePath,
        FileRevision? revision,
        bool hasReliablePath,
        bool hasReliableIdentity,
        IReadOnlyList<SnapshotRange> presenceRanges)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentNullException.ThrowIfNull(presenceRanges);

        var rangeCopy = presenceRanges.ToArray();
        if (rangeCopy.Any(range => range is null))
            throw new ArgumentException("Presence ranges must not contain null.", nameof(presenceRanges));
        for (var index = 1; index < rangeCopy.Length; index++)
        {
            if (rangeCopy[index].First - rangeCopy[index - 1].Last <= 1)
                throw new ArgumentException("Presence ranges must be ordered, disjoint and nonadjacent.", nameof(presenceRanges));
        }

        ExecutablePath = executablePath;
        Revision = revision;
        HasReliablePath = hasReliablePath;
        HasReliableIdentity = hasReliableIdentity;
        PresenceRanges = Array.AsReadOnly(rangeCopy);
    }

    public string ExecutablePath { get; }
    public FileRevision? Revision { get; }
    public bool HasReliablePath { get; }
    public bool HasReliableIdentity { get; }
    public IReadOnlyList<SnapshotRange> PresenceRanges { get; }
}
