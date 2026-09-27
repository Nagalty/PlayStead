using System.Text.Json;
using PlayStead.Core.Library;
using PlayStead.Core.ProviderGameMetadata;

namespace PlayStead.Providers.Igdb;

/// <summary>Maps captured IGDB records without performing authentication or network access.</summary>
public static class IgdbGameMetadataMapper
{
    public static bool TryMap(
        JsonElement record,
        GameId canonicalGameId,
        ProviderKind provider,
        DateTimeOffset refreshedAtUtc,
        out ProviderGameMetadataPatch patch)
    {
        patch = default!;
        if (record.ValueKind != JsonValueKind.Object ||
            !TrySteamExternalId(record, out var steamAppId))
            return false;

        var modes = record.TryGetProperty("multiplayer_modes", out var values) && values.ValueKind == JsonValueKind.Array
            ? values.EnumerateArray().ToArray()
            : [];
        int? Value(string name) => modes.Select(x => x.TryGetProperty(name, out var value) && value.TryGetInt32(out var parsed) && parsed > 0 ? parsed : (int?)null).FirstOrDefault(x => x.HasValue);

        patch = new ProviderGameMetadataPatch(
            canonicalGameId,
            provider,
            steamAppId,
            ProviderField<IReadOnlyList<string>>.NotReported,
            ProviderField<IReadOnlyList<string>>.NotReported,
            ProviderField<IReadOnlyList<string>>.NotReported,
            ProviderField<IReadOnlyList<string>>.NotReported,
            ProviderField<DateOnly>.NotReported,
            ProviderField<bool>.NotReported,
            ProviderField<bool>.NotReported,
            ProviderField<bool>.NotReported,
            Bool(modes, "onlinecoop"),
            Bool(modes, "offlinecoop"),
            ProviderGameMetadataAvailability.Unknown,
            ProviderField<string>.NotReported,
            Field(Value("onlinecoopmax")),
            Field(Value("onlinemax")),
            Field(Value("offlinecoopmax")),
            Field(Value("offlinemax")));
        return true;
    }

    private static bool TrySteamExternalId(JsonElement record, out string appId)
    {
        appId = string.Empty;
        if (!record.TryGetProperty("external_games", out var external) || external.ValueKind != JsonValueKind.Array)
            return false;
        foreach (var item in external.EnumerateArray())
        {
            var source = item.TryGetProperty("external_game_source", out var sourceValue) &&
                         sourceValue.TryGetProperty("name", out var name)
                ? name.GetString()
                : null;
            if (!string.Equals(source, "Steam", StringComparison.OrdinalIgnoreCase)) continue;
            if (item.TryGetProperty("uid", out var uid) && uid.ValueKind == JsonValueKind.String && uint.TryParse(uid.GetString(), out _))
            {
                appId = uid.GetString()!;
                return true;
            }
        }
        return false;
    }

    private static ProviderField<bool> Bool(IEnumerable<JsonElement> modes, string name) =>
        modes.Any(x => x.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True)
            ? ProviderField<bool>.FromValue(true)
            : ProviderField<bool>.NotReported;

    private static ProviderField<int> Field(int? value) =>
        value is int number ? ProviderField<int>.FromValue(number) : ProviderField<int>.NotReported;
}
