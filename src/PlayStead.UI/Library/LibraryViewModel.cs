using System.ComponentModel;
using System.Runtime.CompilerServices;
using PlayStead.Core.Library;
using PlayStead.Core.Persistence;
using PlayStead.Core.Sessions;
using PlayStead.Core.Steam;
using PlayStead.UI.Sessions;
using PlayStead.UI.Steam;

namespace PlayStead.UI.Library;

public sealed class LibraryViewModel :
    INotifyPropertyChanged
{
    private readonly ILibraryStore _libraryStore;
    private readonly ISteamReferenceRuntime? _steamReferenceRuntime;
    private readonly object _verifySteamGate = new();

    private SessionMonitor? _sessionMonitor;

    private IReadOnlyList<LibraryItemViewModel> _items =
        Array.Empty<LibraryItemViewModel>();

    private bool _isSteamChecking;
    private Task? _verifySteamTask;

    public LibraryViewModel(
        ILibraryStore libraryStore)
    {
        ArgumentNullException.ThrowIfNull(libraryStore);

        _libraryStore = libraryStore;
    }

    public LibraryViewModel(
        ILibraryStore libraryStore,
        ISteamReferenceRuntime steamReferenceRuntime)
        : this(libraryStore)
    {
        ArgumentNullException.ThrowIfNull(
            steamReferenceRuntime);

        _steamReferenceRuntime =
            steamReferenceRuntime;
    }

    public LibraryViewModel(
        ILibraryStore libraryStore,
        SessionMonitor sessionMonitor)
        : this(libraryStore)
    {
        AttachSessionMonitor(
            sessionMonitor);
    }

    public LibraryViewModel(
        ILibraryStore libraryStore,
        ISteamReferenceRuntime steamReferenceRuntime,
        SessionMonitor sessionMonitor)
        : this(
            libraryStore,
            steamReferenceRuntime)
    {
        AttachSessionMonitor(
            sessionMonitor);
    }

    public event PropertyChangedEventHandler?
        PropertyChanged;

    public IReadOnlyList<LibraryItemViewModel> Items
    {
        get => _items;
        private set
        {
            if (ReferenceEquals(
                    _items,
                    value))
            {
                return;
            }

            _items = value;

            OnPropertyChanged();
            OnPropertyChanged(
                nameof(HasItems));
        }
    }

    public bool HasItems =>
        Items.Count > 0;

    public bool IsSteamChecking
    {
        get => _isSteamChecking;
        private set
        {
            if (_isSteamChecking == value)
            {
                return;
            }

            _isSteamChecking = value;

            OnPropertyChanged();
            OnPropertyChanged(
                nameof(CanVerifySteam));
        }
    }

    public bool CanVerifySteam =>
        _steamReferenceRuntime is not null &&
        !IsSteamChecking;

    public async Task RefreshAsync(
        CancellationToken cancellationToken)
    {
        var snapshot =
            await _libraryStore.LoadSnapshotAsync(
                cancellationToken);

        Items =
            BuildItems(
                snapshot,
                checking: false);
    }

    public Task VerifySteamAsync(
        CancellationToken cancellationToken)
    {
        if (_steamReferenceRuntime is null)
        {
            return Task.CompletedTask;
        }

        lock (_verifySteamGate)
        {
            if (_verifySteamTask is
                {
                    IsCompleted: false
                })
            {
                return _verifySteamTask;
            }

            _verifySteamTask =
                VerifySteamCoreAsync(
                    cancellationToken);

            return _verifySteamTask;
        }
    }

    private async Task VerifySteamCoreAsync(
        CancellationToken cancellationToken)
    {
        var runtime =
            _steamReferenceRuntime
            ?? throw new InvalidOperationException(
                "Steam reference runtime is unavailable.");

        IsSteamChecking = true;

        Items =
            Items
                .Select(
                    item =>
                        item.Provider ==
                            ProviderKind.Steam
                            ? item with
                            {
                                SteamState =
                                    SteamUpdateState.Checking
                            }
                            : item)
                .ToArray();

        try
        {
            await runtime.RefreshAllAsync(
                cancellationToken);

            await RefreshAsync(
                cancellationToken);
        }
        finally
        {
            IsSteamChecking = false;
        }
    }

    private IReadOnlyList<LibraryItemViewModel>
        BuildItems(
            LibrarySnapshot snapshot,
            bool checking)
    {
        var presentInstallations =
            snapshot.Installations
                .Where(
                    installation =>
                        installation.IsPresent)
                .GroupBy(
                    installation =>
                        installation.GameId)
                .ToDictionary(
                    group =>
                        group.Key,
                    group =>
                        group
                            .OrderByDescending(
                                installation =>
                                    installation.IsPreferred)
                            .ThenBy(
                                installation =>
                                    installation.Provider)
                            .ThenBy(
                                installation =>
                                    installation.InstallPath,
                                StringComparer.OrdinalIgnoreCase)
                            .First());

        var steamStates =
            (_steamReferenceRuntime?.Current.Entries
                ?? Array.Empty<SteamReferenceEntry>())
            .GroupBy(
                entry =>
                    entry.AppId,
                StringComparer.Ordinal)
            .ToDictionary(
                group =>
                    group.Key,
                group =>
                    group.First().Evaluation.State,
                StringComparer.Ordinal);

        var activeGameIds =
            ActiveGameIds(
                _sessionMonitor?.LatestSnapshot);

        return snapshot.Games
            .Where(
                game =>
                    presentInstallations.ContainsKey(
                        game.Id))
            .Select(
                game =>
                {
                    var installation =
                        presentInstallations[
                            game.Id];

                    SteamUpdateState? steamState =
                        null;

                    if (installation.Provider ==
                        ProviderKind.Steam)
                    {
                        if (checking)
                        {
                            steamState =
                                SteamUpdateState.Checking;
                        }
                        else if (!steamStates.TryGetValue(
                                     installation.ExternalId,
                                     out var foundState))
                        {
                            steamState =
                                SteamUpdateState.Unknown;
                        }
                        else
                        {
                            steamState =
                                foundState;
                        }
                    }

                    return new LibraryItemViewModel(
                        game.Id,
                        game.Title,
                        installation.Provider,
                        ProviderLabel(
                            installation.Provider),
                        installation.InstallPath,
                        installation.InstalledSizeBytes,
                        steamState,
                        activeGameIds.Contains(
                            game.Id));
                })
            .OrderBy(
                item =>
                    item.Title,
                StringComparer
                    .CurrentCultureIgnoreCase)
            .ToArray();
    }

    private void AttachSessionMonitor(
        SessionMonitor sessionMonitor)
    {
        ArgumentNullException.ThrowIfNull(
            sessionMonitor);

        _sessionMonitor =
            sessionMonitor;

        _sessionMonitor.SnapshotUpdated +=
            SessionMonitor_OnSnapshotUpdated;
    }

    private void SessionMonitor_OnSnapshotUpdated(
        SessionRuntimeSnapshot snapshot)
    {
        var activeGameIds =
            ActiveGameIds(
                snapshot);

        Items =
            Items
                .Select(
                    item =>
                        item with
                        {
                            IsSessionActive =
                                activeGameIds.Contains(
                                    item.GameId)
                        })
                .ToArray();
    }

    private static HashSet<GameId> ActiveGameIds(
        SessionRuntimeSnapshot? snapshot) =>
        snapshot?.ActiveSessions
            .Select(
                session =>
                    new GameId(
                        session.GameId))
            .ToHashSet()
        ?? [];

    private static string ProviderLabel(
        ProviderKind provider) =>
        provider switch
        {
            ProviderKind.Steam =>
                "Steam",

            ProviderKind.Epic =>
                "Epic",

            ProviderKind.Gog =>
                "GOG",

            ProviderKind.Manual =>
                "Manual",

            _ =>
                provider.ToString()
        };

    private void OnPropertyChanged(
        [CallerMemberName]
        string? propertyName = null) =>
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(
                propertyName));
}
