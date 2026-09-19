using PlayStead.Core.Library;
using PlayStead.Core.Persistence;

namespace PlayStead.Providers.Steam;

public sealed class SteamLocalCatalogBootstrapper
{
    private readonly ISteamLocalCatalogImportSource _source;
    private readonly ICanonicalCatalogWriter _writer;

    public SteamLocalCatalogBootstrapper(
        ISteamLocalCatalogImportSource source,
        ICanonicalCatalogWriter writer)
    {
        _source = source;
        _writer = writer;
    }

    public async Task RunAsync(
        LibrarySnapshot snapshot,
        DateTimeOffset observedAtUtc,
        CancellationToken cancellationToken,
        Action<SteamCatalogBootstrapProgress>? report = null)
    {
        var items = _source.CreateItems(snapshot, observedAtUtc);
        if (items.Count == 0)
            return;

        var total = items.Count;
        var current = 0;
        foreach (var item in items)
        {
            try
            {
                await _writer.ImportSteamAsync([item], cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                // A failed catalog item does not prevent processing the rest of the batch.
            }
            finally
            {
                current++;
                try { report?.Invoke(new(current, total)); } catch { }
            }
        }
    }

}
