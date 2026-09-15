namespace PlayStead.Core.Sessions.Discovery;

public sealed record ProcessSignatureLearningState
{
    public ProcessSignatureLearningState(ExecutableInventory inventory, int policyVersion,
        Guid concurrencyToken, long lastSequenceNumber, bool hasAmbiguousInstallation,
        LearningEpisodeSummary? reference, LearningEpisodeSummary? confirmation,
        IReadOnlyList<DiscoveryReason> reasons)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(reasons);
        if (policyVersion <= 0) throw new ArgumentOutOfRangeException(nameof(policyVersion));
        if (concurrencyToken == Guid.Empty) throw new ArgumentException("Token is required.", nameof(concurrencyToken));
        if (lastSequenceNumber < 0) throw new ArgumentOutOfRangeException(nameof(lastSequenceNumber));
        if (confirmation is not null && reference is null)
            throw new ArgumentException("Confirmation requires a reference.", nameof(confirmation));
        Inventory = inventory;
        PolicyVersion = policyVersion;
        ConcurrencyToken = concurrencyToken;
        LastSequenceNumber = lastSequenceNumber;
        HasAmbiguousInstallation = hasAmbiguousInstallation;
        Reference = reference;
        Confirmation = confirmation;
        Reasons = Array.AsReadOnly(reasons.ToArray());
    }

    public ExecutableInventory Inventory { get; }
    public int PolicyVersion { get; }
    public Guid ConcurrencyToken { get; }
    public long LastSequenceNumber { get; }
    public bool HasAmbiguousInstallation { get; }
    public LearningEpisodeSummary? Reference { get; }
    public LearningEpisodeSummary? Confirmation { get; }
    public IReadOnlyList<DiscoveryReason> Reasons { get; }
}
