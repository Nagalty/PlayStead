using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PlayStead.Providers.Gog;

public sealed record GogGamesV2Details(
    string ProductId,
    string? Title,
    IReadOnlyList<string>? Developers,
    IReadOnlyList<string>? Publishers,
    DateOnly? ReleaseDate,
    IReadOnlyList<string>? Tags,
    bool? SinglePlayer,
    string? Description)
{
    public static GogGamesV2Details? Parse(JsonElement root, string requestedProductId)
    {
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("_embedded", out var embedded) ||
            !embedded.TryGetProperty("product", out var product) ||
            !product.TryGetProperty("id", out var id) ||
            !string.Equals(id.ToString(), requestedProductId, StringComparison.Ordinal))
            return null;

        return new GogGamesV2Details(
            requestedProductId,
            StringValue(product, "title"),
            Names(embedded, "developers"),
            Names(embedded, "publishers") is { Count: > 0 } publishers
                ? publishers
                : embedded.TryGetProperty("publisher", out var publisher) && StringValue(publisher, "name") is { } name
                    ? [name]
                    : null,
            ParseDate(product, "globalReleaseDate"),
            Names(embedded, "tags"),
            HasFeature(embedded, "single"),
            CleanHtml(StringValue(root, "overview") ?? StringValue(root, "description")));
    }

    private static bool? HasFeature(JsonElement embedded, string id)
    {
        if (!embedded.TryGetProperty("features", out var features) || features.ValueKind != JsonValueKind.Array)
            return null;
        return features.EnumerateArray().Any(x => string.Equals(StringValue(x, "id"), id, StringComparison.Ordinal)) ? true : null;
    }

    private static IReadOnlyList<string>? Names(JsonElement parent, string property)
    {
        if (!parent.TryGetProperty(property, out var values) || values.ValueKind != JsonValueKind.Array)
            return null;
        var result = values.EnumerateArray().Select(x => StringValue(x, "name"))
            .Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return result.Length == 0 ? null : result;
    }

    private static string? StringValue(JsonElement parent, string property) =>
        parent.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static DateOnly? ParseDate(JsonElement parent, string property) =>
        DateTimeOffset.TryParse(StringValue(parent, property), CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var value) ? DateOnly.FromDateTime(value.UtcDateTime) : null;

    private static string? CleanHtml(string? value) => string.IsNullOrWhiteSpace(value)
        ? null
        : WebUtility.HtmlDecode(Regex.Replace(value, "<[^>]*>", string.Empty)).Trim();
}
