using PlayStead.Core.Library;

namespace PlayStead.Core.ProviderGameMetadata;

public sealed class ProviderGameMetadataReconciliationService
{
    private readonly IProviderGameMetadataStore _store;
    private readonly IReadOnlyList<IProviderGameMetadataSource> _sources;

    public ProviderGameMetadataReconciliationService(
        IProviderGameMetadataStore store,
        IEnumerable<IProviderGameMetadataSource> sources)
    {
        _store = store;
        _sources = sources.ToArray();
    }

    public event EventHandler? Changed;

    public async Task RefreshAsync(LibrarySnapshot snapshot, CancellationToken cancellationToken)
    {
        var previous = await _store.GetAllAsync(cancellationToken);
        var changed = false;
        foreach (var source in _sources)
        {
            IReadOnlyList<ProviderGameMetadataPatch> patches;
            try
            {
                patches = await source.GetAsync(snapshot, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                continue;
            }

            foreach (var patch in patches)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var old = previous.FirstOrDefault(x => x.GameId == patch.GameId && x.Provider == patch.Provider);
                var merged = Merge(old, patch);
                var semanticChanged = old is null || !SemanticEquals(old, merged);
                if (old is not null && !semanticChanged)
                    merged = merged with { RefreshedAtUtc = patch.Availability == ProviderGameMetadataAvailability.Unknown ? old.RefreshedAtUtc : DateTimeOffset.UtcNow };
                await _store.UpsertAsync(merged, cancellationToken);
                if (semanticChanged) changed = true;
            }
        }
        if (changed) Changed?.Invoke(this, EventArgs.Empty);
    }

    private static ProviderGameMetadata Merge(ProviderGameMetadata? old, ProviderGameMetadataPatch patch)
    {
        old ??= ProviderGameMetadata.Create(patch.GameId, patch.Provider, patch.ProviderGameId, DateTimeOffset.UtcNow);
        return old with
        {
            ProviderGameId = patch.ProviderGameId,
            Genres = MergeReference(old.Genres, patch.Genres),
            Categories = MergeReference(old.Categories, patch.Categories),
            Developers = MergeReference(old.Developers, patch.Developers),
            Publishers = MergeReference(old.Publishers, patch.Publishers),
            ReleaseDate = MergeNullable(old.ReleaseDate, patch.ReleaseDate),
            IsFree = MergeNullable(old.IsFree, patch.IsFree),
            SinglePlayer = MergeNullable(old.SinglePlayer, patch.SinglePlayer),
            MultiPlayer = MergeNullable(old.MultiPlayer, patch.MultiPlayer),
            OnlineCoop = MergeNullable(old.OnlineCoop, patch.OnlineCoop),
            LocalCoop = MergeNullable(old.LocalCoop, patch.LocalCoop),
            ShortDescription = MergeReference(old.ShortDescription, patch.ShortDescription),
            OnlineCoopMaxPlayers = MergeNullable(old.OnlineCoopMaxPlayers, patch.OnlineCoopMaxPlayers),
            OnlineMultiplayerMaxPlayers = MergeNullable(old.OnlineMultiplayerMaxPlayers, patch.OnlineMultiplayerMaxPlayers),
            OfflineCoopMaxPlayers = MergeNullable(old.OfflineCoopMaxPlayers, patch.OfflineCoopMaxPlayers),
            OfflineMultiplayerMaxPlayers = MergeNullable(old.OfflineMultiplayerMaxPlayers, patch.OfflineMultiplayerMaxPlayers),
            Availability = patch.Availability == ProviderGameMetadataAvailability.Unknown
                ? old.Availability
                : patch.Availability,
            RefreshedAtUtc = DateTimeOffset.UtcNow
        };
    }

    private static T? MergeNullable<T>(T? old, ProviderField<T> field) where T : struct => field.State switch
    {
        ProviderFieldState.NotReported => old,
        ProviderFieldState.Value => field.Value,
        ProviderFieldState.ExplicitUnknown => default,
        _ => old
    };

    private static T? MergeReference<T>(T? old, ProviderField<T> field) where T : class => field.State switch
    {
        ProviderFieldState.NotReported => old,
        ProviderFieldState.Value => field.Value,
        ProviderFieldState.ExplicitUnknown => null,
        _ => old
    };

    private static bool SemanticEquals(ProviderGameMetadata a, ProviderGameMetadata b) =>
        a.ProviderGameId == b.ProviderGameId &&
        SequenceEqual(a.Genres, b.Genres) && SequenceEqual(a.Categories, b.Categories) &&
        SequenceEqual(a.Developers, b.Developers) && SequenceEqual(a.Publishers, b.Publishers) &&
        a.ReleaseDate == b.ReleaseDate && a.IsFree == b.IsFree &&
        a.SinglePlayer == b.SinglePlayer && a.MultiPlayer == b.MultiPlayer &&
        a.OnlineCoop == b.OnlineCoop && a.LocalCoop == b.LocalCoop &&
        a.OnlineCoopMaxPlayers == b.OnlineCoopMaxPlayers &&
        a.OnlineMultiplayerMaxPlayers == b.OnlineMultiplayerMaxPlayers &&
        a.OfflineCoopMaxPlayers == b.OfflineCoopMaxPlayers &&
        a.OfflineMultiplayerMaxPlayers == b.OfflineMultiplayerMaxPlayers &&
        string.Equals(a.ShortDescription, b.ShortDescription, StringComparison.Ordinal) &&
        a.Availability == b.Availability;

    private static bool SequenceEqual(IReadOnlyList<string>? left, IReadOnlyList<string>? right) =>
        left is null && right is null || left is not null && right is not null && left.SequenceEqual(right, StringComparer.OrdinalIgnoreCase);
}

/// <summary>Online provider metadata reconciliation kept separate from local startup reconciliation.</summary>
public sealed class ProviderGameMetadataOnlineReconciliationService : IProviderGameMetadataProgress
{
    private readonly ProviderGameMetadataReconciliationService _inner;
    private readonly IReadOnlyList<IProviderGameMetadataProgress> _progressSources;
    private ProviderGameMetadataProgress _current = new(false, 0, 0, 0, 0);

    public ProviderGameMetadataOnlineReconciliationService(
        IProviderGameMetadataStore store,
        IEnumerable<IProviderGameMetadataSource> sources)
    {
        var sourceArray = sources.ToArray();
        _inner = new ProviderGameMetadataReconciliationService(store, sourceArray);
        _progressSources = sourceArray.OfType<IProviderGameMetadataProgress>().ToArray();
        foreach (var source in _progressSources)
            source.ProgressChanged += OnProgressChanged;
    }

    public ProviderGameMetadataProgress Current => _current;
    public event EventHandler<ProviderGameMetadataProgress>? ProgressChanged;

    public event EventHandler? Changed
    {
        add => _inner.Changed += value;
        remove => _inner.Changed -= value;
    }

    public Task RefreshAsync(LibrarySnapshot snapshot, CancellationToken cancellationToken) =>
        _inner.RefreshAsync(snapshot, cancellationToken);

    private void OnProgressChanged(object? sender, ProviderGameMetadataProgress progress)
    {
        _current = progress;
        ProgressChanged?.Invoke(this, progress);
    }
}
