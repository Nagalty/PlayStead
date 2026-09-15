namespace PlayStead.Core.Sessions.Discovery;

public enum InventoryCompleteness
{
    Complete,
    Incomplete
}

public sealed record ExecutableInventory
{
    public ExecutableInventory(
        InstallationScope scope,
        InventoryCompleteness completeness,
        IReadOnlyList<ExecutableCandidate> candidates,
        IReadOnlyList<InventoryIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(issues);
        if (!Enum.IsDefined(completeness))
            throw new ArgumentOutOfRangeException(nameof(completeness));

        var candidateCopy = candidates.ToArray();
        var issueCopy = issues.ToArray();
        if (candidateCopy.Any(candidate => candidate is null))
            throw new ArgumentException("Candidates must not contain null.", nameof(candidates));
        if (issueCopy.Any(issue => issue is null))
            throw new ArgumentException("Issues must not contain null.", nameof(issues));
        if (completeness == InventoryCompleteness.Complete && issueCopy.Length != 0 ||
            completeness == InventoryCompleteness.Incomplete && issueCopy.Length == 0)
            throw new ArgumentException("Inventory completeness and issues must agree.", nameof(issues));

        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (candidateCopy.Any(candidate => !paths.Add(candidate.ExecutablePath)))
            throw new ArgumentException("Candidate paths must be unique.", nameof(candidates));

        Scope = scope;
        Completeness = completeness;
        Candidates = Array.AsReadOnly(candidateCopy);
        Issues = Array.AsReadOnly(issueCopy);
    }

    public InstallationScope Scope { get; }
    public InventoryCompleteness Completeness { get; }
    public IReadOnlyList<ExecutableCandidate> Candidates { get; }
    public IReadOnlyList<InventoryIssue> Issues { get; }
}
