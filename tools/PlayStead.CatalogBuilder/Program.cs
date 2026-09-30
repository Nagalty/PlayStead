using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
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
var manifest = new CanonicalCatalogManifest(1, catalogVersion, generatedAt, "catalog-payload.json", Convert.ToHexString(SHA256.HashData(payload)), entries.Count, payload.LongLength);
ValidateArtifact(document, manifest, payload);

Directory.CreateDirectory(parsed.OutputPath);
await WriteAtomicallyAsync(Path.Combine(parsed.OutputPath, "catalog-payload.json"), payload);
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

static void ValidateArtifact(CanonicalCatalogDocument document, CanonicalCatalogManifest manifest, byte[] payload)
{
    if (manifest.SchemaVersion != 1 || manifest.EntryCount != document.Entries.Count || manifest.PayloadSizeBytes != payload.LongLength || !manifest.PayloadSha256.Equals(Convert.ToHexString(SHA256.HashData(payload)), StringComparison.OrdinalIgnoreCase) || document.Entries.Any(x => string.IsNullOrWhiteSpace(x.CanonicalTitle) || string.IsNullOrWhiteSpace(x.NormalizedTitle))) throw new InvalidDataException("Generated catalog artifact failed validation.");
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

internal static class IgdbClient
{
    public static async Task<IgdbRecord[]> FetchAsync()
    {
        var clientId = Environment.GetEnvironmentVariable("IGDB_CLIENT_ID"); var secret = Environment.GetEnvironmentVariable("IGDB_CLIENT_SECRET");
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(secret)) throw new InvalidOperationException("IGDB_CLIENT_ID and IGDB_CLIENT_SECRET are required for --igdb.");
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        using var tokenResponse = await client.PostAsync($"https://id.twitch.tv/oauth2/token?client_id={Uri.EscapeDataString(clientId)}&client_secret={Uri.EscapeDataString(secret)}&grant_type=client_credentials", null); tokenResponse.EnsureSuccessStatusCode();
        var token = (await JsonSerializer.DeserializeAsync<Token>(await tokenResponse.Content.ReadAsStreamAsync()))?.AccessToken ?? throw new InvalidDataException("IGDB token response missing access_token.");
        client.DefaultRequestHeaders.Add("Client-ID", clientId); client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var records = new List<IgdbRecord>();
        for (var offset = 0; ; offset += 500)
        {
            using var body = new StringContent("fields id,name,first_release_date; limit 500; offset " + offset + ";", Encoding.UTF8, "text/plain"); using var response = await client.PostAsync("https://api.igdb.com/v4/games", body); response.EnsureSuccessStatusCode(); using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync()); var page = document.RootElement.EnumerateArray().Select(ParseGame).ToArray(); records.AddRange(page); if (page.Length < 500) break;
        }
        return records.ToArray();
    }
    private static IgdbRecord ParseGame(JsonElement game) => new(game.GetProperty("id").GetInt64().ToString(), game.GetProperty("name").GetString() ?? string.Empty, null, null, [], [], game.TryGetProperty("first_release_date", out var date) ? date.GetInt64() : null);
    private sealed record Token([property: JsonPropertyName("access_token")] string AccessToken);
}
