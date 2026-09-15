namespace PlayStead.Core.Sessions.Discovery;

public sealed record ExecutableRevisionResult
{
    public ExecutableRevisionResult(FileRevision? revision, InventoryIssue? issue)
    {
        if ((revision is null) == (issue is null))
            throw new ArgumentException("Exactly one of revision and issue is required.");

        Revision = revision;
        Issue = issue;
    }

    public FileRevision? Revision { get; }
    public InventoryIssue? Issue { get; }
}
