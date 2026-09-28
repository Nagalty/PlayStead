using PlayStead.Core.Library;

namespace PlayStead.Core.ProviderActivity;

public enum ProviderActivityAvailability
{
    Unknown = 0,
    Complete = 1,
    Partial = 2
}

public sealed record ProviderActivityMetadata(
    GameId GameId,
    ProviderKind Provider,
    string ProviderGameId,
    TimeSpan? TotalPlaytime,
    DateTimeOffset? LastPlayedAtUtc,
    DateTimeOffset RefreshedAtUtc,
    ProviderActivityAvailability Availability)
{
    public ProviderKind Source => Provider;

    public DateTimeOffset ObservedAtUtc => RefreshedAtUtc;

    public static ProviderActivityMetadata Unknown(
        GameId gameId,
        ProviderKind provider,
        string providerGameId,
        DateTimeOffset refreshedAtUtc) =>
        new(gameId, provider, providerGameId, null, null, refreshedAtUtc, ProviderActivityAvailability.Unknown);
}
