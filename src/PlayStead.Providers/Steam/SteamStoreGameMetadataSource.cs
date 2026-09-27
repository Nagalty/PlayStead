using PlayStead.Core.Library;
using PlayStead.Core.ProviderGameMetadata;

namespace PlayStead.Providers.Steam;

public sealed class SteamStoreGameMetadataSource : IProviderGameMetadataSource, IProviderGameMetadataProgress
{
    private const int MaxConcurrency = 4;
    private static readonly TimeSpan RefreshAge = TimeSpan.FromDays(7);
    private readonly ISteamStoreAppDetailsClient _client;
    private readonly IProviderGameMetadataStore _store;
    private readonly TimeProvider _timeProvider;
    private readonly ISteamEligibleInstallationSnapshot? _eligibleSnapshot;
    private ProviderGameMetadataProgress _progress = new(false, 0, 0, 0, 0);
    public SteamStoreGameMetadataSource(
        ISteamStoreAppDetailsClient client,
        IProviderGameMetadataStore store,
        TimeProvider? timeProvider = null,
        ISteamEligibleInstallationSnapshot? eligibleSnapshot = null) =>
        (_client, _store, _timeProvider, _eligibleSnapshot) =
        (client, store, timeProvider ?? TimeProvider.System, eligibleSnapshot);
    public ProviderKind Provider => ProviderKind.Steam;
    public ProviderGameMetadataProgress Current => _progress;
    public event EventHandler<ProviderGameMetadataProgress>? ProgressChanged;
    public async Task<IReadOnlyList<ProviderGameMetadataPatch>> GetAsync(LibrarySnapshot snapshot, CancellationToken cancellationToken)
    {
        var persisted = (await _store.GetAllAsync(cancellationToken)).Where(x => x.Provider == ProviderKind.Steam).ToDictionary(x => (x.GameId, x.Provider), x => x);
        var eligibleIds = _eligibleSnapshot?.EligibleExternalIds;
        var installations = snapshot.Installations
            .Where(x => x.Provider == ProviderKind.Steam && x.IsPresent)
            .Where(x => eligibleIds is null || eligibleIds.Contains(x.ExternalId))
            .ToArray();
        var candidates = installations.Where(x => uint.TryParse(x.ExternalId, out _)).Where(x => !persisted.TryGetValue((x.GameId, ProviderKind.Steam), out var metadata) || NeedsRefresh(metadata)).ToArray();
        var candidateGroups = candidates.GroupBy(x => x.ExternalId, StringComparer.Ordinal).ToArray();
        System.Diagnostics.Trace.WriteLine($"[STARTUP-STORE] Considered={installations.Length} Eligible={candidateGroups.Length} Mode=BOUNDED_PARALLEL MaxConcurrency={MaxConcurrency}");
        PublishProgress(new ProviderGameMetadataProgress(candidateGroups.Length > 0, candidateGroups.Length, 0, 0, 0));
        var requested = 0;
        var successful = 0;
        var failed = 0;
        var completedCount = 0;
        using var gate = new SemaphoreSlim(MaxConcurrency, MaxConcurrency);
        var tasks = candidateGroups.Select(async group =>
        {
            Interlocked.Increment(ref requested);
            await gate.WaitAsync(cancellationToken);
            try
            {
                SteamStoreAppDetails? details;
                try
                {
                    details = await _client.GetAsync(group.Key, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch
                {
                    Interlocked.Increment(ref failed);
                    PublishCompleted(false);
                    return [];
                }

                if (details is null)
                {
                    Interlocked.Increment(ref failed);
                    PublishCompleted(false);
                    return [];
                }

                Interlocked.Increment(ref successful);
                PublishCompleted(true);
                return group.Select(installation => ToPatch(details, installation.GameId)).ToArray();
            }
            finally
            {
                gate.Release();
            }
        }).ToArray();
        var result = (await Task.WhenAll(tasks)).SelectMany(x => x).ToArray();
        System.Diagnostics.Trace.WriteLine($"[STARTUP-STORE] Requested={requested} Successful={successful} Failed={failed} Timeout=see-client-trace");
        PublishProgress(new ProviderGameMetadataProgress(false, candidateGroups.Length, candidateGroups.Length, successful, failed));
        return result;

        void PublishCompleted(bool succeeded)
        {
            var completed = Interlocked.Increment(ref completedCount);
            var succeededCount = Volatile.Read(ref successful);
            var failedCount = Volatile.Read(ref failed);
            PublishProgress(new ProviderGameMetadataProgress(true, candidateGroups.Length, completed, succeededCount, failedCount));
        }
    }

    private void PublishProgress(ProviderGameMetadataProgress progress)
    {
        _progress = progress;
        ProgressChanged?.Invoke(this, progress);
    }
    private bool NeedsRefresh(ProviderGameMetadata metadata) => _timeProvider.GetUtcNow() - metadata.RefreshedAtUtc >= RefreshAge || metadata.Availability != ProviderGameMetadataAvailability.Complete || metadata.Genres is null || metadata.Categories is null || metadata.Developers is null || metadata.Publishers is null || metadata.ReleaseDate is null;
    private static ProviderGameMetadataPatch ToPatch(SteamStoreAppDetails d, GameId gameId)
    {
        var genres = Field(d.Genres);
        var categories = Field(d.Categories);
        var developers = Field(d.Developers);
        var publishers = Field(d.Publishers);
        var release = d.ReleaseDate is { } date ? ProviderField<DateOnly>.FromValue(date) : ProviderField<DateOnly>.NotReported;
        var ids = d.Categories ?? [];
        bool Has(params string[] values) => ids.Any(x => values.Any(v => string.Equals(x, v, StringComparison.OrdinalIgnoreCase)));
        return new ProviderGameMetadataPatch(gameId, ProviderKind.Steam, d.SteamAppId.ToString(), genres, categories, developers, publishers, release,
            d.IsFree is { } free ? ProviderField<bool>.FromValue(free) : ProviderField<bool>.NotReported,
            Has("Solo", "Single-player") ? ProviderField<bool>.FromValue(true) : ProviderField<bool>.NotReported,
            Has("Multijoueur", "Multi-player") ? ProviderField<bool>.FromValue(true) : ProviderField<bool>.NotReported,
            Has("Coopération en ligne", "Online Co-op") ? ProviderField<bool>.FromValue(true) : ProviderField<bool>.NotReported,
            Has("Coopération locale", "Local Co-op", "Shared/Split Screen Co-op") ? ProviderField<bool>.FromValue(true) : ProviderField<bool>.NotReported,
            genres.State == ProviderFieldState.Value && categories.State == ProviderFieldState.Value && developers.State == ProviderFieldState.Value && publishers.State == ProviderFieldState.Value && release.State == ProviderFieldState.Value ? ProviderGameMetadataAvailability.Complete : ProviderGameMetadataAvailability.Partial,
            d.ShortDescription is null ? ProviderField<string>.NotReported : ProviderField<string>.FromValue(d.ShortDescription));
    }
    private static ProviderField<IReadOnlyList<string>> Field(IReadOnlyList<string>? value) => value is null ? ProviderField<IReadOnlyList<string>>.NotReported : ProviderField<IReadOnlyList<string>>.FromValue(value);
}
