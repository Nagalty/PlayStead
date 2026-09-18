using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.IO;
using PlayStead.Core.Library;
using PlayStead.Core.Sessions;
using PlayStead.UI.Launching;
using PlayStead.UI.Sessions;

namespace PlayStead.UI.Library;

public sealed class GameDetailViewModel : INotifyPropertyChanged
{
    private readonly SessionMonitor? _sessionMonitor;
    private readonly Func<Task>? _refreshActivityAsync;
    private bool _isActive;
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
        string? heroPath)
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
    }

    public GameDetailViewModel(
        LibraryItemViewModel game,
        GameLaunchViewModel? launch,
        GameQuickPanelViewModel? activity,
        string? heroPath,
        SessionMonitor sessionMonitor,
        Func<Task>? refreshActivityAsync = null)
        : this(game, launch, activity, heroPath)
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

    public Task LoadAsync(
        CancellationToken cancellationToken) =>
        Activity?.LoadSessionSummaryAsync(
            cancellationToken) ??
        Task.CompletedTask;

    public string Title { get; }

    public string DisplayTitle { get; }

    public string ProviderLabel { get; }

    public string InstallPath { get; }

    public string InstallDriveLabel { get; }

    public string InstalledSizeLabel { get; }

    public string SteamStatusLabel { get; }

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
