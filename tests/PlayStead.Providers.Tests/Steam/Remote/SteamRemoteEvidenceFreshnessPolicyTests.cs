using PlayStead.Core.Steam;
using PlayStead.Providers.Steam.Remote;

namespace PlayStead.Providers.Tests.Steam.Remote;

public sealed class SteamRemoteEvidenceFreshnessPolicyTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 12, 20, 0, 0, TimeSpan.Zero);

    private readonly SteamRemoteEvidenceFreshnessPolicy _sut = new();

    [Fact]
    public void Evidence_younger_than_six_hours_is_fresh()
    {
        var evidence = Remote(
            Now - TimeSpan.FromHours(6) + TimeSpan.FromTicks(1));

        Assert.True(
            _sut.IsFresh(evidence, Now));
    }

    [Fact]
    public void Evidence_exactly_six_hours_old_is_stale()
    {
        var evidence = Remote(
            Now - TimeSpan.FromHours(6));

        Assert.False(
            _sut.IsFresh(evidence, Now));
    }

    [Fact]
    public void Evidence_older_than_six_hours_is_stale()
    {
        var evidence = Remote(
            Now - TimeSpan.FromHours(7));

        Assert.False(
            _sut.IsFresh(evidence, Now));
    }

    [Fact]
    public void Future_observation_from_small_clock_skew_is_treated_as_fresh()
    {
        var evidence = Remote(
            Now + TimeSpan.FromMinutes(2));

        Assert.True(
            _sut.IsFresh(evidence, Now));
    }

    private static SteamRemoteEvidence Remote(
        DateTimeOffset observedAtUtc)
        => new(
            "730",
            "public",
            "101",
            new Dictionary<string, string>
            {
                ["731"] = "111"
            },
            observedAtUtc,
            SteamRemoteEvidenceSource.SteamCmdAnonymous);
}
