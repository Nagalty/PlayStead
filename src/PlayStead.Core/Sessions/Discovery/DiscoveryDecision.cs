namespace PlayStead.Core.Sessions.Discovery;

public enum DiscoveryDecisionKind
{
    PromoteMain,
    InsufficientEvidence,
    Ambiguous
}

public sealed record DiscoveryDecision
{
    public DiscoveryDecision(
        DiscoveryDecisionKind kind,
        ExecutableCandidate? main,
        IReadOnlyList<DiscoveryReason> reasons)
    {
        if (!Enum.IsDefined(kind))
            throw new ArgumentOutOfRangeException(nameof(kind));
        ArgumentNullException.ThrowIfNull(reasons);

        var reasonCopy = reasons.ToArray();
        if (reasonCopy.Length == 0)
            throw new ArgumentException("A decision must have a reason.", nameof(reasons));
        if (reasonCopy.Any(reason => !Enum.IsDefined(reason)))
            throw new ArgumentOutOfRangeException(nameof(reasons));
        if (reasonCopy.Distinct().Count() != reasonCopy.Length)
            throw new ArgumentException("Decision reasons must be distinct.", nameof(reasons));
        if (kind == DiscoveryDecisionKind.PromoteMain && main is null ||
            kind != DiscoveryDecisionKind.PromoteMain && main is not null)
            throw new ArgumentException("Only a promotion may carry a main candidate.", nameof(main));

        Kind = kind;
        Main = main;
        Reasons = Array.AsReadOnly(reasonCopy);
    }

    public DiscoveryDecisionKind Kind { get; }
    public ExecutableCandidate? Main { get; }
    public IReadOnlyList<DiscoveryReason> Reasons { get; }
}
