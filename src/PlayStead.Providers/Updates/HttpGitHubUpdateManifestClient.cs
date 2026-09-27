using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using PlayStead.Core.Updates;

namespace PlayStead.Providers.Updates;

public sealed class HttpGitHubUpdateManifestClient : IGitHubUpdateManifestClient
{
    private readonly HttpClient _httpClient;

    public HttpGitHubUpdateManifestClient(HttpClient httpClient) => _httpClient = httpClient;

    public async Task<GitHubUpdateManifest?> GetAsync(Uri manifestUri, string expectedChannel, CancellationToken cancellationToken = default)
    {
        if (manifestUri.Scheme != Uri.UriSchemeHttps) throw new InvalidOperationException("Update manifest URI must use HTTPS.");
        var dto = await _httpClient.GetFromJsonAsync<ManifestDto>(manifestUri, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("Update manifest response was empty.");
        if (!SemanticVersion.TryParse(dto.Version, out _)) throw new InvalidDataException("Update manifest version is invalid.");
        if (!string.Equals(dto.Channel, expectedChannel, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Update manifest channel is not supported by this build.");
        if (!Uri.TryCreate(dto.PackageUrl, UriKind.Absolute, out var packageUri) || packageUri.Scheme != Uri.UriSchemeHttps) throw new InvalidDataException("Update package URI must use HTTPS.");
        Uri? notesUri = null;
        if (!string.IsNullOrWhiteSpace(dto.ReleaseNotesUrl))
        {
            if (!Uri.TryCreate(dto.ReleaseNotesUrl, UriKind.Absolute, out notesUri) || notesUri.Scheme != Uri.UriSchemeHttps) throw new InvalidDataException("Release notes URI must use HTTPS.");
        }
        if (string.IsNullOrWhiteSpace(dto.Sha256) || dto.Sha256.Length != 64 || !dto.Sha256.All(IsHex)) throw new InvalidDataException("SHA-256 is invalid.");
        return new GitHubUpdateManifest(dto.Version!, DistributionChannel.GitHub, packageUri, dto.Sha256, notesUri, dto.PublishedAtUtc)
        {
            ReleaseChannel = dto.Channel!
        };
    }

    private static bool IsHex(char value) => value is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F';

    private sealed record ManifestDto(
        [property: JsonPropertyName("channel")] string? Channel,
        [property: JsonPropertyName("version")] string? Version,
        [property: JsonPropertyName("publishedAtUtc")] DateTimeOffset? PublishedAtUtc,
        [property: JsonPropertyName("packageUrl")] string? PackageUrl,
        [property: JsonPropertyName("sha256")] string? Sha256,
        [property: JsonPropertyName("releaseNotesUrl")] string? ReleaseNotesUrl);
}
