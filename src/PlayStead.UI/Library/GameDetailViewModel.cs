using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.IO;
using PlayStead.Core.Library;
using PlayStead.Core.Catalog;
using PlayStead.Core.Persistence;
using PlayStead.Core.Sessions;
using PlayStead.UI.Launching;
using PlayStead.UI.Sessions;
using PlayStead.Core.Shortlist;
using PlayStead.Core.ProviderGameMetadata;
using PlayStead.Core.GameBuildHistory;
using CommunityToolkit.Mvvm.Input;
using System.Globalization;

namespace PlayStead.UI.Library;

public sealed class GameDetailViewModel : INotifyPropertyChanged
{
    private readonly SessionMonitor? _sessionMonitor;
    private readonly Func<Task>? _refreshActivityAsync;
    private bool _isActive;
    private readonly ICanonicalCatalogStore? _catalogStore;
    private readonly IGamesDuMomentService? _gamesDuMomentService;
    private readonly IProviderGameMetadataStore? _providerGameMetadataStore;
    private readonly GameBuildHistoryService? _gameBuildHistoryService;
    private bool _isShortlistOperationInProgress;
    public GameDetailViewModel(
        LibraryItemViewModel game)
        : this(game, launch: null, activity: null)
    {
    }

    public GameDetailViewModel(
        LibraryItemViewModel game,
        GameLaunchViewModel? launch)
        : this(game, launch, activity: null)
    {
    }

    public GameDetailViewModel(
        LibraryItemViewModel game,
        GameLaunchViewModel? launch,
        GameQuickPanelViewModel? activity)
        : this(game, launch, activity, heroPath: null)
    {
    }

    public GameDetailViewModel(
        LibraryItemViewModel game,
        GameLaunchViewModel? launch,
        GameQuickPanelViewModel? activity,
        string? heroPath,
        ICanonicalCatalogStore? catalogStore = null,
        IGamesDuMomentService? gamesDuMomentService = null,
        IProviderGameMetadataStore? providerGameMetadataStore = null,
        GameBuildHistoryService? gameBuildHistoryService = null)
    {
        ArgumentNullException.ThrowIfNull(
            game);

        Game =
            game;

        GameId =
            game.GameId;

        Title =
            game.Title;

        DisplayTitle =
            game.Title.ToUpperInvariant();

        ProviderLabel =
            game.ProviderLabel;

        InstallPath =
            game.InstallPath;

        InstallDriveLabel =
            GetDriveLabel(game.InstallPath);

        InstalledSizeLabel =
            game.InstalledSizeLabel;

        SteamStatusLabel =
            game.SteamStatusLabel;

        Launch =
            launch;

        Activity =
            activity;

        HeroPath =
            heroPath;

        _catalogStore = catalogStore;
        _gamesDuMomentService = gamesDuMomentService;
        _providerGameMetadataStore = providerGameMetadataStore;
        _gameBuildHistoryService = gameBuildHistoryService;
        AddToGamesDuMomentCommand = new AsyncRelayCommand(AddToGamesDuMomentAsync, () => CanChangeGamesDuMoment);
        RemoveFromGamesDuMomentCommand = new AsyncRelayCommand(RemoveFromGamesDuMomentAsync, () => CanChangeGamesDuMoment);
    }

