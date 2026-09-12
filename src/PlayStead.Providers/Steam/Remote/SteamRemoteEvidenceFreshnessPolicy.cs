using PlayStead.Core.Steam;

namespace PlayStead.Providers.Steam.Remote;

public sealed class SteamRemoteEvidenceFreshnessPolicy
{
    public static readonly TimeSpan TimeToLive =
        TimeSpan.FromHours(6);

    public bool IsFresh(
        SteamRemoteEvidence evidence,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        var age =
            nowUtc - evidence.ObservedAtUtc;

        return age < TimeToLive;
    }
}
