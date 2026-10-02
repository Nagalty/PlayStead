using PlayStead.Core.Media;
using PlayStead.Core.Library;

namespace PlayStead.Core.ProviderGameMetadata;

public static class ProviderGameMetadataComposer
{
    public static ProviderGameMetadata Compose(
        ProviderGameMetadata? baseMetadata,
        ProviderGameMetadata? enrichmentMetadata,
        MediaSourceIdentity? currentMediaSource)
    {
        var enrichment = IsUsableEnrichment(baseMetadata, enrichmentMetadata, currentMediaSource)
            ? enrichmentMetadata
            : null;
        var source = baseMetadata ?? enrichment;
        if (source is null)
            throw new ArgumentException("At least one metadata row is required.", nameof(baseMetadata));

        return source with
        {
            Genres = Prefer(baseMetadata?.Genres, enrichment?.Genres),
            Categories = Prefer(baseMetadata?.Categories, enrichment?.Categories),
            Developers = Prefer(baseMetadata?.Developers, enrichment?.Developers),
            Publishers = Prefer(baseMetadata?.Publishers, enrichment?.Publishers),
            ReleaseDate = baseMetadata?.ReleaseDate ?? enrichment?.ReleaseDate,
            IsFree = baseMetadata?.IsFree ?? enrichment?.IsFree,
            SinglePlayer = baseMetadata?.SinglePlayer ?? enrichment?.SinglePlayer,
            MultiPlayer = baseMetadata?.MultiPlayer ?? enrichment?.MultiPlayer,
            OnlineCoop = baseMetadata?.OnlineCoop ?? enrichment?.OnlineCoop,
            LocalCoop = baseMetadata?.LocalCoop ?? enrichment?.LocalCoop,
            Availability = baseMetadata?.Availability is ProviderGameMetadataAvailability.Unknown
                ? enrichment?.Availability ?? baseMetadata.Availability
                : baseMetadata?.Availability ?? enrichment?.Availability ?? ProviderGameMetadataAvailability.Unknown,
            ShortDescription = Prefer(baseMetadata?.ShortDescription, enrichment?.ShortDescription),
            OnlineCoopMaxPlayers = baseMetadata?.OnlineCoopMaxPlayers ?? enrichment?.OnlineCoopMaxPlayers,
            OnlineMultiplayerMaxPlayers = baseMetadata?.OnlineMultiplayerMaxPlayers ?? enrichment?.OnlineMultiplayerMaxPlayers,
            OfflineCoopMaxPlayers = baseMetadata?.OfflineCoopMaxPlayers ?? enrichment?.OfflineCoopMaxPlayers,
            OfflineMultiplayerMaxPlayers = baseMetadata?.OfflineMultiplayerMaxPlayers ?? enrichment?.OfflineMultiplayerMaxPlayers
        };
    }

    private static bool IsUsableEnrichment(
        ProviderGameMetadata? baseMetadata,
        ProviderGameMetadata? enrichment,
        MediaSourceIdentity? currentMediaSource)
    {
        if (enrichment is null)
            return false;
        if (enrichment.Provider != ProviderKind.Steam || (baseMetadata is not null && baseMetadata.Provider != ProviderKind.Manual))
            return true;
        return currentMediaSource is { Provider: ProviderKind.Steam } current &&
               string.Equals(current.ExternalId, enrichment.ProviderGameId, StringComparison.Ordinal);
    }

    private static IReadOnlyList<string>? Prefer(IReadOnlyList<string>? baseValue, IReadOnlyList<string>? enrichmentValue) =>
        baseValue is not null ? baseValue : enrichmentValue;

    private static string? Prefer(string? baseValue, string? enrichmentValue) =>
        !string.IsNullOrWhiteSpace(baseValue) ? baseValue : enrichmentValue;
}
