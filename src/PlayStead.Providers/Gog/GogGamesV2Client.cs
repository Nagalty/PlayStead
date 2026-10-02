using System.Globalization;
using System.Net;
using System.Text.Json;

namespace PlayStead.Providers.Gog;

public sealed class GogGamesV2Client(HttpClient httpClient) : IGogGamesV2Client
{
    public async Task<GogGamesV2Details?> GetAsync(string productId, CancellationToken cancellationToken)
    {
        if (!ulong.TryParse(productId, NumberStyles.None, CultureInfo.InvariantCulture, out _))
            return null;
        using var response = await httpClient.GetAsync($"https://api.gog.com/v2/games/{productId}?locale=fr-FR", cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        var result = GogGamesV2Details.Parse(document.RootElement, productId);
        return result;
    }
}
