using PlayStead.Core.Library;
using PlayStead.UI.Launching;

namespace PlayStead.UI.Library;

public sealed class GameDetailViewModel
{
    public GameDetailViewModel(
        LibraryItemViewModel game)
    {
        ArgumentNullException.ThrowIfNull(
            game);

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
    }

    public GameId GameId { get; }

    public GameDetailViewModel(
        LibraryItemViewModel game,
        GameLaunchViewModel? launch)
        : this(game)
    {
        Launch = launch;
    }

    public GameLaunchViewModel? Launch { get; }

    public string Title { get; }

    public string ProviderLabel { get; }

    public string InstallPath { get; }

    public string InstalledSizeLabel { get; }

    public string SteamStatusLabel { get; }

    public string? SessionStatusLabel { get; }
}
