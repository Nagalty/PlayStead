using PlayStead.UI.Navigation;

namespace PlayStead.UI.Library;

public sealed class GameQuickPanelViewModel
{
    private readonly NavigationService
        _navigationService;

    public GameQuickPanelViewModel(
        LibraryItemViewModel game,
        NavigationService navigationService)
    {
        ArgumentNullException.ThrowIfNull(
            game);

        ArgumentNullException.ThrowIfNull(
            navigationService);

        Game =
            game;

        _navigationService =
            navigationService;
    }

    public LibraryItemViewModel Game { get; }

    public void OpenDetails()
    {
        _navigationService.Navigate(
            new NavigationRequest(
                AppRoute.GameDetail,
                Game.GameId));
    }
}
