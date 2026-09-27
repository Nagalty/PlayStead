using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;

namespace PlayStead.Providers.Steam;

public sealed class SteamStoreAppDetailsClient : ISteamStoreAppDetailsClient
{
    private readonly HttpClient _httpClient;
    private readonly ConcurrentDictionary<string, Lazy<Task<SteamStoreAppDetails?>>> _inflight = new(StringComparer.Ordinal);
    public SteamStoreAppDetailsClient(HttpClient httpClient) => _httpClient = httpClient;
    public async Task<SteamStoreAppDetails?> GetAsync(string appId, CancellationToken cancellationToken)
    {
        if (!uint.TryParse(appId, NumberStyles.None, CultureInfo.InvariantCulture, out _)) return null;
        var created = false;
        var lazy = _inflight.GetOrAdd(appId, id =>
        {
            created = true;
            return new Lazy<Task<SteamStoreAppDetails?>>(() => FetchAsync(id), LazyThreadSafetyMode.ExecutionAndPublication);
        });
        System.Diagnostics.Trace.WriteLine($"[STARTUP-STORE] AppDetails AppId={appId} InFlightReuse={(!created).ToString().ToLowerInvariant()}");
        try { return await lazy.Value.WaitAsync(cancellationToken).ConfigureAwait(false); }
        finally { _inflight.TryRemove(new KeyValuePair<string, Lazy<Task<SteamStoreAppDetails?>>>(appId, lazy)); }
    }
    private async Task<SteamStoreAppDetails?> FetchAsync(string appId)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(7));
            using var response = await _httpClient.GetAsync($"https://store.steampowered.com/api/appdetails?appids={Uri.EscapeDataString(appId)}&l=french&cc=fr", cts.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return null;
            await using var stream = await response.Content.ReadAsStreamAsync(cts.Token).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cts.Token).ConfigureAwait(false);
            return SteamStoreAppDetails.Parse(document.RootElement, appId);
        }
        catch (OperationCanceledException)
        {
            System.Diagnostics.Trace.WriteLine($"[STARTUP-STORE] AppDetails AppId={appId} TimeoutOrCanceled=true");
            return null;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException)
        {
            System.Diagnostics.Trace.WriteLine($"[STARTUP-STORE] AppDetails AppId={appId} Failed={ex.GetType().Name}");
            return null;
        }
    }
}
