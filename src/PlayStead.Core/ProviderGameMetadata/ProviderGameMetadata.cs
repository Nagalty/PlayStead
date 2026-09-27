using PlayStead.Core.Library;

namespace PlayStead.Core.ProviderGameMetadata;

public sealed record ProviderGameMetadata(
    GameId GameId,
    ProviderKind Provider,
    string ProviderGameId,
    DateTimeOffset RefreshedAtUtc,
    IReadOnlyList<string>? Genres,
    IReadOnlyList<string>? Categories,
    IReadOnlyList<string>? Developers,
    IReadOnlyList<string>? Publishers,
    DateOnly? ReleaseDate,
    bool? IsFree,
    bool? SinglePlayer,
    bool? MultiPlayer,
    bool? OnlineCoop,
    bool? LocalCoop,
    ProviderGameMetadataAvailability Availability,
    string? ShortDescription = null,
    int? OnlineCoopMaxPlayers = null,
    int? OnlineMultiplayerMaxPlayers = null,
    int? OfflineCoopMaxPlayers = null,
    int? OfflineMultiplayerMaxPlayers = null)
{
    public bool? SupportsCoop => OnlineCoop is true || LocalCoop is true
        ? true
        : OnlineCoop is false && LocalCoop is false
            ? false
            : null;

    public static ProviderGameMetadata Create(
        GameId gameId,
        ProviderKind provider,
        string providerGameId,
        DateTimeOffset refreshedAtUtc,
        IReadOnlyCollection<string>? genres = null,
        IReadOnlyCollection<string>? categories = null,
        IReadOnlyCollection<string>? developers = null,
        IReadOnlyCollection<string>? publishers = null,
        DateOnly? releaseDate = null,
        bool? isFree = null,
        bool? singlePlayer = null,
        bool? multiPlayer = null,
        bool? onlineCoop = null,
        bool? localCoop = null,
        ProviderGameMetadataAvailability availability = ProviderGameMetadataAvailability.Unknown,
        string? shortDescription = null,
        int? onlineCoopMaxPlayers = null,
        int? onlineMultiplayerMaxPlayers = null,
        int? offlineCoopMaxPlayers = null,
        int? offlineMultiplayerMaxPlayers = null) =>
        new(gameId, provider, providerGameId, refreshedAtUtc,
            Normalize(genres), Normalize(categories), Normalize(developers), Normalize(publishers),
            releaseDate, isFree, singlePlayer, multiPlayer, onlineCoop, localCoop, availability,
            string.IsNullOrWhiteSpace(shortDescription) ? null : shortDescription.Trim(),
            onlineCoopMaxPlayers, onlineMultiplayerMaxPlayers, offlineCoopMaxPlayers, offlineMultiplayerMaxPlayers);

    private static IReadOnlyList<string>? Normalize(IReadOnlyCollection<string>? values) =>
        values is null
            ? null
            : values.Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToArray();
}

public sealed record ProviderGameMetadataPatch(
    GameId GameId,
    ProviderKind Provider,
    string ProviderGameId,
    ProviderField<IReadOnlyList<string>> Genres,
    ProviderField<IReadOnlyList<string>> Categories,
    ProviderField<IReadOnlyList<string>> Developers,
    ProviderField<IReadOnlyList<string>> Publishers,
    ProviderField<DateOnly> ReleaseDate,
    ProviderField<bool> IsFree,
    ProviderField<bool> SinglePlayer,
    ProviderField<bool> MultiPlayer,
    ProviderField<bool> OnlineCoop,
    ProviderField<bool> LocalCoop,
    ProviderGameMetadataAvailability Availability,
    ProviderField<string> ShortDescription = default,
    ProviderField<int> OnlineCoopMaxPlayers = default,
    ProviderField<int> OnlineMultiplayerMaxPlayers = default,
    ProviderField<int> OfflineCoopMaxPlayers = default,
    ProviderField<int> OfflineMultiplayerMaxPlayers = default);
