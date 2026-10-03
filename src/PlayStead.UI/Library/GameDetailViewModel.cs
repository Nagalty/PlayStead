using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.IO;
using System.IO.Compression;
using PlayStead.Core.Library;
using PlayStead.Core.Catalog;
using PlayStead.Core.Persistence;
using PlayStead.Core.Sessions;
using PlayStead.UI.Launching;
using PlayStead.UI.Sessions;
using PlayStead.Core.Shortlist;
using PlayStead.Core.ProviderGameMetadata;
using PlayStead.Core.GameBuildHistory;
using PlayStead.Core.Modding;
using PlayStead.Core.LocalArtifacts;
using PlayStead.Core.Graphics;
using CommunityToolkit.Mvvm.Input;
using System.Globalization;
using System.Diagnostics;
using System.Windows.Input;
using Forms = System.Windows.Forms;

namespace PlayStead.UI.Library;

public sealed class GameDetailViewModel : INotifyPropertyChanged
{
    private readonly SessionMonitor? _sessionMonitor;
    private readonly Func<Task>? _refreshActivityAsync;
    private bool _isActive;
    private bool _isGamePropertySubscriptionActive;
    private readonly ICanonicalCatalogStore? _catalogStore;
    private readonly IGamesDuMomentService? _gamesDuMomentService;
    private readonly IProviderGameMetadataStore? _providerGameMetadataStore;
    private readonly IManualMetadataLinkStore? _manualMetadataLinkStore;
    private readonly GameBuildHistoryService? _gameBuildHistoryService;
    private readonly IGameLocalArtifactDiscoveryService? _localArtifactDiscoveryService;
    private readonly IArtifactFingerprintService? _artifactFingerprintService;
    private readonly ILocalArtifactBaselineStore? _localArtifactBaselineStore;
    private readonly ILocalArtifactSnapshotService? _localArtifactSnapshotService;
    private readonly ILocalArtifactRestoreService? _localArtifactRestoreService;
    private readonly UserDefinedLocalArtifactService? _userDefinedArtifactService;
    private readonly ILocalArtifactComparisonService? _localArtifactComparisonService;
    private readonly IGraphicsTechnologyDetectionService? _graphicsTechnologyDetectionService;
    private readonly IModEvidenceStore? _modEvidenceStore;
    private Func<Task>? _editManualGame;
    private Func<Task>? _removeManualGame;
    private readonly LocalArtifactBaselineComparisonService _artifactBaselineComparisonService = new();
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
        GameBuildHistoryService? gameBuildHistoryService = null,
        IGameLocalArtifactDiscoveryService? localArtifactDiscoveryService = null,
        IArtifactFingerprintService? artifactFingerprintService = null,
        ILocalArtifactBaselineStore? artifactBaselineStore = null,
        LocalArtifactBaselineComparisonService? artifactBaselineComparisonService = null,
        ILocalArtifactSnapshotService? artifactSnapshotService = null,
        ILocalArtifactRestoreService? artifactRestoreService = null,
        IModEvidenceStore? modEvidenceStore = null,
        UserDefinedLocalArtifactService? userDefinedArtifactService = null,
        ILocalArtifactComparisonService? localArtifactComparisonService = null,
        IGraphicsTechnologyDetectionService? graphicsTechnologyDetectionService = null,
        IManualMetadataLinkStore? manualMetadataLinkStore = null)
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
        _manualMetadataLinkStore = manualMetadataLinkStore;
        _gameBuildHistoryService = gameBuildHistoryService;
        _localArtifactDiscoveryService = localArtifactDiscoveryService;
        _artifactFingerprintService = artifactFingerprintService;
        _localArtifactBaselineStore = artifactBaselineStore;
        _localArtifactSnapshotService = artifactSnapshotService;
        _localArtifactRestoreService = artifactRestoreService;
        _userDefinedArtifactService = userDefinedArtifactService;
        _localArtifactComparisonService = localArtifactComparisonService;
        _graphicsTechnologyDetectionService = graphicsTechnologyDetectionService;
        _modEvidenceStore = modEvidenceStore;
        _artifactBaselineComparisonService = artifactBaselineComparisonService ?? new LocalArtifactBaselineComparisonService();
        _openLocalArtifactFolderCommand = new RelayCommand<GameLocalArtifact>(OpenLocalArtifact,
            artifact => artifact?.Exists == true && artifact.Details is not null);
        CaptureLocalArtifactBaselineCommand = new AsyncRelayCommand<GameLocalArtifact>(CaptureLocalArtifactBaselineAsync,
            artifact => artifact?.CanCaptureBaseline == true);
        CreateLocalArtifactSnapshotCommand = new AsyncRelayCommand<GameLocalArtifact>(CreateLocalArtifactSnapshotAsync,
            artifact => artifact?.Exists == true && artifact.HasBaseline && artifact.IsSnapshotVisible && _localArtifactSnapshotService is not null);
        ProtectLocalArtifactCommand = new AsyncRelayCommand<GameLocalArtifact>(ProtectLocalArtifactAsync,
            artifact => artifact?.CanProtect == true && _localArtifactSnapshotService is not null);
        DeleteLocalArtifactSnapshotCommand = new AsyncRelayCommand<LocalArtifactSnapshot>(DeleteLocalArtifactSnapshotAsync,
            snapshot => snapshot is not null && _localArtifactSnapshotService is not null);
        RestoreLocalArtifactSnapshotCommand = new AsyncRelayCommand<LocalArtifactSnapshot>(RestoreLocalArtifactSnapshotAsync,
            snapshot => snapshot is not null && snapshot.IsValid && _localArtifactRestoreService is not null && LocalArtifacts.Any(a => a.Kind == snapshot.ArtifactKind && a.RuleIdentity == snapshot.RuleIdentity && a.Exists && a.HasBaseline) && !_localArtifactRestoreService.IsGameRunning(GameId));
        AddUserDefinedArtifactCommand = new AsyncRelayCommand(AddUserDefinedArtifactAsync, () => _userDefinedArtifactService is not null);
        RemoveUserDefinedArtifactCommand = new AsyncRelayCommand<GameLocalArtifact>(RemoveUserDefinedArtifactAsync, artifact => artifact?.Source == GameLocalArtifactSource.UserDefined && _userDefinedArtifactService is not null);
        CompareLocalArtifactCommand = new AsyncRelayCommand<GameLocalArtifact>(CompareLocalArtifactAsync, artifact => artifact?.CanCompare == true && _localArtifactComparisonService is not null);
        AddToGamesDuMomentCommand = new AsyncRelayCommand(AddToGamesDuMomentAsync, () => CanChangeGamesDuMoment);
        RemoveFromGamesDuMomentCommand = new AsyncRelayCommand(RemoveFromGamesDuMomentAsync, () => CanChangeGamesDuMoment);
        EditManualGameCommand = new AsyncRelayCommand(EditManualGameAsync, () => IsManualGame && _editManualGame is not null);
        RemoveManualGameCommand = new AsyncRelayCommand(RemoveManualGameAsync, () => IsManualGame && _removeManualGame is not null);
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
        GameBuildHistoryService? gameBuildHistoryService = null,
        IGameLocalArtifactDiscoveryService? localArtifactDiscoveryService = null,
        IArtifactFingerprintService? artifactFingerprintService = null,
        ILocalArtifactBaselineStore? artifactBaselineStore = null,
        LocalArtifactBaselineComparisonService? artifactBaselineComparisonService = null,
        ILocalArtifactSnapshotService? artifactSnapshotService = null,
        ILocalArtifactRestoreService? artifactRestoreService = null,
        IModEvidenceStore? modEvidenceStore = null,
        UserDefinedLocalArtifactService? userDefinedArtifactService = null,
        ILocalArtifactComparisonService? localArtifactComparisonService = null,
        IGraphicsTechnologyDetectionService? graphicsTechnologyDetectionService = null,
        IManualMetadataLinkStore? manualMetadataLinkStore = null)
        : this(game, launch, activity, heroPath, catalogStore, gamesDuMomentService, providerGameMetadataStore, gameBuildHistoryService, localArtifactDiscoveryService, artifactFingerprintService, artifactBaselineStore, artifactBaselineComparisonService, artifactSnapshotService, artifactRestoreService, modEvidenceStore, userDefinedArtifactService, localArtifactComparisonService, graphicsTechnologyDetectionService, manualMetadataLinkStore)
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

    public string? HeroPath { get; private set; }

    public bool HasHero =>
        !string.IsNullOrWhiteSpace(HeroPath);

    public void SetHeroPath(string? path)
    {
        if (string.Equals(HeroPath, path, StringComparison.Ordinal))
        {
            return;
        }

        HeroPath = path;
        OnPropertyChanged(nameof(HeroPath));
        OnPropertyChanged(nameof(HasHero));
    }

    public bool HasCover =>
        Game.HasCover;

    public string? CoverPath =>
        Game.CoverPath;

    public bool HasInstallPath =>
        !string.IsNullOrWhiteSpace(
            Game.InstallPath);

    public bool HasInstalledSize =>
        Game.InstalledSizeBytes.HasValue;

    public bool HasNoInstalledSize => !HasInstalledSize;

    public bool HasInstallDrive =>
        !string.IsNullOrWhiteSpace(InstallDriveLabel);

    public bool HasNoInstallDrive => !HasInstallDrive;

    public bool HasNoInstallPath => !HasInstallPath;

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
        {
            var baseMetadata = await _providerGameMetadataStore.GetAsync(GameId, Game.Provider, cancellationToken);
            var enrichment = Game.Provider == ProviderKind.Manual
                ? await _providerGameMetadataStore.GetAsync(GameId, ProviderKind.Steam, cancellationToken)
                : null;
            var currentMediaSource = Game.Provider == ProviderKind.Manual && _manualMetadataLinkStore is not null
                ? (await _manualMetadataLinkStore.GetAsync(GameId, cancellationToken))?.MediaSource
                : null;
            if (baseMetadata is not null || enrichment is not null)
                ApplyProviderMetadata(ProviderGameMetadataComposer.Compose(baseMetadata, enrichment, currentMediaSource));
        }
        if (_modEvidenceStore is not null)
        {
            ModState = ModEvidenceAggregation.GetState(await _modEvidenceStore.GetByGameAsync(GameId, cancellationToken));
            OnPropertyChanged(nameof(ModState));
            OnPropertyChanged(nameof(HasModEvidence));
            OnPropertyChanged(nameof(ModStatusLabel));
            OnPropertyChanged(nameof(HasGeneralInfo));
        }

        await LoadBuildHistoryAsync(cancellationToken);
        await LoadLocalArtifactsAsync(cancellationToken);
        if (_graphicsTechnologyDetectionService is not null)
        {
            GraphicsTechnologies = await _graphicsTechnologyDetectionService.DetectAsync(GameId, InstallPath, Title, cancellationToken);
            OnPropertyChanged(nameof(GraphicsTechnologies));
            OnPropertyChanged(nameof(HasGraphicsTechnologies));
            OnPropertyChanged(nameof(HasNoGraphicsTechnologies));
        }

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

    public string InstalledSizeDisplay => HasInstalledSize ? InstalledSizeLabel : "Inconnue";
    public string InstallDriveDisplay => HasInstallDrive ? InstallDriveLabel : "—";
    public string InstallPathDisplay => HasInstallPath ? InstallPath : "—";

    public string SteamStatusLabel { get; }

    public string? DeveloperDisplay { get; private set; }
    public string? PublisherDisplay { get; private set; }
    public string? ReleaseDateDisplay { get; private set; }
    public IReadOnlyList<string> Genres { get; private set; } = [];
    public IReadOnlyList<string> GameModes { get; private set; } = [];
    public bool HasGenres => Genres.Count > 0;
    public bool HasGameModes => GameModes.Count > 0;
    public bool HasNoGenres => !HasGenres;
    public bool HasNoGameModes => !HasGameModes;
    public ModDetectionState ModState { get; private set; } = ModDetectionState.Unknown;
    public bool HasModEvidence => ModState != ModDetectionState.Unknown;
    public string ModStatusLabel => ModState switch
    {
        ModDetectionState.ConfirmedModded => "Toi, tu utilises des mods sur ce jeu, c’est certain. Je le vois, tu sais.",
        ModDetectionState.PossiblyModded => "Il me semble que tu utilises des mods sur ce jeu, mais j’ai encore un doute.",
        _ => string.Empty
    };

    public bool IsInGamesDuMoment { get; private set; }
    public bool IsManualGame => Game.Provider == ProviderKind.Manual;
    public IAsyncRelayCommand EditManualGameCommand { get; }
    public IAsyncRelayCommand RemoveManualGameCommand { get; }

    public void AttachManualGameActions(Func<Task> edit, Func<Task> remove)
    {
        _editManualGame = edit ?? throw new ArgumentNullException(nameof(edit));
        _removeManualGame = remove ?? throw new ArgumentNullException(nameof(remove));
        EditManualGameCommand.NotifyCanExecuteChanged();
        RemoveManualGameCommand.NotifyCanExecuteChanged();
    }
    public bool CanChangeGamesDuMoment => _gamesDuMomentService is not null && !_isShortlistOperationInProgress;
    public string GamesDuMomentActionLabel => IsInGamesDuMoment ? "Retirer des jeux du moment" : "Ajouter aux jeux du moment";
    public IAsyncRelayCommand AddToGamesDuMomentCommand { get; }
    public IAsyncRelayCommand RemoveFromGamesDuMomentCommand { get; }

    private Task EditManualGameAsync() => _editManualGame?.Invoke() ?? Task.CompletedTask;
    private Task RemoveManualGameAsync() => _removeManualGame?.Invoke() ?? Task.CompletedTask;

    public sealed record BuildHistoryEntryViewModel(
        string? PreviousBuildId,
        string BuildId,
        DateTimeOffset ObservedAtUtc,
        bool IsBaseline,
        bool IsSinceLastPlay)
    {
        public string ObservedAtLabel => ObservedAtUtc.ToLocalTime().ToString("d MMM yyyy HH:mm", CultureInfo.CurrentCulture);
        public string BuildTransitionLabel => IsBaseline
            ? $"Premier point de repère · {BuildId}"
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
            : HasBuildChanges ? "Celui-là a bougé depuis la dernière fois." : "Rien n’a bougé depuis que je garde un œil dessus.";

    public IReadOnlyList<GameLocalArtifact> LocalArtifacts { get; private set; } = [];
    public IReadOnlyList<GraphicsTechnologyObservation> GraphicsTechnologies { get; private set; } = [];
    public bool HasGraphicsTechnologies => GraphicsTechnologies.Count > 0;
    public bool HasNoGraphicsTechnologies => !HasGraphicsTechnologies;
    public bool HasLocalArtifacts => LocalArtifacts.Count > 0;
    public bool ShowLocalArtifactHelper => !HasLocalArtifacts;
    public bool HasLocalArtifactArea => true;
    public ICommand OpenLocalArtifactFolderCommand => _openLocalArtifactFolderCommand;
    private readonly RelayCommand<GameLocalArtifact> _openLocalArtifactFolderCommand;
    public IAsyncRelayCommand<GameLocalArtifact> CaptureLocalArtifactBaselineCommand { get; }
    public IAsyncRelayCommand<GameLocalArtifact> CreateLocalArtifactSnapshotCommand { get; }
    public IAsyncRelayCommand<GameLocalArtifact> ProtectLocalArtifactCommand { get; }
    public IAsyncRelayCommand<LocalArtifactSnapshot> DeleteLocalArtifactSnapshotCommand { get; }
    public IAsyncRelayCommand<LocalArtifactSnapshot> RestoreLocalArtifactSnapshotCommand { get; }
    public IAsyncRelayCommand AddUserDefinedArtifactCommand { get; }
    public IAsyncRelayCommand<GameLocalArtifact> RemoveUserDefinedArtifactCommand { get; }
    public IAsyncRelayCommand<GameLocalArtifact> CompareLocalArtifactCommand { get; }
    public IReadOnlyList<LocalArtifactSnapshot> LocalArtifactSnapshots { get; private set; } = [];
    public bool HasLocalArtifactSnapshots => LocalArtifactSnapshots.Count > 0;

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

    private async Task LoadLocalArtifactsAsync(CancellationToken cancellationToken)
    {
        var discovered = _localArtifactDiscoveryService is null
            ? []
            : await _localArtifactDiscoveryService.DiscoverAsync(
                GameId,
                Game.Provider,
                Game.ProviderGameId,
                cancellationToken);
        LocalArtifacts = await ApplyBaselineStatusesAsync(discovered, cancellationToken);
        LocalArtifactSnapshots = _localArtifactSnapshotService is null
            ? []
            : (await Task.WhenAll(LocalArtifacts.Where(a => a.RuleIdentity is not null && a.IsSnapshotVisible).Select(a => _localArtifactSnapshotService.ListAsync(a, cancellationToken)))).SelectMany(x => x).OrderByDescending(x => x.CreatedAtUtc).Take(5).ToArray();
        if (_localArtifactSnapshotService is not null)
        {
            var withProtection = new List<GameLocalArtifact>(LocalArtifacts.Count);
            foreach (var artifact in LocalArtifacts)
            {
                if (!artifact.IsSnapshotVisible)
                {
                    withProtection.Add(artifact with { SnapshotCount = 0, LastSnapshotAtUtc = null });
                    continue;
                }
                var snapshots = artifact.RuleIdentity is null
                    ? []
                    : await _localArtifactSnapshotService.ListAsync(
                        artifact with { BaselineStatus = artifact.HasBaseline ? LocalArtifactBaselineStatus.Unchanged : LocalArtifactBaselineStatus.NoBaseline },
                        cancellationToken);
                withProtection.Add(artifact with
                {
                    SnapshotCount = snapshots.Count,
                    LastSnapshotAtUtc = snapshots.OrderByDescending(x => x.CreatedAtUtc).FirstOrDefault()?.CreatedAtUtc
                });
            }
            LocalArtifacts = withProtection;
        }
        OnPropertyChanged(nameof(LocalArtifacts));
        OnPropertyChanged(nameof(HasLocalArtifacts));
        OnPropertyChanged(nameof(ShowLocalArtifactHelper));
        OnPropertyChanged(nameof(LocalArtifactSnapshots));
        OnPropertyChanged(nameof(HasLocalArtifactSnapshots));
        ProtectLocalArtifactCommand.NotifyCanExecuteChanged();
        CreateLocalArtifactSnapshotCommand.NotifyCanExecuteChanged();
    }

    private async Task<IReadOnlyList<GameLocalArtifact>> ApplyBaselineStatusesAsync(
        IReadOnlyList<GameLocalArtifact> artifacts,
        CancellationToken cancellationToken)
    {
        if (_artifactFingerprintService is null || _localArtifactBaselineStore is null)
            return artifacts;
        var result = new List<GameLocalArtifact>(artifacts.Count);
        foreach (var artifact in artifacts)
        {
            var current = artifact.Exists
                ? await _artifactFingerprintService.ComputeAsync(artifact, cancellationToken)
                : ArtifactFingerprintResult.Unavailable("Artifact is missing.");
            var baseline = artifact.RuleIdentity is null
                ? null
                : await _localArtifactBaselineStore.GetAsync(GameId, artifact.Kind, artifact.RuleIdentity, cancellationToken);
            result.Add(artifact with { BaselineStatus = _artifactBaselineComparisonService.Compare(artifact, current, baseline) });
        }
        return result;
    }

    private async Task CaptureLocalArtifactBaselineAsync(GameLocalArtifact? artifact)
    {
        if (artifact is null || !artifact.CanCaptureBaseline || artifact.RuleIdentity is null || _artifactFingerprintService is null || _localArtifactBaselineStore is null)
            return;
        var current = await _artifactFingerprintService.ComputeAsync(artifact, CancellationToken.None);
        if (!current.IsAvailable) return;
        var fingerprint = current.Fingerprint!;
        await _localArtifactBaselineStore.UpsertAsync(
            new LocalArtifactBaseline(GameId, artifact.Kind, artifact.RuleIdentity, fingerprint.Algorithm, fingerprint.Hash, fingerprint.FileCount, fingerprint.TotalSizeBytes, fingerprint.CapturedAtUtc),
            CancellationToken.None);
        await LoadLocalArtifactsAsync(CancellationToken.None);
        CaptureLocalArtifactBaselineCommand.NotifyCanExecuteChanged();
    }

    private async Task CreateLocalArtifactSnapshotAsync(GameLocalArtifact? artifact)
    {
        if (artifact is null || _localArtifactSnapshotService is null) return;
        try
        {
            await _localArtifactSnapshotService.CreateAsync(artifact, CancellationToken.None);
        }
        catch (SnapshotStorageQuotaExceededException)
        {
            MessageBox.Show("L’espace réservé aux sauvegardes est plein. Supprime une ancienne sauvegarde ou augmente la limite dans Paramètres.", "Fichiers locaux", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        await LoadLocalArtifactsAsync(CancellationToken.None);
    }

    private async Task ProtectLocalArtifactAsync(GameLocalArtifact? artifact)
    {
        if (artifact is null || !artifact.CanProtect || _artifactFingerprintService is null || _localArtifactBaselineStore is null || _localArtifactSnapshotService is null)
            return;
        var current = await _artifactFingerprintService.ComputeAsync(artifact, CancellationToken.None);
        if (!current.IsAvailable) return;
        var fingerprint = current.Fingerprint!;
        await _localArtifactBaselineStore.UpsertAsync(
            new LocalArtifactBaseline(GameId, artifact.Kind, artifact.RuleIdentity!, fingerprint.Algorithm, fingerprint.Hash, fingerprint.FileCount, fingerprint.TotalSizeBytes, fingerprint.CapturedAtUtc),
            CancellationToken.None);
        if (artifact.Kind == GameLocalArtifactKind.SaveData)
            await _localArtifactSnapshotService.CreateAsync(artifact with { BaselineStatus = LocalArtifactBaselineStatus.Unchanged }, CancellationToken.None, SnapshotReason.InitialProtection);
        await LoadLocalArtifactsAsync(CancellationToken.None);
    }

    private async Task CompareLocalArtifactAsync(GameLocalArtifact? artifact)
    {
        if (artifact is null || !artifact.CanCompare || _localArtifactComparisonService is null) return;
        var snapshot = LocalArtifactSnapshots.FirstOrDefault(x => x.ArtifactKind == artifact.Kind && x.RuleIdentity == artifact.RuleIdentity && x.IsValid);
        if (snapshot is null) return;
        var temp = Path.Combine(Path.GetTempPath(), $"playstead-compare-{Guid.NewGuid():N}{Path.GetExtension(artifact.Path)}");
        try
        {
            using (var archive = ZipFile.OpenRead(snapshot.ArchivePath))
            {
                var entry = archive.Entries.FirstOrDefault(x => !string.Equals(x.FullName, "metadata.json", StringComparison.OrdinalIgnoreCase) && !x.FullName.EndsWith('/'));
                if (entry is null) return;
                entry.ExtractToFile(temp, overwrite: true);
            }
            var result = await _localArtifactComparisonService.CompareAsync(temp, artifact.Path, CancellationToken.None);
            var body = result.Status switch
            {
                LocalArtifactComparisonStatus.Available => $"{result.ChangedLineCount} ligne(s) modifiée(s)\n\n" + string.Join("\n", result.DiffHunks.SelectMany(x => x.Lines).Take(80).Select(x => $"{(x.IsRemoved ? "-" : x.IsAdded ? "+" : " ")}{x.Text}")),
                LocalArtifactComparisonStatus.Identical => "Rien n’a changé.",
                _ => "La comparaison détaillée n’est pas disponible pour ce fichier."
            };
            MessageBox.Show(body, $"Ce qui a changé · {Path.GetFileName(artifact.Path)}", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (IOException) { }
        catch (InvalidDataException) { }
        finally
        {
            try { if (File.Exists(temp)) File.Delete(temp); } catch { }
        }
    }

    private async Task DeleteLocalArtifactSnapshotAsync(LocalArtifactSnapshot? snapshot)
    {
        if (snapshot is null || _localArtifactSnapshotService is null) return;
        if (MessageBox.Show("Supprimer cette sauvegarde de PlayStead ?\n\nLes fichiers du jeu ne seront pas modifiés.", "Supprimer cette sauvegarde ?", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK)
            return;
        await _localArtifactSnapshotService.DeleteAsync(snapshot, CancellationToken.None);
        await LoadLocalArtifactsAsync(CancellationToken.None);
    }

    private async Task RestoreLocalArtifactSnapshotAsync(LocalArtifactSnapshot? snapshot)
    {
        if (snapshot is null || _localArtifactRestoreService is null) return;
        var artifact = LocalArtifacts.FirstOrDefault(a => a.Kind == snapshot.ArtifactKind && a.RuleIdentity == snapshot.RuleIdentity && a.Exists && a.HasBaseline);
        if (artifact is null) return;
        var answer = MessageBox.Show("L’état actuel sera remplacé.\n\nUne sauvegarde de sécurité sera créée avant la restauration.",
            "Restaurer cette sauvegarde ?", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
        if (answer != MessageBoxResult.OK) return;
        var result = await _localArtifactRestoreService.RestoreAsync(artifact, snapshot, CancellationToken.None);
        MessageBox.Show(result.Message, "Fichiers locaux", MessageBoxButton.OK,
            result.Succeeded ? MessageBoxImage.Information : MessageBoxImage.Warning);
        if (result.Succeeded) await LoadLocalArtifactsAsync(CancellationToken.None);
    }

    private async Task AddUserDefinedArtifactAsync()
    {
        if (_userDefinedArtifactService is null) return;
        using var folderDialog = new Forms.FolderBrowserDialog
        {
            Description = "Choisis le dossier à garder à l’œil pour ce jeu.",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false
        };
        if (folderDialog.ShowDialog() != Forms.DialogResult.OK) return;

        var kind = ChooseArtifactKind();
        if (kind is null) return;
        try
        {
            await _userDefinedArtifactService.AddAsync(
                GameId,
                Game.Provider,
                Game.ProviderGameId,
                kind.Value,
                folderDialog.SelectedPath,
                null,
                CancellationToken.None);
            await LoadLocalArtifactsAsync(CancellationToken.None);
        }
        catch (ArgumentException error)
        {
            MessageBox.Show(error.Message, "Fichiers locaux", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (InvalidOperationException error)
        {
            MessageBox.Show(error.Message, "Fichiers locaux", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private async Task RemoveUserDefinedArtifactAsync(GameLocalArtifact? artifact)
    {
        if (artifact?.Source != GameLocalArtifactSource.UserDefined || _userDefinedArtifactService is null || !TryGetUserArtifactId(artifact, out var id)) return;
        var baseline = artifact.RuleIdentity is null || _localArtifactBaselineStore is null
            ? null
            : await _localArtifactBaselineStore.GetAsync(GameId, artifact.Kind, artifact.RuleIdentity, CancellationToken.None);
        var snapshots = _localArtifactSnapshotService is null ? [] : await _localArtifactSnapshotService.ListAsync(artifact, CancellationToken.None);
        if (baseline is not null || snapshots.Count > 0)
        {
            MessageBox.Show("Ce dossier a encore un point de repère ou des sauvegardes dans PlayStead.", "Fichiers locaux", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (MessageBox.Show("Retirer ce dossier de PlayStead ?\n\nRien ne sera supprimé sur ton disque.", "Fichiers locaux", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
        await _userDefinedArtifactService.RemoveAsync(id, CancellationToken.None);
        await LoadLocalArtifactsAsync(CancellationToken.None);
    }

    private static bool TryGetUserArtifactId(GameLocalArtifact artifact, out Guid id)
    {
        id = Guid.Empty;
        return artifact.RuleIdentity is not null && Guid.TryParse(artifact.RuleIdentity.StartsWith("user:", StringComparison.OrdinalIgnoreCase) ? artifact.RuleIdentity[5..] : string.Empty, out id);
    }

    private static GameLocalArtifactKind? ChooseArtifactKind()
    {
        using var dialog = new Forms.Form { Text = "Ajouter un dossier", Width = 320, Height = 150, StartPosition = Forms.FormStartPosition.CenterScreen, FormBorderStyle = Forms.FormBorderStyle.FixedDialog, MinimizeBox = false, MaximizeBox = false };
        var combo = new Forms.ComboBox { Left = 16, Top = 16, Width = 272, DropDownStyle = Forms.ComboBoxStyle.DropDownList };
        combo.Items.AddRange(["Sauvegardes", "Configuration", "Logs", "Autre"]);
        combo.SelectedIndex = 0;
        var ok = new Forms.Button { Text = "Valider", Left = 190, Top = 55, Width = 98, DialogResult = Forms.DialogResult.OK };
        var cancel = new Forms.Button { Text = "Annuler", Left = 82, Top = 55, Width = 98, DialogResult = Forms.DialogResult.Cancel };
        dialog.Controls.AddRange([combo, ok, cancel]);
        dialog.AcceptButton = ok;
        dialog.CancelButton = cancel;
        if (dialog.ShowDialog() != Forms.DialogResult.OK) return null;
        return combo.SelectedIndex switch
        {
            0 => GameLocalArtifactKind.SaveData,
            1 => GameLocalArtifactKind.Configuration,
            2 => GameLocalArtifactKind.Log,
            3 => GameLocalArtifactKind.Other,
            _ => null
        };
    }

    private static void OpenLocalArtifact(GameLocalArtifact? artifact)
    {
        if (artifact?.Exists != true || artifact.Details is null)
            return;

        Process.Start(new ProcessStartInfo
        {
            FileName = artifact.Path,
            UseShellExecute = true
        });
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
        // Keep provider/catalogue values untouched; only the UI projection is localized.
        Genres = GenreDisplayLocalizer.LocalizeMany(metadata.Genres ?? []);
        GameModes = BuildGameModes(metadata);
        OnPropertyChanged(nameof(Genres));
        OnPropertyChanged(nameof(GameModes));
        OnPropertyChanged(nameof(HasGenres));
        OnPropertyChanged(nameof(HasGameModes));
        OnPropertyChanged(nameof(HasNoGenres));
        OnPropertyChanged(nameof(HasNoGameModes));
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
        if (_isActive)
        {
            return;
        }

        _isActive = true;
        SubscribeToGameProperties();
        if (_sessionMonitor is not null)
        {
            _sessionMonitor.SnapshotUpdated += SessionMonitor_OnSnapshotUpdated;
        }
    }

    public void Deactivate()
    {
        if (!_isActive && !_isGamePropertySubscriptionActive)
        {
            return;
        }

        _isActive = false;
        if (_sessionMonitor is not null)
        {
            _sessionMonitor.SnapshotUpdated -= SessionMonitor_OnSnapshotUpdated;
        }
        UnsubscribeFromGameProperties();
    }

    private void SubscribeToGameProperties()
    {
        if (_isGamePropertySubscriptionActive)
        {
            return;
        }

        Game.PropertyChanged += Game_OnPropertyChanged;
        _isGamePropertySubscriptionActive = true;
    }

    private void UnsubscribeFromGameProperties()
    {
        if (!_isGamePropertySubscriptionActive)
        {
            return;
        }

        Game.PropertyChanged -= Game_OnPropertyChanged;
        _isGamePropertySubscriptionActive = false;
    }

    private void Game_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(LibraryItemViewModel.CoverPath)
            or nameof(LibraryItemViewModel.HasCover))
        {
            OnPropertyChanged(e.PropertyName);
        }
    }

    private void ReplaceGame(LibraryItemViewModel game)
    {
        var previous = Game;
        if (ReferenceEquals(previous, game))
        {
            return;
        }

        if (_isGamePropertySubscriptionActive)
        {
            previous.PropertyChanged -= Game_OnPropertyChanged;
            game.PropertyChanged += Game_OnPropertyChanged;
        }

        Game = game;
        OnPropertyChanged(nameof(Game));
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

        ReplaceGame(Game with { IsSessionActive = isSessionActive });
        OnPropertyChanged(nameof(SessionStatusLabel));

        if (_refreshActivityAsync is not null)
        {
            await _refreshActivityAsync();
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
