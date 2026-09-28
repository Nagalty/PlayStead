using System.Diagnostics;
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
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                Trace.WriteLine(
                    $"[PROVIDER-ACTIVITY-ERROR] Source={source.GetType().FullName} " +
                    $"Provider={source.Provider} Stage=GetAsync " +
                    $"Exception={exception.GetType().FullName} " +
                    $"Message={exception.Message} " +
                    $"Details={exception}");
                continue;
            }

            foreach (var value in values)
            {
                var previous = previousValues
                    .FirstOrDefault(x => x.GameId == value.GameId && x.Provider == value.Provider);
                var merged = Merge(previous, value);
                if (previous is not null &&
                    previous.ProviderGameId == merged.ProviderGameId &&
                    previous.TotalPlaytime == merged.TotalPlaytime &&
                    previous.LastPlayedAtUtc == merged.LastPlayedAtUtc &&
                    previous.Availability == merged.Availability)
                {
                    continue;
                }
                await _store.UpsertAsync(merged, cancellationToken);
                changed = true;
            }
        }
        if (changed)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private static ProviderActivityMetadata Merge(
        ProviderActivityMetadata? previous,
        ProviderActivityMetadata current)
    {
        if (previous is null)
        {
            return current;
        }

        var playtime = current.TotalPlaytime ?? previous.TotalPlaytime;
        var lastPlayed = current.LastPlayedAtUtc ?? previous.LastPlayedAtUtc;
        var availability = current.Availability == ProviderActivityAvailability.Unknown &&
                           (playtime is not null || lastPlayed is not null)
            ? ProviderActivityAvailability.Partial
            : current.Availability;

        return current with
        {
            TotalPlaytime = playtime,
            LastPlayedAtUtc = lastPlayed,
            Availability = availability
        };
    }
}
