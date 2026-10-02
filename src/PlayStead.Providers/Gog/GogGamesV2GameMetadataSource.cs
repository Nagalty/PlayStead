using System.Globalization;
using PlayStead.Core.Library;
using PlayStead.Core.ProviderGameMetadata;

namespace PlayStead.Providers.Gog;

public sealed class GogGamesV2GameMetadataSource : IProviderGameMetadataSource, IProviderGameMetadataProgress
{
    private const int MaxConcurrency = 4;
    private static readonly TimeSpan RefreshAge = TimeSpan.FromDays(1);
    private readonly IGogGamesV2Client _client;
    private readonly IProviderGameMetadataStore _store;
    private readonly TimeProvider _timeProvider;
    private ProviderGameMetadataProgress _progress = new(false, 0, 0, 0, 0);

    public GogGamesV2GameMetadataSource(IGogGamesV2Client client, IProviderGameMetadataStore store, TimeProvider? timeProvider = null) =>
        (_client, _store, _timeProvider) = (client, store, timeProvider ?? TimeProvider.System);

    public ProviderKind Provider => ProviderKind.Gog;
    public ProviderGameMetadataProgress Current => _progress;
    public event EventHandler<ProviderGameMetadataProgress>? ProgressChanged;

    public async Task<IReadOnlyList<ProviderGameMetadataPatch>> GetAsync(LibrarySnapshot snapshot, CancellationToken cancellationToken)
    {
        var persisted = (await _store.GetAllAsync(cancellationToken).ConfigureAwait(false))
            .Where(x => x.Provider == ProviderKind.Gog)
            .ToDictionary(x => (x.GameId, x.Provider));
        var targets = snapshot.Installations
            .Where(x => x.IsPresent && x.Provider == ProviderKind.Gog && ulong.TryParse(x.ExternalId, NumberStyles.None, CultureInfo.InvariantCulture, out _))
            .GroupBy(x => x.ExternalId, StringComparer.Ordinal)
            .SelectMany(group => group.Select(x => (x.GameId, ProductId: group.Key)))
            .Where(x => !persisted.TryGetValue((x.GameId, ProviderKind.Gog), out var existing) || existing.ProviderGameId != x.ProductId || NeedsRefresh(existing!))
            .GroupBy(x => x.ProductId, StringComparer.Ordinal)
            .ToArray();

        Publish(new(true, targets.Length, 0, 0, 0));
        var succeeded = 0;
        var failed = 0;
        var completed = 0;
        using var gate = new SemaphoreSlim(MaxConcurrency, MaxConcurrency);
        var tasks = targets.Select(async group =>
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                GogGamesV2Details? details;
                try { details = await _client.GetAsync(group.Key, cancellationToken).ConfigureAwait(false); }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                catch { Interlocked.Increment(ref failed); PublishCompleted(); return []; }
                if (details is null) { Interlocked.Increment(ref failed); PublishCompleted(); return []; }
                Interlocked.Increment(ref succeeded);
                PublishCompleted();
                var patches = group.Select(target => ToPatch(details, target.GameId)).ToArray();
                return patches;
            }
            finally { gate.Release(); }
        }).ToArray();
        var result = (await Task.WhenAll(tasks).ConfigureAwait(false)).SelectMany(x => x).ToArray();
        Publish(new(false, targets.Length, targets.Length, succeeded, failed));
        return result;

        void PublishCompleted() => Publish(new(true, targets.Length, Interlocked.Increment(ref completed), Volatile.Read(ref succeeded), Volatile.Read(ref failed)));
    }

    private bool NeedsRefresh(ProviderGameMetadata metadata) => _timeProvider.GetUtcNow() - metadata.RefreshedAtUtc >= RefreshAge || metadata.Availability != ProviderGameMetadataAvailability.Complete || metadata.Genres is null || metadata.Developers is null || metadata.Publishers is null || metadata.ReleaseDate is null;

    private static ProviderGameMetadataPatch ToPatch(GogGamesV2Details details, GameId gameId) =>
        new(gameId, ProviderKind.Gog, details.ProductId,
            Field(details.Tags), ProviderField<IReadOnlyList<string>>.NotReported, Field(details.Developers), Field(details.Publishers),
            details.ReleaseDate is { } date ? ProviderField<DateOnly>.FromValue(date) : ProviderField<DateOnly>.NotReported,
            ProviderField<bool>.NotReported,
            details.SinglePlayer is true ? ProviderField<bool>.FromValue(true) : ProviderField<bool>.NotReported,
            ProviderField<bool>.NotReported, ProviderField<bool>.NotReported, ProviderField<bool>.NotReported,
            details.Tags is not null && details.Developers is not null && details.Publishers is not null && details.ReleaseDate is not null ? ProviderGameMetadataAvailability.Complete : ProviderGameMetadataAvailability.Partial,
            details.Description is null ? ProviderField<string>.NotReported : ProviderField<string>.FromValue(details.Description));

    private static ProviderField<IReadOnlyList<string>> Field(IReadOnlyList<string>? value) => value is null ? ProviderField<IReadOnlyList<string>>.NotReported : ProviderField<IReadOnlyList<string>>.FromValue(value);
    private void Publish(ProviderGameMetadataProgress progress) { _progress = progress; ProgressChanged?.Invoke(this, progress); }
}
