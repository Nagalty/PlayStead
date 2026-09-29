using PlayStead.Core.Library;
using PlayStead.Core.GameBuildHistory;

namespace PlayStead.Core.ProviderInstallUpdate;

public sealed class ProviderInstallUpdateStateReconciliationService
{
    private readonly IReadOnlyList<IProviderInstallUpdateStateSource> _sources;
    private readonly GameBuildHistoryService? _gameBuildHistory;
    private readonly IProviderInstallUpdateProtectionService? _protection;
    private readonly Dictionary<(GameId GameId, ProviderKind Provider), ProviderInstallUpdateState> _current = [];

    public ProviderInstallUpdateStateReconciliationService(
        IEnumerable<IProviderInstallUpdateStateSource> sources,
        GameBuildHistoryService? gameBuildHistory = null,
        IProviderInstallUpdateProtectionService? protection = null)
    {
        ArgumentNullException.ThrowIfNull(sources);
        _sources = sources.ToArray();
        _gameBuildHistory = gameBuildHistory;
        _protection = protection;
    }

    public event EventHandler? Changed;

    public IReadOnlyList<ProviderInstallUpdateState> GetAll() =>
        _current.Values
            .OrderBy(x => x.Provider)
            .ThenBy(x => x.ProviderGameId, StringComparer.Ordinal)
            .ToArray();

    public async Task RefreshAsync(
        IReadOnlyCollection<GameInstallation> installations,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(installations);
        var changed = false;

        foreach (var source in _sources)
        {
            var scoped = installations
                .Where(x => x.Provider == source.Provider && x.IsPresent)
                .ToArray();

            IReadOnlyList<ProviderInstallUpdateState> values;
            try
            {
                values = await source.GetAsync(scoped, cancellationToken);
            }
            catch
            {
                continue;
            }

            foreach (var value in values)
            {
                if (_gameBuildHistory is not null && !string.IsNullOrWhiteSpace(value.InstalledBuildId))
                {
                    try
                    {
                        await _gameBuildHistory.AppendIfChangedAsync(
                            new GameBuildObservation(
                                value.GameId,
                                value.Provider,
                                value.ProviderGameId,
                                value.InstalledBuildId,
                                value.ObservedAtUtc),
                            cancellationToken);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch
                    {
                        // A history write must not prevent install/update state refresh.
                    }
                }

                var key = (value.GameId, value.Provider);
                if (_current.TryGetValue(key, out var previous) && SemanticEquals(previous, value))
                {
                    _current[key] = value;
                    continue;
                }

                _current[key] = value;
                changed = true;
                if (_protection is not null && value.Status == ProviderInstallUpdateStatus.UpdateAvailable)
                {
                    try
                    {
                        await _protection.ProtectBeforeUpdateAsync(value, cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch
                    {
                        // Protection failure is surfaced through its attention notification.
                    }
                }
            }

            var validGameIds = scoped.Select(x => x.GameId).ToHashSet();
            foreach (var key in _current.Keys
                         .Where(x => x.Provider == source.Provider && !validGameIds.Contains(x.GameId))
                         .ToArray())
            {
                _current.Remove(key);
                changed = true;
            }
        }

        if (changed)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private static bool SemanticEquals(
        ProviderInstallUpdateState left,
        ProviderInstallUpdateState right) =>
        left with { ObservedAtUtc = default } == right with { ObservedAtUtc = default };
}
