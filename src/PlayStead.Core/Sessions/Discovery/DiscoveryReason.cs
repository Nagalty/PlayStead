namespace PlayStead.Core.Sessions.Discovery;

public enum DiscoveryReason
{
    RepeatedQualifiedEpisodes,
    NoCandidates,
    InstallationAbsent,
    IncompleteInventory,
    UnreliablePath,
    PartialEpisode,
    CaptureGap,
    UnknownProcessIdentity,
    UnobservedCompetitor,
    EquivalentCandidates,
    LateCompetitor,
    ReappearingCompetitor,
    ConflictingEpisodes,
    AmbiguousInstallation,
    RevisionChanged,
    ProtectedSignature,
    AwaitingIndependentEpisode,
    ScopeChanged,
    GenerationChanged,
    PolicyVersionChanged,
    DuplicateEpisode,
    OverlappingEpisodes,
    InsufficientPresence
}
