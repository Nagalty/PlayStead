using System.Net;
using System.Net.Http.Headers;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using PlayStead.Core.Catalog;

var parsed = Arguments.Parse(args);
if (parsed is null)
{
    Console.Error.WriteLine("Usage: PlayStead.CatalogBuilder --input <igdb-export.json> --output <dir> [--dry-run] [--catalog-version <number>]");
    Console.Error.WriteLine("       PlayStead.CatalogBuilder --igdb --output <dir> [--dry-run] [--catalog-version <number>]");
    return 2;
}

var records = parsed.InputPath is not null ? await LoadExportAsync(parsed.InputPath) : await IgdbClient.FetchAsync();
var entries = BuildEntries(records);
var generatedAt = DateTimeOffset.TryParse(Environment.GetEnvironmentVariable("PLAYSTEAD_CATALOG_GENERATED_AT_UTC"), out var fixedTime) ? fixedTime.ToUniversalTime() : DateTimeOffset.UtcNow;
var catalogVersion = parsed.CatalogVersion ?? long.Parse(generatedAt.ToString("yyyyMMddHHmm"), System.Globalization.CultureInfo.InvariantCulture);
var document = new CanonicalCatalogDocument(1, catalogVersion, generatedAt, entries);
var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
var payload = JsonSerializer.SerializeToUtf8Bytes(document, jsonOptions);
var compressedPayload = CompressDeterministically(payload);
var manifest = new CanonicalCatalogManifest(2, catalogVersion, generatedAt, "catalog-payload.json.gz", Convert.ToHexString(SHA256.HashData(compressedPayload)), entries.Count, compressedPayload.LongLength, "gzip", payload.LongLength);
ValidateArtifact(document, manifest, payload, compressedPayload);

Directory.CreateDirectory(parsed.OutputPath);
await WriteAtomicallyAsync(Path.Combine(parsed.OutputPath, "catalog-payload.json"), payload);
await WriteAtomicallyAsync(Path.Combine(parsed.OutputPath, "catalog-payload.json.gz"), compressedPayload);
await WriteAtomicallyAsync(Path.Combine(parsed.OutputPath, "catalog-manifest.json"), JsonSerializer.SerializeToUtf8Bytes(manifest, jsonOptions));
if (parsed.DryRun) Console.Error.WriteLine("Dry-run: validated artifacts generated locally; nothing published.");
return 0;

static async Task<IgdbRecord[]> LoadExportAsync(string path) => JsonSerializer.Deserialize<IgdbRecord[]>(await File.ReadAllBytesAsync(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];

static List<CanonicalCatalogEntry> BuildEntries(IEnumerable<IgdbRecord> source)
{
    var candidates = source.Where(x => !string.IsNullOrWhiteSpace(x.Id) && !string.IsNullOrWhiteSpace(x.Name))
        .Select(x => (Record: x, Normalized: CanonicalCatalogTitleNormalizer.Normalize(x.Name))).Where(x => x.Normalized.Length != 0)
        .GroupBy(x => x.Normalized, StringComparer.Ordinal).Select(x => x.OrderBy(y => y.Record.Id, StringComparer.Ordinal).First()).OrderBy(x => x.Normalized, StringComparer.Ordinal).ToList();
    return candidates.Select((item, index) => new CanonicalCatalogEntry(new CatalogContentId(StableGuid(item.Record.Id)), PlaySteadPublicId.Parse($"PlayStead-{index + 1:000000}"), item.Record.Name, item.Normalized,
        item.Record.ReleaseDate is null ? null : DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds(item.Record.ReleaseDate.Value).UtcDateTime), item.Record.Developer, item.Record.Publisher,
        (item.Record.Genres ?? []).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray(),
        (item.Record.ExternalGames ?? []).Where(x => x is not null && !string.IsNullOrWhiteSpace(x.ExternalId)).Select(x => new CanonicalCatalogProviderReference(MapProvider(x.Source), x.ExternalId!, null, CatalogProvenance.Igdb, CatalogConfidence.Deterministic, DateTimeOffset.UnixEpoch)).Where(x => x.Provider is CatalogProviderKind.Steam or CatalogProviderKind.Epic or CatalogProviderKind.Gog).GroupBy(x => (x.Provider, x.ExternalId)).Select(x => x.First()).OrderBy(x => x.Provider).ThenBy(x => x.ExternalId, StringComparer.Ordinal).ToArray(), CatalogProvenance.Igdb, DateTimeOffset.UnixEpoch)).ToList();
}

