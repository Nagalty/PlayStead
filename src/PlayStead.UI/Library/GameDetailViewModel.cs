using PlayStead.Core.Library;
using PlayStead.UI.Launching;

namespace PlayStead.UI.Library;

public sealed class GameDetailViewModel
{
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
    {
        ArgumentNullException.ThrowIfNull(
            game);

        Game =
            game;

        GameId =
            game.GameId;

        Title =
            game.Title;

        ProviderLabel =
            game.ProviderLabel;

        InstallPath =
            game.InstallPath;

        InstalledSizeLabel =
            game.InstalledSizeLabel;

        SteamStatusLabel =
            game.SteamStatusLabel;

        SessionStatusLabel =
            game.SessionStatusLabel;

        Launch =
            launch;

        Activity =
            activity;
    }

    public LibraryItemViewModel Game { get; }

    public GameId GameId { get; }

    public GameLaunchViewModel? Launch { get; }

    public GameQuickPanelViewModel? Activity { get; }

    public bool HasCover =>
        Game.HasCover;

    public string? CoverPath =>
        Game.CoverPath;

    public bool HasInstallPath =>
        !string.IsNullOrWhiteSpace(
            Game.InstallPath);

    public bool HasInstalledSize =>
        Game.InstalledSizeBytes.HasValue;

    public bool HasSteamStatus =>
        Game.HasSteamStatus;

    public Task LoadAsync(
        CancellationToken cancellationToken) =>
        Activity?.LoadSessionSummaryAsync(
            cancellationToken) ??
        Task.CompletedTask;

    public string Title { get; }

    public string ProviderLabel { get; }

    public string InstallPath { get; }

    public string InstalledSizeLabel { get; }

    public string SteamStatusLabel { get; }

    public string? SessionStatusLabel { get; }
}