    public GameDetailViewModel(
        LibraryItemViewModel game,
        GameLaunchViewModel? launch,
        GameQuickPanelViewModel? activity,
        string? heroPath,
        SessionMonitor sessionMonitor,
        Func<Task>? refreshActivityAsync = null,
        ICanonicalCatalogStore? catalogStore = null,
        IGamesDuMomentService? gamesDuMomentService = null,
        IProviderGameMetadataStore? providerGameMetadataStore = null,
        GameBuildHistoryService? gameBuildHistoryService = null)
        : this(game, launch, activity, heroPath, catalogStore, gamesDuMomentService, providerGameMetadataStore, gameBuildHistoryService)
    {
        ArgumentNullException.ThrowIfNull(sessionMonitor);
        _sessionMonitor = sessionMonitor;
        _refreshActivityAsync = refreshActivityAsync
            ?? (() => LoadAsync(CancellationToken.None));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public LibraryItemViewModel Game { get; private set; }

    public GameId GameId { get; }

    public GameLaunchViewModel? Launch { get; }

    public GameQuickPanelViewModel? Activity { get; }

    public string? HeroPath { get; }

    public bool HasHero =>
        !string.IsNullOrWhiteSpace(HeroPath);

    public bool HasCover =>
        Game.HasCover;

    public string? CoverPath =>
        Game.CoverPath;

    public bool HasInstallPath =>
        !string.IsNullOrWhiteSpace(
            Game.InstallPath);

    public bool HasInstalledSize =>
        Game.InstalledSizeBytes.HasValue;

    public bool HasInstallDrive =>
        !string.IsNullOrWhiteSpace(InstallDriveLabel);

    public bool HasSteamStatus =>
        Game.HasSteamStatus;

    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        await (Activity?.LoadSessionSummaryAsync(cancellationToken) ?? Task.CompletedTask);
        if (_gamesDuMomentService is not null)
        {
            IsInGamesDuMoment = (await _gamesDuMomentService.GetAsync(cancellationToken)).Any(entry => entry.GameId == GameId);
            OnPropertyChanged(nameof(IsInGamesDuMoment));
            OnPropertyChanged(nameof(GamesDuMomentActionLabel));
        }
        if (_providerGameMetadataStore is not null)
            ApplyProviderMetadata(await _providerGameMetadataStore.GetAsync(GameId, Game.Provider, cancellationToken));

        await LoadBuildHistoryAsync(cancellationToken);

        if (_catalogStore is null || Game.CanonicalContentId is not CatalogContentId contentId)
            return;

        var content = await _catalogStore.GetByIdAsync(contentId, cancellationToken);
        DeveloperDisplay = content?.Developer;
        PublisherDisplay = content?.Publisher;
        ReleaseDateDisplay = content?.ReleaseDate?.ToString("d MMMM yyyy", UiDisplayCulture.Current);
        OnPropertyChanged(nameof(DeveloperDisplay));
        OnPropertyChanged(nameof(PublisherDisplay));
        OnPropertyChanged(nameof(ReleaseDateDisplay));
        OnPropertyChanged(nameof(HasDeveloper));
        OnPropertyChanged(nameof(HasPublisher));
        OnPropertyChanged(nameof(HasReleaseDate));
        OnPropertyChanged(nameof(HasGeneralInfo));

    }

    public string Title { get; }

    public string DisplayTitle { get; }

    public string ProviderLabel { get; }

    public string InstallPath { get; }

    public string InstallDriveLabel { get; }

    public string InstalledSizeLabel { get; }

    public string SteamStatusLabel { get; }

    public string? DeveloperDisplay { get; private set; }
    public string? PublisherDisplay { get; private set; }
    public string? ReleaseDateDisplay { get; private set; }
    public IReadOnlyList<string> Genres { get; private set; } = [];
    public IReadOnlyList<string> GameModes { get; private set; } = [];
    public bool HasGenres => Genres.Count > 0;
    public bool HasGameModes => GameModes.Count > 0;

    public bool IsInGamesDuMoment { get; private set; }
    public bool CanChangeGamesDuMoment => _gamesDuMomentService is not null && !_isShortlistOperationInProgress;
    public string GamesDuMomentActionLabel => IsInGamesDuMoment ? "Retirer des jeux du moment" : "Ajouter aux jeux du moment";
    public IAsyncRelayCommand AddToGamesDuMomentCommand { get; }
    public IAsyncRelayCommand RemoveFromGamesDuMomentCommand { get; }

