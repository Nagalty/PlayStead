using System.ComponentModel;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.Input;
using PlayStead.UI.Library;
using PlayStead.Core.Library;
using PlayStead.Core.Persistence;
using PlayStead.Core.LocalArtifacts;
using System.Collections.ObjectModel;

namespace PlayStead.UI.Settings;

public sealed class SettingsViewModel :
    INotifyPropertyChanged
{
    private readonly UiPreferencesStore _store;
    private bool _reduceMotion;
    private LibraryViewMode _libraryViewMode = LibraryViewMode.Grid;
    private string _librarySortKey = "Title";
    private string? _libraryFilterKey;
    private bool _protectionOnboardingCompleted;
    private bool _autoProtectRecognizedArtifacts;
    private readonly ILocalProtectionSetupService? _protectionService;
    private readonly ILibraryStore? _libraryStore;
    private IReadOnlyList<LocalProtectionGameContext> _protectionGames = [];
    private LocalProtectionInventory _protectionInventory = new([]);
    private int _protectionFailureCount;
    private readonly Dictionary<Guid, bool> _protectionEnabledGameIds = new();
    private string _protectionActionMessage = string.Empty;
    public ObservableCollection<ProtectionGameOption> ProtectionGames { get; } = [];
    private bool _isChoosingProtectionGames;

    public SettingsViewModel(
        UiPreferencesStore store,
        ILocalProtectionSetupService? protectionService = null,
        ILibraryStore? libraryStore = null)
    {
        ArgumentNullException.ThrowIfNull(store);

        _store = store;
        _protectionService = protectionService;
        _libraryStore = libraryStore;

        SaveCommand =
            new AsyncRelayCommand(
                SaveAsync);
        ProtectAllCommand = new AsyncRelayCommand(ProtectAllAsync, () => HasProtectionPrompt);
        ChooseProtectionGamesCommand = new RelayCommand(() => IsChoosingProtectionGames = !IsChoosingProtectionGames);
        RevisitProtectionCommand = new RelayCommand(() => IsChoosingProtectionGames = true);
        LaterProtectionCommand = new AsyncRelayCommand(MarkProtectionOnboardingCompletedAsync);
    }

    public event PropertyChangedEventHandler?
        PropertyChanged;

    public bool ReduceMotion
    {
        get => _reduceMotion;
        set
        {
            if (_reduceMotion == value)
            {
                return;
            }

            _reduceMotion = value;

            OnPropertyChanged();
        }
    }

    public LibraryViewMode LibraryViewMode
    {
        get => _libraryViewMode;
        set
        {
            if (_libraryViewMode == value)
            {
                return;
            }

            _libraryViewMode = value;
            OnPropertyChanged();
        }
    }

    public IAsyncRelayCommand SaveCommand
    {
        get;
    }

    public IAsyncRelayCommand ProtectAllCommand { get; }
    public IAsyncRelayCommand LaterProtectionCommand { get; }
    public IRelayCommand ChooseProtectionGamesCommand { get; }
    public IRelayCommand RevisitProtectionCommand { get; }
    public bool IsChoosingProtectionGames
    {
        get => _isChoosingProtectionGames;
        private set { if (_isChoosingProtectionGames == value) return; _isChoosingProtectionGames = value; OnPropertyChanged(); }
    }
    public bool HasProtectionPrompt => !ProtectionOnboardingCompleted && _protectionInventory.RecognizedArtifactsCount > 0;
    public bool IsProtectionOnboardingPending => !ProtectionOnboardingCompleted;
    public bool IsProtectionSummaryVisible => ProtectionOnboardingCompleted;
    public bool IsProtectionCardVisible => HasProtectionPrompt || IsProtectionSummaryVisible;
    public string ProtectionSummaryLabel => _protectionInventory.RecognizedArtifactsCount == 0
        ? string.Empty
        : $"J’ai trouvé des fichiers importants sur {_protectionInventory.RecognizedGamesCount} jeux.";
    public int PendingProtectionArtifactsCount => _protectionInventory.PendingProtectionArtifactsCount;
    public int ProtectedGamesCount => _protectionInventory.Artifacts
        .Where(x => x.State == LocalProtectionState.Protected && IsProtectionEnabled(x.Artifact.GameId))
        .Select(x => x.Artifact.GameId)
        .Distinct()
        .Count();
    public int ProtectedSaveArtifactsCount => _protectionInventory.Artifacts.Count(x =>
        x.State == LocalProtectionState.Protected && x.Artifact.Kind == GameLocalArtifactKind.SaveData && IsProtectionEnabled(x.Artifact.GameId));
    public int BaselineOnlyArtifactsCount => _protectionInventory.Artifacts.Count(x => x.State == LocalProtectionState.BaselineOnly);
    public int ProtectionFailureCount => _protectionFailureCount;
    public string ProtectionActionMessage => _protectionActionMessage;
    public string ProtectedGamesSummaryLabel =>
        ProtectionFailureCount > 0
            ? $"J’ai protégé {ProtectedGamesCount} jeux sur {_protectionInventory.RecognizedGamesCount}."
            : $"{ProtectedGamesCount} jeux protégés";
    public string ProtectedArtifactsSummaryLabel =>
        $"{ProtectedSaveArtifactsCount} sauvegardes protégées\n{Math.Max(0, _protectionInventory.Artifacts.Count(x => x.State == LocalProtectionState.Protected && IsProtectionEnabled(x.Artifact.GameId)) - ProtectedSaveArtifactsCount)} autres dossiers surveillés";
    public string ProtectionFailureSummaryLabel =>
        ProtectionFailureCount > 0
            ? "Un dossier m’a résisté, on pourra regarder ça."
            : string.Empty;

    private bool IsProtectionEnabled(GameId gameId) =>
        !_protectionEnabledGameIds.TryGetValue(gameId.Value, out var enabled) || enabled;

    public bool ProtectionOnboardingCompleted
    {
        get => _protectionOnboardingCompleted;
        set { if (_protectionOnboardingCompleted == value) return; _protectionOnboardingCompleted = value; OnPropertyChanged(); }
    }

    public bool AutoProtectRecognizedArtifacts
    {
        get => _autoProtectRecognizedArtifacts;
        set { if (_autoProtectRecognizedArtifacts == value) return; _autoProtectRecognizedArtifacts = value; OnPropertyChanged(); }
    }

    public async Task LoadAsync(
        CancellationToken cancellationToken)
    {
        var preferences =
            await _store.LoadAsync(
                cancellationToken);

        ReduceMotion = preferences.ReduceMotion;
        LibraryViewMode = preferences.LibraryViewMode;
        _librarySortKey = preferences.LibrarySortKey;
        _libraryFilterKey = preferences.LibraryFilterKey;
        ProtectionOnboardingCompleted = preferences.ProtectionOnboardingCompleted;
        AutoProtectRecognizedArtifacts = preferences.AutoProtectRecognizedArtifacts;
        _protectionEnabledGameIds.Clear();
        foreach (var entry in preferences.LocalProtectionEnabledByGame ?? new Dictionary<Guid, bool>())
            _protectionEnabledGameIds[entry.Key] = entry.Value;
        if (_protectionService is not null && _libraryStore is not null)
        {
            try
            {
                var snapshot = await _libraryStore.LoadSnapshotAsync(cancellationToken);
                _protectionGames = snapshot.Installations
                    .Where(x => x.IsPresent)
                    .GroupBy(x => x.GameId)
                    .Select(x => new LocalProtectionGameContext(x.Key, x.First().Provider, x.First().ExternalId, snapshot.Games.FirstOrDefault(g => g.Id == x.Key)?.Title))
                    .ToArray();
                _protectionInventory = await _protectionService.InspectAsync(_protectionGames, cancellationToken);
                if (ProtectionOnboardingCompleted)
                {
                    foreach (var gameId in _protectionInventory.Artifacts
                        .Where(x => x.State == LocalProtectionState.Protected)
                        .Select(x => x.Artifact.GameId.Value)
                        .Distinct())
                    {
                        _protectionEnabledGameIds.TryAdd(gameId, true);
                    }
                }
                var recognizedGameIds = _protectionInventory.Artifacts
                    .Select(x => x.Artifact.GameId)
                    .ToHashSet();
                _protectionGames = _protectionGames
                    .Where(x => recognizedGameIds.Contains(x.GameId))
                    .ToArray();
                ProtectionGames.Clear();
                foreach (var game in _protectionGames)
                {
                    var isProtected = _protectionInventory.Artifacts.Any(x =>
                        x.Artifact.GameId == game.GameId && x.State == LocalProtectionState.Protected);
                    var isSelected = _protectionEnabledGameIds.TryGetValue(game.GameId.Value, out var enabled)
                        ? enabled
                        : !ProtectionOnboardingCompleted || isProtected;
                    ProtectionGames.Add(new ProtectionGameOption(game, isSelected, ProtectionGameSelectionChangedAsync));
                }
                if (AutoProtectRecognizedArtifacts && _protectionInventory.PendingProtectionArtifactsCount > 0)
                {
                    await _protectionService.ProtectAsync(_protectionGames, cancellationToken);
                    _protectionInventory = await _protectionService.InspectAsync(_protectionGames, cancellationToken);
                }
                if (ProtectionOnboardingCompleted)
                {
                    var migrated = false;
                    foreach (var gameId in _protectionInventory.Artifacts
                        .Where(x => x.State == LocalProtectionState.Protected)
                        .Select(x => x.Artifact.GameId.Value)
                        .Distinct())
                    {
                        if (_protectionEnabledGameIds.ContainsKey(gameId))
                            continue;
                        _protectionEnabledGameIds[gameId] = true;
                        migrated = true;
                    }
                    if (migrated)
                        await SaveAsync(cancellationToken);
                }
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                _protectionGames = [];
                _protectionInventory = new LocalProtectionInventory([]);
            }
            OnPropertyChanged(nameof(ProtectionSummaryLabel));
            OnPropertyChanged(nameof(HasProtectionPrompt));
            OnPropertyChanged(nameof(IsProtectionOnboardingPending));
            OnPropertyChanged(nameof(IsProtectionSummaryVisible));
            OnPropertyChanged(nameof(IsProtectionCardVisible));
            OnPropertyChanged(nameof(PendingProtectionArtifactsCount));
            OnPropertyChanged(nameof(ProtectedGamesCount));
            OnPropertyChanged(nameof(ProtectedSaveArtifactsCount));
            OnPropertyChanged(nameof(BaselineOnlyArtifactsCount));
            OnPropertyChanged(nameof(ProtectionFailureCount));
            OnPropertyChanged(nameof(ProtectedGamesSummaryLabel));
            OnPropertyChanged(nameof(ProtectedArtifactsSummaryLabel));
            OnPropertyChanged(nameof(ProtectionFailureSummaryLabel));
            ProtectAllCommand.NotifyCanExecuteChanged();
        }
    }

    public Task SaveAsync(
        CancellationToken cancellationToken)
    {
        return _store.SaveAsync(
            new UiPreferences(
                ReduceMotion,
                LibraryViewMode,
                _librarySortKey,
                _libraryFilterKey,
                null,
                ProtectionOnboardingCompleted,
                AutoProtectRecognizedArtifacts,
                new Dictionary<Guid, bool>(_protectionEnabledGameIds)),
            cancellationToken);
    }

    private async Task ProtectAllAsync()
    {
        if (_protectionService is null) return;
        var selected = IsChoosingProtectionGames
            ? ProtectionGames.Where(x => x.IsSelected).Select(x => x.Context).ToArray()
            : _protectionGames;
        var result = await _protectionService.ProtectAsync(selected, CancellationToken.None);
        _protectionInventory = await _protectionService.InspectAsync(_protectionGames, CancellationToken.None);
        _protectionFailureCount = result.Failed;
        foreach (var game in selected)
            _protectionEnabledGameIds[game.GameId.Value] = result.Failed == 0 ||
                _protectionInventory.Artifacts.Any(x => x.Artifact.GameId == game.GameId && x.State == LocalProtectionState.Protected);
        ProtectionOnboardingCompleted = true;
        await SaveAsync(CancellationToken.None);
        OnPropertyChanged(nameof(ProtectionSummaryLabel));
        OnPropertyChanged(nameof(HasProtectionPrompt));
        OnPropertyChanged(nameof(IsProtectionOnboardingPending));
        OnPropertyChanged(nameof(IsProtectionSummaryVisible));
        OnPropertyChanged(nameof(IsProtectionCardVisible));
        OnPropertyChanged(nameof(PendingProtectionArtifactsCount));
        OnPropertyChanged(nameof(ProtectedGamesCount));
        OnPropertyChanged(nameof(ProtectedSaveArtifactsCount));
        OnPropertyChanged(nameof(BaselineOnlyArtifactsCount));
        OnPropertyChanged(nameof(ProtectionFailureCount));
        OnPropertyChanged(nameof(ProtectedGamesSummaryLabel));
        OnPropertyChanged(nameof(ProtectedArtifactsSummaryLabel));
        OnPropertyChanged(nameof(ProtectionFailureSummaryLabel));
        ProtectAllCommand.NotifyCanExecuteChanged();
    }

    public sealed class ProtectionGameOption(
        LocalProtectionGameContext context,
        bool isSelected,
        Func<ProtectionGameOption, bool, Task> selectionChanged) : INotifyPropertyChanged
    {
        private bool _isSelected = isSelected;
        public LocalProtectionGameContext Context { get; } = context;
        public string DisplayName => Context.DisplayName ?? Context.ProviderGameId ?? Context.GameId.Value.ToString("D");
        public bool IsProtected => IsSelected;
        public bool CanProtect => true;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value) return;
                var previous = _isSelected;
                _isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsProtected)));
                _ = CompleteSelectionChangeAsync(previous, value);
            }
        }
        private async Task CompleteSelectionChangeAsync(bool previous, bool value)
        {
            try { await selectionChanged(this, value); }
            catch { SetSelectedSilently(previous); }
        }
        internal void SetSelectedSilently(bool value)
        {
            if (_isSelected == value) return;
            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsProtected)));
        }
        public event PropertyChangedEventHandler? PropertyChanged;
    }

    private async Task ProtectionGameSelectionChangedAsync(ProtectionGameOption option, bool enabled)
    {
        if (_protectionService is null)
            return;

        try
        {
            _protectionActionMessage = string.Empty;
            OnPropertyChanged(nameof(ProtectionActionMessage));
            if (enabled)
            {
                var result = await _protectionService.ProtectAsync([option.Context], CancellationToken.None);
                if (result.Failed > 0)
                    throw new InvalidOperationException("Protection failed.");
            }

            _protectionEnabledGameIds[option.Context.GameId.Value] = enabled;
            _protectionInventory = await _protectionService.InspectAsync(_protectionGames, CancellationToken.None);
            await SaveAsync(CancellationToken.None);
            OnPropertyChanged(nameof(ProtectedGamesCount));
            OnPropertyChanged(nameof(ProtectedSaveArtifactsCount));
            OnPropertyChanged(nameof(BaselineOnlyArtifactsCount));
            OnPropertyChanged(nameof(ProtectedGamesSummaryLabel));
            OnPropertyChanged(nameof(ProtectedArtifactsSummaryLabel));
        }
        catch
        {
            _protectionActionMessage = "Je n’ai pas réussi à modifier la protection de ce jeu.";
            OnPropertyChanged(nameof(ProtectionActionMessage));
            throw;
        }
    }

    private async Task MarkProtectionOnboardingCompletedAsync()
    {
        ProtectionOnboardingCompleted = true;
        await SaveAsync(CancellationToken.None);
        OnPropertyChanged(nameof(HasProtectionPrompt));
        ProtectAllCommand.NotifyCanExecuteChanged();
    }

    private void OnPropertyChanged(
        [CallerMemberName]
        string? propertyName = null)
    {
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(
                propertyName));
    }
}