static CatalogProviderKind MapProvider(string? source) => source?.Trim().ToLowerInvariant() switch { "steam" => CatalogProviderKind.Steam, "epic" or "epic games store" => CatalogProviderKind.Epic, "gog" or "gog.com" => CatalogProviderKind.Gog, _ => CatalogProviderKind.Igdb };
static Guid StableGuid(string value) => new(SHA256.HashData(Encoding.UTF8.GetBytes(value))[..16]);

static byte[] CompressDeterministically(byte[] payload)
{
    using var output = new MemoryStream();
    using (var gzip = new GZipStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
        gzip.Write(payload);
    return output.ToArray();
}

static void ValidateArtifact(CanonicalCatalogDocument document, CanonicalCatalogManifest manifest, byte[] payload, byte[] compressedPayload)
{
    if (manifest.SchemaVersion != 2 || manifest.PayloadEncoding != "gzip" || manifest.EntryCount != document.Entries.Count || manifest.PayloadSizeBytes != compressedPayload.LongLength || manifest.PayloadUncompressedSizeBytes != payload.LongLength || !manifest.PayloadSha256.Equals(Convert.ToHexString(SHA256.HashData(compressedPayload)), StringComparison.OrdinalIgnoreCase) || document.Entries.Any(x => string.IsNullOrWhiteSpace(x.CanonicalTitle) || string.IsNullOrWhiteSpace(x.NormalizedTitle))) throw new InvalidDataException("Generated catalog artifact failed validation.");
    var secrets = new[] { Environment.GetEnvironmentVariable("IGDB_CLIENT_ID"), Environment.GetEnvironmentVariable("IGDB_CLIENT_SECRET") }.Where(x => !string.IsNullOrWhiteSpace(x));
    var text = Encoding.UTF8.GetString(payload);
    if (secrets.Any(secret => text.Contains(secret!, StringComparison.Ordinal))) throw new InvalidDataException("Generated artifact contains a configured secret.");
}

static async Task WriteAtomicallyAsync(string path, byte[] content) { var temp = path + ".tmp"; await File.WriteAllBytesAsync(temp, content); File.Move(temp, path, true); }

public sealed record IgdbRecord(string Id, string Name, string? Developer, string? Publisher, string[]? Genres, IgdbExternal[]? ExternalGames, long? ReleaseDate = null);
public sealed record IgdbExternal(string? Source, string? ExternalId);

internal sealed record Arguments(string? InputPath, string OutputPath, bool DryRun, long? CatalogVersion)
{
    public static Arguments? Parse(string[] args)
    {
        string? input = null, output = null; var dry = false; long? version = null; var igdb = false;
        for (var i = 0; i < args.Length; i++) switch (args[i]) { case "--input" when i + 1 < args.Length: input = args[++i]; break; case "--igdb": igdb = true; break; case "--output" when i + 1 < args.Length: output = args[++i]; break; case "--dry-run": dry = true; break; case "--catalog-version" when i + 1 < args.Length && long.TryParse(args[++i], out var v): version = v; break; }
        return output is null || (input is null && !igdb) ? null : new(input, output, dry, version);
    }
}

public static class IgdbClient
{
    private static readonly TimeSpan MinimumRequestInterval = TimeSpan.FromMilliseconds(250);
    private const int Max429Retries = 3;

    public static async Task<IgdbRecord[]> FetchAsync(HttpClient? httpClient = null)
    {
        var clientId = Environment.GetEnvironmentVariable("IGDB_CLIENT_ID"); var secret = Environment.GetEnvironmentVariable("IGDB_CLIENT_SECRET");
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(secret)) throw new InvalidOperationException("IGDB_CLIENT_ID and IGDB_CLIENT_SECRET are required for --igdb.");
        using var ownedClient = httpClient is null ? new HttpClient { Timeout = TimeSpan.FromSeconds(30) } : null;
        var client = httpClient ?? ownedClient!;
        using var tokenBody = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["client_secret"] = secret,
            ["grant_type"] = "client_credentials"
        });
        using var tokenResponse = await client.PostAsync("https://id.twitch.tv/oauth2/token", tokenBody);
        if (!tokenResponse.IsSuccessStatusCode)
        {
            var detail = await tokenResponse.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Twitch OAuth failed: HTTP {(int)tokenResponse.StatusCode} - {SanitizeError(detail, clientId, secret)}");
        }
        var token = (await JsonSerializer.DeserializeAsync<Token>(await tokenResponse.Content.ReadAsStreamAsync()))?.AccessToken ?? throw new InvalidDataException("IGDB token response missing access_token.");
        client.DefaultRequestHeaders.Add("Client-ID", clientId); client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var records = new List<IgdbRecord>();
        DateTimeOffset? lastRequest = null;
        for (var offset = 0; ; offset += 500)
        {
            const string fields = "fields id,name,first_release_date,involved_companies.company.name,involved_companies.developer,involved_companies.publisher,genres.name,external_games.external_game_source.name,external_games.uid;";
            var query = $"{fields} limit 500; offset {offset};";
            using var response = await PostIgdbPageAsync(client, query, lastRequest);
            lastRequest = DateTimeOffset.UtcNow;
            using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync());
            var page = document.RootElement.EnumerateArray().Select(ParseGame).ToArray();
            Console.Error.WriteLine($"IGDB_PAGE offset={offset} count={page.Length}");
            records.AddRange(page);
            if (page.Length < 500) break;
        }
        return records.ToArray();
    }

    private static async Task<HttpResponseMessage> PostIgdbPageAsync(HttpClient client, string query, DateTimeOffset? lastRequest)
    {
        var lastSent = lastRequest;
        for (var attempt = 0; ; attempt++)
        {
            if (lastSent is { } previous)
            {
                var wait = MinimumRequestInterval - (DateTimeOffset.UtcNow - previous);
                if (wait > TimeSpan.Zero) await Task.Delay(wait);
            }
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.igdb.com/v4/games") { Content = new StringContent(query, Encoding.UTF8, "text/plain") };
            var response = await client.SendAsync(request);
            lastSent = DateTimeOffset.UtcNow;
            if (response.StatusCode != HttpStatusCode.TooManyRequests || attempt >= Max429Retries) return response;
            TimeSpan? retryAfter = response.Headers.RetryAfter?.Delta;
            if (retryAfter is null && response.Headers.RetryAfter?.Date is { } retryDate)
                retryAfter = retryDate - DateTimeOffset.UtcNow;
            retryAfter ??= TimeSpan.FromMilliseconds(500 * (attempt + 1));
            response.Dispose();
            var retryDelay = retryAfter.Value;
            if (retryDelay < TimeSpan.Zero) retryDelay = TimeSpan.Zero;
            if (retryDelay > TimeSpan.FromSeconds(10)) retryDelay = TimeSpan.FromSeconds(10);
            var intervalRemaining = MinimumRequestInterval - (DateTimeOffset.UtcNow - lastSent.Value);
            await Task.Delay(retryDelay > intervalRemaining ? retryDelay : intervalRemaining);
        }
    }
    internal static string SanitizeError(string detail, string clientId, string secret)
    {
        var sanitized = detail.Replace(secret, "[redacted]", StringComparison.Ordinal)
            .Replace(clientId, "[redacted]", StringComparison.Ordinal);
        return Regex.Replace(sanitized, @"(?i)(access[_-]?token\s*[:=]\s*[""']?)[^\s,;""'}]+", "$1[redacted]");
    }
    private static IgdbRecord ParseGame(JsonElement game)
    {
        var companies = game.TryGetProperty("involved_companies", out var companyArray) && companyArray.ValueKind == JsonValueKind.Array ? companyArray.EnumerateArray().ToArray() : [];
        var developers = companies.Where(x => x.TryGetProperty("developer", out var d) && d.GetBoolean()).Select(x => x.GetProperty("company").GetProperty("name").GetString()).OfType<string>();
        var publishers = companies.Where(x => x.TryGetProperty("publisher", out var p) && p.GetBoolean()).Select(x => x.GetProperty("company").GetProperty("name").GetString()).OfType<string>();
        var genres = game.TryGetProperty("genres", out var genreArray) && genreArray.ValueKind == JsonValueKind.Array ? genreArray.EnumerateArray().Select(x => x.GetProperty("name").GetString()).OfType<string>().ToArray() : [];
        var external = game.TryGetProperty("external_games", out var externalArray) && externalArray.ValueKind == JsonValueKind.Array ? externalArray.EnumerateArray().Select(x => new IgdbExternal(x.TryGetProperty("external_game_source", out var source) && source.ValueKind == JsonValueKind.Object && source.TryGetProperty("name", out var name) ? name.GetString() : null, x.TryGetProperty("uid", out var uid) ? uid.GetString() : null)).ToArray() : [];
        return new(game.GetProperty("id").GetInt64().ToString(), game.GetProperty("name").GetString() ?? string.Empty, developers.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).FirstOrDefault(), publishers.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).FirstOrDefault(), genres.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray(), external, game.TryGetProperty("first_release_date", out var date) ? date.GetInt64() : null);
    }
    private sealed record Token([property: JsonPropertyName("access_token")] string AccessToken);
}
