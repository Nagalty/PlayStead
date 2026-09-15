namespace PlayStead.Core.Sessions.Discovery;

public sealed record SnapshotRange
{
    public SnapshotRange(long first, long last)
    {
        if (first < 0)
            throw new ArgumentOutOfRangeException(nameof(first));
        if (last < first)
            throw new ArgumentOutOfRangeException(nameof(last));

        First = first;
        Last = last;
    }

    public long First { get; }
    public long Last { get; }
}
