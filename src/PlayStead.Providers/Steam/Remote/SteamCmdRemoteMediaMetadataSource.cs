using System.Collections.Concurrent;
using PlayStead.Providers.Steam;

namespace PlayStead.Providers.Steam.Remote;

public sealed class SteamCmdRemoteMediaMetadataSource : ISteamRemoteMediaMetadataSource
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(24);
    private readonly ISteamCmdRunner _runner;
    private readonly SteamCmdAppInfoParser _parser;
    private readonly TimeProvider _timeProvider;
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Lazy<Task<CacheEntry>>> _inFlight = new(StringComparer.Ordinal);

    public SteamCmdRemoteMediaMetadataSource(
        ISteamCmdRunner runner,
        SteamCmdAppInfoParser parser,
        TimeProvider timeProvider)
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<SteamMediaAssetMetadata?> GetAsync(
        string appId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);
        if (!appId.All(char.IsAsciiDigit))
            throw new ArgumentException("Steam AppId must be numeric.", nameof(appId));

        var now = _timeProvider.GetUtcNow();
        if (_cache.TryGetValue(appId, out var cached) && now - cached.ObservedAtUtc < CacheTtl)
            return cached.Metadata;

        var operation = _inFlight.GetOrAdd(
            appId,
            key => new Lazy<Task<CacheEntry>>(
                () => FetchAsync(key, cancellationToken),
                LazyThreadSafetyMode.ExecutionAndPublication));

        try
        {
            return (await operation.Value.ConfigureAwait(false)).Metadata;
        }
        finally
        {
            _inFlight.TryRemove(new KeyValuePair<string, Lazy<Task<CacheEntry>>>(appId, operation));
        }
    }

    private async Task<CacheEntry> FetchAsync(
        string appId,
        CancellationToken cancellationToken)
    {
        SteamMediaAssetMetadata? metadata = null;
        try
        {
            var result = await _runner.RunAsync(
                new SteamCmdRequest(appId),
                cancellationToken).ConfigureAwait(false);

            if (!result.TimedOut && result.ExitCode == 0)
                metadata = _parser.ParseMediaAssets(appId, result.StandardOutput);
        }
        catch (FileNotFoundException)
        {
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (IOException)
        {
        }

        var entry = new CacheEntry(metadata, _timeProvider.GetUtcNow());
        _cache[appId] = entry;
        System.Diagnostics.Trace.WriteLine(
            $"[STEAM-MEDIA-META] AppId={appId} Source=SteamCmd Result={(metadata is null ? "Missing" : "Success")}");
        return entry;
    }

    private sealed record CacheEntry(
        SteamMediaAssetMetadata? Metadata,
        DateTimeOffset ObservedAtUtc);
}