    public sealed record BuildHistoryEntryViewModel(
        string? PreviousBuildId,
        string BuildId,
        DateTimeOffset ObservedAtUtc,
        bool IsBaseline,
        bool IsSinceLastPlay)
    {
        public string ObservedAtLabel => ObservedAtUtc.ToLocalTime().ToString("d MMM yyyy HH:mm", CultureInfo.CurrentCulture);
        public string BuildTransitionLabel => IsBaseline
            ? $"Première version observée · {BuildId}"
            : $"{PreviousBuildId} → {BuildId}";
        public string SinceLastPlayLabel => IsSinceLastPlay ? "Depuis ta dernière partie" : string.Empty;
    }

    public IReadOnlyList<BuildHistoryEntryViewModel> BuildHistory { get; private set; } = [];
    public bool HasBuildHistory => BuildHistory.Count > 0;
    public bool HasBuildChanges => BuildHistory.Any(entry => !entry.IsBaseline);
    public int BuildChangeCountSinceLastPlay => BuildHistory.Count(entry => entry.IsSinceLastPlay);
    public string BuildHistorySummary => BuildHistory.Count == 0
        ? "PlayStead commencera à suivre les versions observées ici."
        : BuildChangeCountSinceLastPlay > 0
            ? $"{BuildChangeCountSinceLastPlay} changement{(BuildChangeCountSinceLastPlay == 1 ? string.Empty : "s")} observé{(BuildChangeCountSinceLastPlay == 1 ? string.Empty : "s")} depuis ta dernière partie"
            : HasBuildChanges ? "Historique des versions observées" : "Pas encore de changement observé.";

    private async Task LoadBuildHistoryAsync(CancellationToken cancellationToken)
    {
        if (_gameBuildHistoryService is null)
        {
            BuildHistory = [];
        }
        else
        {
            var observations = (await _gameBuildHistoryService.GetHistoryAsync(GameId, Game.Provider, cancellationToken))
                .OrderBy(x => x.ObservedAtUtc)
                .ToArray();
            var lastPlay = await _gameBuildHistoryService.GetLastCompletedPlayAtAsync(GameId, cancellationToken);
            BuildHistory = observations
                .Select((observation, index) => new BuildHistoryEntryViewModel(
                    index == 0 ? null : observations[index - 1].BuildId,
                    observation.BuildId,
                    observation.ObservedAtUtc,
                    index == 0,
                    index > 0 && lastPlay is not null && observation.ObservedAtUtc > lastPlay.Value))
                .OrderByDescending(entry => entry.ObservedAtUtc)
                .ToArray();
        }

        OnPropertyChanged(nameof(BuildHistory));
        OnPropertyChanged(nameof(HasBuildHistory));
        OnPropertyChanged(nameof(HasBuildChanges));
        OnPropertyChanged(nameof(BuildChangeCountSinceLastPlay));
        OnPropertyChanged(nameof(BuildHistorySummary));
    }

    private Task AddToGamesDuMomentAsync() => ChangeGamesDuMomentAsync(true);
    private Task RemoveFromGamesDuMomentAsync() => ChangeGamesDuMomentAsync(false);

    private async Task ChangeGamesDuMomentAsync(bool add)
    {
        if (!CanChangeGamesDuMoment) return;
        _isShortlistOperationInProgress = true;
        OnPropertyChanged(nameof(CanChangeGamesDuMoment));
        try
        {
            if (add)
            {
                var result = await _gamesDuMomentService!.AddAsync(GameId, CancellationToken.None);
                if (result == GamesDuMomentAddResult.Full) return;
                IsInGamesDuMoment = true;
            }
            else
            {
                await _gamesDuMomentService!.RemoveAsync(GameId, CancellationToken.None);
                IsInGamesDuMoment = false;
            }
            OnPropertyChanged(nameof(IsInGamesDuMoment));
            OnPropertyChanged(nameof(GamesDuMomentActionLabel));
        }
        finally
        {
            _isShortlistOperationInProgress = false;
            OnPropertyChanged(nameof(CanChangeGamesDuMoment));
        }
    }

