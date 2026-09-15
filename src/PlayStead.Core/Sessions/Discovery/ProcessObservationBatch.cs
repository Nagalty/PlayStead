using PlayStead.Core.Sessions;

namespace PlayStead.Core.Sessions.Discovery;

public sealed record ProcessObservationBatch
{
    public ProcessObservationBatch(long sequenceNumber, DateTimeOffset observedAtUtc,
        EpisodeQuality quality, IReadOnlyList<ProcessSnapshot> processes)
    {
        if (sequenceNumber < 0) throw new ArgumentOutOfRangeException(nameof(sequenceNumber));
        if ((quality & ~(EpisodeQuality.Partial | EpisodeQuality.CaptureGap | EpisodeQuality.UnknownProcessIdentity)) != 0)
            throw new ArgumentOutOfRangeException(nameof(quality));
        ArgumentNullException.ThrowIfNull(processes);
        if (processes.Any(process => process is null))
            throw new ArgumentException("Processes cannot contain null.", nameof(processes));
        SequenceNumber = sequenceNumber;
        ObservedAtUtc = observedAtUtc.ToUniversalTime();
        Quality = quality;
        Processes = Array.AsReadOnly(processes.ToArray());
    }

    public long SequenceNumber { get; }
    public DateTimeOffset ObservedAtUtc { get; }
    public EpisodeQuality Quality { get; }
    public IReadOnlyList<ProcessSnapshot> Processes { get; }
}
