using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace PlayStead.Providers.Steam;

public sealed record SteamStoreAppDetails(
    uint SteamAppId,
    IReadOnlyList<string>? Genres,
    IReadOnlyList<string>? Categories,
    IReadOnlyList<string>? Developers,
    IReadOnlyList<string>? Publishers,
    DateOnly? ReleaseDate,
    bool? IsFree,
    string? ShortDescription,
    Uri? HeaderImageUri,
    Uri? CapsuleImageUri,
    Uri? CapsuleImageV5Uri)
{
    internal static SteamStoreAppDetails? Parse(System.Text.Json.JsonElement root, string requestedAppId)
    {
        if (!uint.TryParse(requestedAppId, NumberStyles.None, CultureInfo.InvariantCulture, out var requested) ||
            root.ValueKind != System.Text.Json.JsonValueKind.Object)
            return null;
        foreach (var entry in root.EnumerateObject())
        {
            var app = entry.Value;
            if (!app.TryGetProperty("success", out var success) || success.ValueKind != System.Text.Json.JsonValueKind.True ||
                !app.TryGetProperty("data", out var data) || data.ValueKind != System.Text.Json.JsonValueKind.Object ||
                !data.TryGetProperty("steam_appid", out var id) || !uint.TryParse(id.ToString(), out var actual) || actual != requested)
                continue;
            return new SteamStoreAppDetails(actual, Collection(data, "genres"), Collection(data, "categories"), Strings(data, "developers"), Strings(data, "publishers"), ParseReleaseDate(data), Bool(data, "is_free"), ParseShortDescription(data), UriValue(data, "header_image"), UriValue(data, "capsule_image"), UriValue(data, "capsule_imagev5"));
        }
        return null;
    }

    private static IReadOnlyList<string>? Collection(System.Text.Json.JsonElement data, string property) =>
        data.TryGetProperty(property, out var value) && value.ValueKind == System.Text.Json.JsonValueKind.Array
            ? value.EnumerateArray().Where(x => x.TryGetProperty("description", out var d) && d.ValueKind == System.Text.Json.JsonValueKind.String).Select(x => x.GetProperty("description").GetString()!).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray()
            : null;
    private static IReadOnlyList<string>? Strings(System.Text.Json.JsonElement data, string property) =>
        data.TryGetProperty(property, out var value) && value.ValueKind == System.Text.Json.JsonValueKind.Array
            ? value.EnumerateArray().Where(x => x.ValueKind == System.Text.Json.JsonValueKind.String).Select(x => x.GetString()!).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray()
            : null;
    private static Uri? UriValue(System.Text.Json.JsonElement data, string property) =>
        data.TryGetProperty(property, out var value) && value.ValueKind == System.Text.Json.JsonValueKind.String && Uri.TryCreate(value.GetString(), UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp) ? uri : null;
    private static bool? Bool(System.Text.Json.JsonElement data, string property) => data.TryGetProperty(property, out var value) && (value.ValueKind == System.Text.Json.JsonValueKind.True || value.ValueKind == System.Text.Json.JsonValueKind.False) ? value.GetBoolean() : null;
    private static DateOnly? ParseReleaseDate(System.Text.Json.JsonElement data) => data.TryGetProperty("release_date", out var release) && release.TryGetProperty("date", out var date) && DateOnly.TryParse(date.GetString(), CultureInfo.GetCultureInfo("fr-FR"), System.Globalization.DateTimeStyles.AllowWhiteSpaces, out var parsed) ? parsed : null;
    private static string? ParseShortDescription(System.Text.Json.JsonElement data) => data.TryGetProperty("short_description", out var value) && value.ValueKind == System.Text.Json.JsonValueKind.String ? WebUtility.HtmlDecode(Regex.Replace(value.GetString() ?? string.Empty, "<[^>]*>", string.Empty)).Trim() : null;
}