    public bool HasDeveloper => !string.IsNullOrWhiteSpace(DeveloperDisplay);
    public bool HasPublisher => !string.IsNullOrWhiteSpace(PublisherDisplay);
    public bool HasReleaseDate => !string.IsNullOrWhiteSpace(ReleaseDateDisplay);
    public bool HasGeneralInfo => HasDeveloper || HasPublisher || HasReleaseDate || HasGenres || HasGameModes;

    private void ApplyProviderMetadata(ProviderGameMetadata? metadata)
    {
        if (metadata is null) return;
        if (!HasDeveloper && metadata.Developers is { Count: > 0 })
        {
            DeveloperDisplay = string.Join(", ", metadata.Developers);
            OnPropertyChanged(nameof(DeveloperDisplay));
            OnPropertyChanged(nameof(HasDeveloper));
        }
        if (!HasPublisher && metadata.Publishers is { Count: > 0 })
        {
            PublisherDisplay = string.Join(", ", metadata.Publishers);
            OnPropertyChanged(nameof(PublisherDisplay));
            OnPropertyChanged(nameof(HasPublisher));
        }
        if (!HasReleaseDate && metadata.ReleaseDate is DateOnly releaseDate)
        {
            ReleaseDateDisplay = releaseDate.ToString("d MMMM yyyy", UiDisplayCulture.Current);
            OnPropertyChanged(nameof(ReleaseDateDisplay));
            OnPropertyChanged(nameof(HasReleaseDate));
        }
        Genres = metadata.Genres ?? [];
        GameModes = BuildGameModes(metadata);
        OnPropertyChanged(nameof(Genres));
        OnPropertyChanged(nameof(GameModes));
        OnPropertyChanged(nameof(HasGenres));
        OnPropertyChanged(nameof(HasGameModes));
        OnPropertyChanged(nameof(HasGeneralInfo));
    }

    private static IReadOnlyList<string> BuildGameModes(ProviderGameMetadata metadata)
    {
        var modes = new List<string>();
        if (metadata.SinglePlayer is true) modes.Add("Solo");
        if (metadata.MultiPlayer is true) modes.Add("Multijoueur");
        if (metadata.OnlineCoop is true) modes.Add("Coop en ligne");
        if (metadata.LocalCoop is true) modes.Add("Coop locale");
        return modes;
    }

    private static string GetDriveLabel(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;
        var root = Path.GetPathRoot(path);
        return string.IsNullOrWhiteSpace(root) ? string.Empty : root.TrimEnd('\\');
    }

    public string? SessionStatusLabel => Game.SessionStatusLabel;

    public void Activate()
    {
        if (_isActive || _sessionMonitor is null)
        {
            return;
        }

        _isActive = true;
        _sessionMonitor.SnapshotUpdated += SessionMonitor_OnSnapshotUpdated;
    }

    public void Deactivate()
    {
        if (!_isActive || _sessionMonitor is null)
        {
            return;
        }

        _isActive = false;
        _sessionMonitor.SnapshotUpdated -= SessionMonitor_OnSnapshotUpdated;
    }

    private async void SessionMonitor_OnSnapshotUpdated(SessionRuntimeSnapshot snapshot)
    {
        if (!_isActive)
        {
            return;
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            _ = dispatcher.BeginInvoke(() => SessionMonitor_OnSnapshotUpdated(snapshot));
            return;
        }

        var isSessionActive = snapshot.ActiveSessions.Any(session => session.GameId == GameId.Value);
        if (isSessionActive == Game.IsSessionActive)
        {
            return;
        }

        Game = Game with { IsSessionActive = isSessionActive };
        OnPropertyChanged(nameof(Game));
        OnPropertyChanged(nameof(SessionStatusLabel));

        if (_refreshActivityAsync is not null)
        {
            await _refreshActivityAsync();
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
