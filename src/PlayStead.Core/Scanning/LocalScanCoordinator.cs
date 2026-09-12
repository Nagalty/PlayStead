namespace PlayStead.Core.Scanning;

public sealed class LocalScanCoordinator
{
    private readonly IReadOnlyList<ILocalLibrarySource> _sources;

    public LocalScanCoordinator(
        IEnumerable<ILocalLibrarySource> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        _sources = sources.ToArray();
    }

    public async Task<IReadOnlyList<SourceScanResult>> ScanAllAsync(
        CancellationToken cancellationToken)
    {
        var results =
            new List<SourceScanResult>(_sources.Count);

        foreach (var source in _sources)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                results.Add(
                    await source.ScanAsync(cancellationToken));
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                results.Add(
                    SourceScanResult.Failure(
                        source.Provider,
                        DateTimeOffset.UtcNow,
                        ex.GetType().Name,
                        ex.Message));
            }
        }

        return results;
    }
}
