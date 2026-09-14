using PlayStead.Core.Library;
using PlayStead.UI.Library;
using PlayStead.UI.Navigation;

namespace PlayStead.UI.Tests.Library;

public sealed class GameQuickPanelViewModelTests
{
    [Fact]
    public void OpenDetails_navigates_to_game_detail_with_selected_game_id()
    {
        var gameId =
            GameId.New();

        var item =
            new LibraryItemViewModel(
                gameId,
                "Test Game",
                default,
                "Provider",
                @"C:\Games\TestGame",
                42_000_000_000);

        var navigation =
            new NavigationService();

        navigation.Navigate(
            new NavigationRequest(
                AppRoute.Library));

        var viewModel =
            new GameQuickPanelViewModel(
                item,
                navigation);

        viewModel.OpenDetails();

        Assert.Equal(
            AppRoute.GameDetail,
            navigation.CurrentRoute);

        Assert.Equal(
            gameId,
            navigation.CurrentParameter);
    }
}
