namespace PlayStead.Core.Sessions.Discovery;

public enum InventoryIssueKind
{
    MissingRoot,
    InvalidRoot,
    AccessDenied,
    IoFailure,
    ReparsePoint,
    EscapedRoot,
    RevisionChanged
}

public sealed record InventoryIssue
{
    public InventoryIssue(string path, InventoryIssueKind kind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!Enum.IsDefined(kind))
            throw new ArgumentOutOfRangeException(nameof(kind));

        Path = path;
        Kind = kind;
    }

    public string Path { get; }
    public InventoryIssueKind Kind { get; }
}
