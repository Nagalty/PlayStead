using System.Diagnostics;
using PlayStead.Core.Catalog;
using PlayStead.Core.Library;
using PlayStead.Core.Media;
using PlayStead.Core.ProviderGameMetadata;
using ProviderMetadata = PlayStead.Core.ProviderGameMetadata.ProviderGameMetadata;

namespace PlayStead.Core.Persistence;

public sealed record ManualMetadataReconciliationResult(
    int Matched,
    int Updated,
    int Skipped);

public interface IManualMetadataReconciliationService
{
    Task<ManualMetadataReconciliationResult> ReconcileAsync(CancellationToken cancellationToken);
}

public sealed class ManualMetadataReconciliationService : IManualMetadataReconciliationService
{
    private readonly ILibraryStore _libraryStore;
    private readonly ICanonicalCatalogStore _catalogStore;
    private readonly IManualMetadataLinkStore _linkStore;
    private readonly IProviderGameMetadataStore _metadataStore;

    public ManualMetadataReconciliationService(
        ILibraryStore libraryStore,
        ICanonicalCatalogStore catalogStore,
        IManualMetadataLinkStore linkStore,
        IProviderGameMetadataStore metadataStore)
    {
        _libraryStore = libraryStore ?? throw new ArgumentNullException(nameof(libraryStore));
        _catalogStore = catalogStore ?? throw new ArgumentNullException(nameof(catalogStore));
        _linkStore = linkStore ?? throw new ArgumentNullException(nameof(linkStore));
        _metadataStore = metadataStore ?? throw new ArgumentNullException(nameof(metadataStore));
    }

    public async Task<ManualMetadataReconciliationResult> ReconcileAsync(CancellationToken cancellationToken)
    {
        var matched = 0;
        var updated = 0;
        var skipped = 0;

        LibrarySnapshot snapshot;
        try
        {
            snapshot = await _libraryStore.LoadSnapshotAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[STARTUP] ManualMetadataReconciliation Matched=0 Updated=0 Skipped=0 Error={ex.GetType().Name}");
            return new(0, 0, 0);
        }

        var titles = snapshot.Games.ToDictionary(x => x.Id);
        foreach (var installation in snapshot.Installations.Where(x => x.Provider == ProviderKind.Manual))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!titles.TryGetValue(installation.GameId, out var game) || string.IsNullOrWhiteSpace(game.Title))
            {
                skipped++;
                continue;
            }

            try
            {
                var normalized = CanonicalCatalogTitleNormalizer.Normalize(game.Title);
                var matches = await _catalogStore.FindByNormalizedTitleAsync(normalized, cancellationToken).ConfigureAwait(false);
                if (matches.Count != 1)
                {
                    skipped++;
                    continue;
                }

                var content = matches[0];
                var refs = await _catalogStore.GetProviderRefsAsync(content.Id, cancellationToken).ConfigureAwait(false);
                var mediaSource = ManualMetadataMediaSourceSelector.Select(refs);
                Trace.WriteLine($"[STARTUP] ManualMetadataReconciliation Title=\"{game.Title}\" Normalized=\"{normalized}\" Matches={matches.Count} MediaSource={(mediaSource is null ? "None" : $"{mediaSource.Provider}:{mediaSource.ExternalId}")}");

                var existing = await _linkStore.GetAsync(installation.GameId, cancellationToken).ConfigureAwait(false);
                var link = new ManualMetadataLink(
                    installation.GameId,
                    content.Id,
                    mediaSource,
                    DateTimeOffset.UtcNow);
                matched++;
                if (existing is null || existing.CanonicalCatalogId != link.CanonicalCatalogId || existing.MediaSource != link.MediaSource)
                {
                    await _linkStore.UpsertAsync(link, cancellationToken).ConfigureAwait(false);
                    updated++;
                }

                await _metadataStore.UpsertAsync(
                    ProviderMetadata.Create(
                        installation.GameId,
                        ProviderKind.Manual,
                        installation.ExternalId,
                        DateTimeOffset.UtcNow,
                        genres: content.Genres,
                        developers: content.Developer is null ? null : [content.Developer],
                        publishers: content.Publisher is null ? null : [content.Publisher],
                        releaseDate: content.ReleaseDate,
                        availability: ProviderGameMetadataAvailability.Partial),
                    cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                skipped++;
                Trace.WriteLine($"[STARTUP] ManualMetadataReconciliation installation={installation.Id} Error={ex.GetType().Name}");
            }
        }

        Trace.WriteLine($"[STARTUP] ManualMetadataReconciliation Matched={matched} Updated={updated} Skipped={skipped}");
        return new(matched, updated, skipped);
    }

}
