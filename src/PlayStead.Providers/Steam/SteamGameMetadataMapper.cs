using System.Globalization;
using PlayStead.Core.Library;
using PlayStead.Core.ProviderGameMetadata;

namespace PlayStead.Providers.Steam;

public static class SteamGameMetadataMapper
{
    public static ProviderGameMetadataPatch Map(
        SteamAppInfoEntry entry,
        GameId gameId,
        ProviderKind provider,
        DateTimeOffset refreshedAtUtc)
    {
        var categories = entry.Categories ?? [];
        bool? Capability(string description) => categories.Any(x => string.Equals(x, description, StringComparison.OrdinalIgnoreCase)) ? true : null;
        var release = entry.ReleaseDateReported
            ? DateOnly.TryParse(entry.ReleaseDateText, CultureInfo.InvariantCulture, DateTimeStyles.None, out var value)
                ? ProviderField<DateOnly>.FromValue(value)
                : ProviderField<DateOnly>.NotReported
            : ProviderField<DateOnly>.NotReported;

        return new ProviderGameMetadataPatch(
            gameId,
            provider,
            entry.AppId.ToString(CultureInfo.InvariantCulture),
            Collection(entry.Genres),
            Collection(entry.Categories),
            Collection(entry.Developers ?? (entry.Developer is null ? null : [entry.Developer])),
            Collection(entry.Publishers ?? (entry.Publisher is null ? null : [entry.Publisher])),
            release,
            entry.IsFree.HasValue ? ProviderField<bool>.FromValue(entry.IsFree.Value) : ProviderField<bool>.NotReported,
            CapabilityField(Capability("Single-player")),
            CapabilityField(Capability("Multi-player")),
            CapabilityField(Capability("Online Co-op")),
            CapabilityField(Capability("Local Co-op") ?? Capability("Shared/Split Screen Co-op")),
            ProviderGameMetadataAvailability.Partial,
            ProviderField<string>.NotReported);
    }

    private static ProviderField<IReadOnlyList<string>> Collection(IReadOnlyList<string>? values) =>
        values is null ? ProviderField<IReadOnlyList<string>>.NotReported : ProviderField<IReadOnlyList<string>>.FromValue(values);

    private static ProviderField<bool> CapabilityField(bool? value) =>
        value.HasValue ? ProviderField<bool>.FromValue(value.Value) : ProviderField<bool>.NotReported;
}
