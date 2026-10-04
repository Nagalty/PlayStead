using PlayStead.Core.Library;
using PlayStead.Core.Media;
using PlayStead.Core.Catalog;
using PlayStead.Core.Persistence;
using System.Net.Http.Headers;

namespace PlayStead.Providers.Epic;

public sealed class EpicMediaProvider : IGameMediaProvider
{
    private readonly EpicLocalMediaLocator _locator;
    private readonly ICanonicalCatalogStore? _catalog;
    private readonly HttpClient? _httpClient;

    public EpicMediaProvider(EpicLocalMediaLocator locator, ICanonicalCatalogStore? catalog = null, HttpClient? httpClient = null)
    {
        _locator = locator ?? throw new ArgumentNullException(nameof(locator));
        _catalog = catalog;
        _httpClient = httpClient;
    }

    public bool CanResolve(GameMediaIdentity identity) => identity.Provider == ProviderKind.Epic && !string.IsNullOrWhiteSpace(identity.ProviderGameId);

    public async Task<GameMediaPayload?> ResolveAsync(GameMediaIdentity identity, GameMediaAssetType assetType, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identity);
        cancellationToken.ThrowIfCancellationRequested();
        if (!CanResolve(identity) || assetType is not (GameMediaAssetType.Cover or GameMediaAssetType.Hero))
            return null;

        var path = _locator.TryLocate(identity.ProviderGameId, assetType);
        if (path is null && _catalog is not null && _httpClient is not null)
            return await ResolveCanonicalAsync(identity, assetType, cancellationToken).ConfigureAwait(false);
        if (path is null) return null;

        try
        {
            var content = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
            return new GameMediaPayload(assetType, "epic-local", identity.ProviderGameId, content, GetContentType(path), new Uri(Path.GetFullPath(path)));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private async Task<GameMediaPayload?> ResolveCanonicalAsync(GameMediaIdentity identity, GameMediaAssetType assetType, CancellationToken cancellationToken)
    {
        var content = await _catalog!.FindByProviderRefAsync(CatalogProviderKind.Epic, identity.ProviderGameId, cancellationToken).ConfigureAwait(false);
        var url = assetType == GameMediaAssetType.Cover ? content?.Media?.CoverUrl : content?.Media?.HeroUrl;
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            return null;
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("image/*"));
        using var response = await _httpClient!.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (!response.IsSuccessStatusCode
            || mediaType is null
            || !mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
            || response.Content.Headers.ContentLength is > 10 * 1024 * 1024)
            return null;
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        if (bytes.Length == 0 || bytes.Length > 10 * 1024 * 1024)
            return null;
        return new GameMediaPayload(assetType, "epic-canonical", identity.ProviderGameId, bytes, mediaType, uri);
    }

    private static string GetContentType(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".webp" => "image/webp",
        _ => "image/jpeg"
    };
}
