using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using PlayStead.Core.Graphics;
using PlayStead.Core.Library;

namespace PlayStead.Providers.Graphics;

public sealed class AmdOfficialGraphicsTechnologySupportSource : IGraphicsTechnologySupportSource
{
    public const string OfficialCatalogUrl = "https://www.amd.com/en/products/graphics/technologies/fidelityfx/supported-games.html";
    private const int CacheSchemaVersion = 2;
    private const int ParserVersion = 2;
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromDays(7);
    private static readonly Regex HeadingRegex = new("<h[1-6][^>]*>(?<text>.*?)</h[1-6]>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex CellRegex = new("<td[^>]*>(?<text>.*?)</td>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private readonly HttpClient _httpClient;
    private readonly string _cachePath;
    private readonly DateTimeOffset _now;

    public AmdOfficialGraphicsTechnologySupportSource(
        HttpClient httpClient,
        string cachePath,
        TimeProvider? timeProvider = null,
        DateTimeOffset? observedAtUtc = null)
    {
        _httpClient = httpClient;
        _cachePath = cachePath;
        _now = observedAtUtc ?? (timeProvider ?? TimeProvider.System).GetUtcNow();
    }

    public async Task<IReadOnlyList<GraphicsTechnologySupportEvidence>> GetSupportAsync(
        string gameTitle,
        GameId gameId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(gameTitle))
            return [];

        var cached = await ReadCacheAsync(cancellationToken);
        IReadOnlyList<CatalogEntry>? entries = null;
        if (cached is not null && _now - cached.FetchedAtUtc <= CacheLifetime)
        {
            entries = cached.Entries;
        }
        else
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, OfficialCatalogUrl);
                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();
                var html = await response.Content.ReadAsStringAsync(cancellationToken);
                var parsed = ParseCatalog(html);
                if (parsed.Count > 0)
                {
                    entries = parsed;
                    await WriteCacheAsync(new CachePayload(_now, parsed, CacheSchemaVersion, ParserVersion), cancellationToken);
                }
            }
            catch (HttpRequestException) { }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { }
            catch (IOException) { }
            catch (JsonException) { }

            entries ??= cached?.Entries;
        }

        if (entries is null)
            return [];

        var normalizedTitle = NormalizeTitle(gameTitle);
        var matchingEntries = entries
            .Where(x => string.Equals(x.NormalizedTitle, normalizedTitle, StringComparison.Ordinal))
            .ToArray();
        var duplicateTechnology = matchingEntries
            .GroupBy(x => (x.Technology, x.SectionKey))
            .Any(x => x.Count() > 1);
        var matches = matchingEntries.Select(x => x.Technology).Distinct().ToArray();
        if (matches.Length == 0 || duplicateTechnology)
            return [];

        return matches.Select(technology => new GraphicsTechnologySupportEvidence(
            gameId,
            technology,
            GraphicsSupportStatus.Supported,
            GraphicsTechnologySupportSourceKind.OfficialVendor,
            "AMD",
            _now,
            gameTitle)).ToArray();
    }

    internal static IReadOnlyList<CatalogEntry> ParseCatalog(string html)
    {
        var headings = HeadingRegex.Matches(html).Cast<Match>().ToArray();
        var entries = new List<CatalogEntry>();
        for (var i = 0; i < headings.Length; i++)
        {
            var heading = CleanText(headings[i].Groups["text"].Value);
            var technology = TechnologyForHeading(heading);
            if (technology is null)
                continue;

            var start = headings[i].Index + headings[i].Length;
            var end = i + 1 < headings.Length ? headings[i + 1].Index : html.Length;
            foreach (Match cell in CellRegex.Matches(html[start..end]))
            {
                var title = CleanText(cell.Groups["text"].Value);
                if (!string.IsNullOrWhiteSpace(title))
                    entries.Add(new CatalogEntry(title, NormalizeTitle(title), technology.Value, NormalizeTitle(heading)));
            }
        }

        return entries;
    }

    internal static string NormalizeTitle(string value)
    {
        var decomposed = value.Normalize(System.Text.NormalizationForm.FormD);
        var chars = decomposed
            .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
            .ToArray();
        var text = new string(chars).Replace("™", string.Empty, StringComparison.Ordinal).Replace("®", string.Empty, StringComparison.Ordinal);
        var normalized = new string(text.Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : ' ').ToArray());
        return string.Join(' ', normalized.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static GraphicsTechnology? TechnologyForHeading(string heading)
    {
        var normalized = NormalizeTitle(heading);
        if (normalized.Contains("amd fsr frame generation", StringComparison.Ordinal) || normalized.EndsWith("fsr frame generation", StringComparison.Ordinal))
            return GraphicsTechnology.FrameGeneration;
        if (normalized.Contains("amd fsr", StringComparison.Ordinal) &&
            (normalized.Contains("redstone", StringComparison.Ordinal) || normalized.EndsWith("fsr 1", StringComparison.Ordinal) || normalized.EndsWith("fsr 2", StringComparison.Ordinal) || normalized.EndsWith("fsr 3", StringComparison.Ordinal)))
            return GraphicsTechnology.FSR;
        return null;
    }

    private static string CleanText(string value)
    {
        var withoutTags = Regex.Replace(value, "<[^>]+>", " ");
        return WebUtility.HtmlDecode(withoutTags).Trim();
    }

    private async Task<CachePayload?> ReadCacheAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_cachePath))
            return null;
        try
        {
            await using var stream = File.OpenRead(_cachePath);
            var payload = await JsonSerializer.DeserializeAsync<CachePayload>(stream, cancellationToken: cancellationToken);
            return payload?.SchemaVersion == CacheSchemaVersion && payload.ParserVersion == ParserVersion ? payload : null;
        }
        catch (FileNotFoundException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
        catch (IOException) { return null; }
        catch (JsonException) { return null; }
    }

    private async Task WriteCacheAsync(CachePayload payload, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_cachePath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);
        var tempPath = _cachePath + ".tmp";
        await using (var stream = File.Create(tempPath))
            await JsonSerializer.SerializeAsync(stream, payload, cancellationToken: cancellationToken);
        File.Move(tempPath, _cachePath, true);
    }

    internal sealed record CatalogEntry(string Title, string NormalizedTitle, GraphicsTechnology Technology, string SectionKey);

    private sealed record CachePayload(
        DateTimeOffset FetchedAtUtc,
        IReadOnlyList<CatalogEntry> Entries,
        int SchemaVersion,
        int ParserVersion);

}
