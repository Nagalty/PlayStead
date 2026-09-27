using PlayStead.Core.Library;

namespace PlayStead.Core.ProviderActivity;

public sealed class ProviderActivityReconciliationService
{
    private readonly IProviderActivityMetadataStore _store;
    private readonly IReadOnlyList<IProviderActivityMetadataSource> _sources;

    public ProviderActivityReconciliationService(
        IProviderActivityMetadataStore store,
        IEnumerable<IProviderActivityMetadataSource> sources)
    {
        _store = store;
        _sources = sources.ToArray();
    }

    public event EventHandler? Changed;

    public async Task RefreshAsync(
        LibrarySnapshot snapshot,
        CancellationToken cancellationToken)
    {
        var changed = false;
        var previousValues = await _store.GetAllAsync(cancellationToken);
        foreach (var source in _sources)
        {
            var installations = snapshot.Installations
                .Where(x => x.Provider == source.Provider && x.IsPresent)
                .ToArray();
            IReadOnlyList<ProviderActivityMetadata> values;
            try
            {
                values = await source.GetAsync(installations, cancellationToken);
            }
            catch
            {
                continue;
            }

            foreach (var value in values)
            {
                var previous = previousValues
                    .FirstOrDefault(x => x.GameId == value.GameId && x.Provider == value.Provider);
                if (previous is not null &&
                    previous.ProviderGameId == value.ProviderGameId &&
                    previous.TotalPlaytime == value.TotalPlaytime &&
                    previous.LastPlayedAtUtc == value.LastPlayedAtUtc &&
                    previous.Availability == value.Availability)
                {
                    continue;
                }
                await _store.UpsertAsync(value, cancellationToken);
                changed = true;
            }
        }
        if (changed)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
