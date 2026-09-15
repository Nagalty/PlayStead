namespace PlayStead.Core.Sessions.Discovery;

public sealed record FileRevision
{
    public FileRevision(long sizeBytes, DateTimeOffset lastWriteTimeUtc)
    {
        if (sizeBytes < 0)
            throw new ArgumentOutOfRangeException(nameof(sizeBytes));

        SizeBytes = sizeBytes;
        LastWriteTimeUtc = lastWriteTimeUtc.ToUniversalTime();
    }

    public long SizeBytes { get; }
    public DateTimeOffset LastWriteTimeUtc { get; }
}
