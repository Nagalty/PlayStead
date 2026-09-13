using PlayStead.UI.Navigation;

namespace PlayStead.UI.Tests.Navigation;

public sealed class NavigationServiceTests
{
    [Fact]
    public void New_service_starts_on_Home_without_back_history()
    {
        var sut = new NavigationService();

        Assert.Equal(AppRoute.Home, sut.CurrentRoute);
        Assert.Null(sut.CurrentParameter);
        Assert.False(sut.CanGoBack);
    }

    [Fact]
    public void Navigate_to_Library_updates_current_route_and_enables_back()
    {
        var sut = new NavigationService();

        sut.Navigate(
            new NavigationRequest(
                AppRoute.Library));

        Assert.Equal(AppRoute.Library, sut.CurrentRoute);
        Assert.Null(sut.CurrentParameter);
        Assert.True(sut.CanGoBack);
    }

    [Fact]
    public void Game_detail_pushes_Library_and_GoBack_restores_it_with_its_parameter()
    {
        var sut = new NavigationService();
        var libraryContext = new object();
        var gameId = Guid.Parse(
            "11111111-1111-1111-1111-111111111111");

        sut.Navigate(
            new NavigationRequest(
                AppRoute.Library,
                libraryContext));

        sut.Navigate(
            new NavigationRequest(
                AppRoute.GameDetail,
                gameId));

        Assert.Equal(AppRoute.GameDetail, sut.CurrentRoute);
        Assert.Equal(
            gameId,
            Assert.IsType<Guid>(sut.CurrentParameter));
        Assert.True(sut.CanGoBack);

        var wentBack = sut.GoBack();

        Assert.True(wentBack);
        Assert.Equal(AppRoute.Library, sut.CurrentRoute);
        Assert.Same(libraryContext, sut.CurrentParameter);
        Assert.True(sut.CanGoBack);
    }

    [Fact]
    public void Duplicate_navigation_to_same_route_and_same_parameter_does_not_grow_history()
    {
        var sut = new NavigationService();
        var parameter = new object();

        sut.Navigate(
            new NavigationRequest(
                AppRoute.Library,
                parameter));

        sut.Navigate(
            new NavigationRequest(
                AppRoute.Library,
                parameter));

        Assert.True(sut.GoBack());
        Assert.Equal(AppRoute.Home, sut.CurrentRoute);
        Assert.False(sut.CanGoBack);
        Assert.False(sut.GoBack());
    }

    [Fact]
    public void Sessions_is_a_valid_contextual_route()
    {
        var sut = new NavigationService();

        sut.Navigate(
            new NavigationRequest(
                AppRoute.Sessions));

        Assert.Equal(AppRoute.Sessions, sut.CurrentRoute);
        Assert.True(sut.CanGoBack);

        Assert.True(sut.GoBack());
        Assert.Equal(AppRoute.Home, sut.CurrentRoute);
    }

    [Fact]
    public void AddToHistory_false_replaces_current_route_without_creating_a_back_entry()
    {
        var sut = new NavigationService();

        sut.Navigate(
            new NavigationRequest(
                AppRoute.Library,
                AddToHistory: false));

        Assert.Equal(AppRoute.Library, sut.CurrentRoute);
        Assert.False(sut.CanGoBack);
        Assert.False(sut.GoBack());
    }

    [Fact]
    public void Changed_fires_once_for_a_real_navigation_and_not_for_an_exact_duplicate()
    {
        var sut = new NavigationService();
        var changedCount = 0;

        sut.Changed +=
            (_, _) =>
                changedCount++;

        sut.Navigate(
            new NavigationRequest(
                AppRoute.Library));

        sut.Navigate(
            new NavigationRequest(
                AppRoute.Library));

        Assert.Equal(1, changedCount);
    }
}
