using PlayStead.Core.Media;

namespace PlayStead.Providers.Steam.Media;

public sealed class HttpSteamMediaTransport : ISteamMediaTransport
{
    private const long MaxMediaBytes = 25L * 1024 * 1024;
    private const int BufferSize = 64 * 1024;

    private readonly HttpClient _httpClient;
    private readonly TimeSpan _timeout;

    public HttpSteamMediaTransport(HttpClient httpClient)
        : this(httpClient, TimeSpan.FromSeconds(10))
    {
    }

    public HttpSteamMediaTransport(
        HttpClient httpClient,
        TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(httpClient);

        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        _httpClient = httpClient;
        _timeout = timeout;
    }

    public async Task<GameMediaPayload?> TryDownloadAsync(
        string appId,
        GameMediaAssetType assetType,
        IReadOnlyList<Uri> candidates,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);
        ArgumentNullException.ThrowIfNull(candidates);
        cancellationToken.ThrowIfCancellationRequested();

        foreach (var candidate in candidates)
        {
            using var timeoutCts =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            timeoutCts.CancelAfter(_timeout);

            try
            {
                using var response = await _httpClient.GetAsync(
                        candidate,
                        HttpCompletionOption.ResponseHeadersRead,
                        timeoutCts.Token)
                    .ConfigureAwait(false);

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    ReportCandidate(appId, assetType, candidate, "404");
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    ReportCandidate(appId, assetType, candidate, $"HTTP{(int)response.StatusCode}");
                    return null;
                }

                if (response.Content.Headers.ContentLength is long contentLength &&
                    contentLength > MaxMediaBytes)
                {
                    continue;
                }

                var content = await ReadBoundedAsync(
                        response.Content,
                        timeoutCts.Token)
                    .ConfigureAwait(false);

                if (content is null)
                {
                    ReportCandidate(appId, assetType, candidate, "InvalidSize");
                    continue;
                }

                ReportCandidate(appId, assetType, candidate, "Success");
                return new GameMediaPayload(
                    assetType,
                    "steam-remote",
                    appId,
                    content,
                    response.Content.Headers.ContentType?.MediaType,
                    response.RequestMessage?.RequestUri ?? candidate);
            }
            catch (OperationCanceledException)
                when (timeoutCts.IsCancellationRequested &&
                      !cancellationToken.IsCancellationRequested)
            {
                ReportCandidate(appId, assetType, candidate, "Timeout");
                continue;
            }
        }

        return null;
    }

    private static void ReportCandidate(
        string appId,
        GameMediaAssetType assetType,
        Uri candidate,
        string result)
    {
        var name = Path.GetFileName(candidate.AbsolutePath);
        System.Diagnostics.Trace.WriteLine(
            $"[STEAM-MEDIA] AppId={appId} Asset={assetType} Source=Remote Candidate={name} Result={result}");
    }

    private static async Task<byte[]?> ReadBoundedAsync(
        HttpContent content,
        CancellationToken cancellationToken)
    {
        await using var source = await content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);

        using var destination = new MemoryStream();
        var buffer = new byte[BufferSize];
        long totalRead = 0;

        while (true)
        {
            var remainingAllowed = (MaxMediaBytes + 1) - totalRead;
            if (remainingAllowed <= 0)
            {
                return null;
            }

            var requested = (int)Math.Min(
                buffer.Length,
                remainingAllowed);

            var read = await source.ReadAsync(
                    buffer.AsMemory(0, requested),
                    cancellationToken)
                .ConfigureAwait(false);

            if (read == 0)
            {
                return destination.ToArray();
            }

            totalRead += read;

            if (totalRead > MaxMediaBytes)
            {
                return null;
            }

            await destination.WriteAsync(
                    buffer.AsMemory(0, read),
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
