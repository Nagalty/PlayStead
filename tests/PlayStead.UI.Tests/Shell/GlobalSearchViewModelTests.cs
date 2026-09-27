using PlayStead.Core.Library;
using PlayStead.UI.Library;
using PlayStead.UI.Navigation;
using PlayStead.UI.Shell;

namespace PlayStead.UI.Tests.Shell;

public sealed class GlobalSearchViewModelTests
{
    [Fact]
    public void Empty_query_has_no_results_and_closes_panel()
    {
        var search = new GlobalSearchViewModel(new NavigationService());
        search.SetItems([Item("Helldivers 2"), Item("Hades")]);

        search.SetQuery("H");
        search.SetQuery(string.Empty);

        Assert.Empty(search.Results);
        Assert.False(search.IsOpen);
    }

    [Fact]
    public void Matching_is_case_and_accent_insensitive()
    {
        var search = new GlobalSearchViewModel(new NavigationService());
        search.SetItems([Item("Étoile du Nord"), Item("Autre jeu")]);

        search.SetQuery("ETOILE");

        Assert.Single(search.Results);
        Assert.Equal("Étoile du Nord", search.Results[0].Title);
    }

    [Fact]
    public void Starts_with_results_are_ranked_before_contains_results()
    {
        var search = new GlobalSearchViewModel(new NavigationService());
        search.SetItems([Item("The Witcher"), Item("Helldivers 2"), Item("Another Helldivers story")]);

        search.SetQuery("hel");

        Assert.True(search.IsOpen);
        Assert.Equal(["Helldivers 2", "Another Helldivers story"], search.Results.Select(x => x.Title));
    }

    [Fact]
    public void Selecting_result_navigates_to_game_detail_without_launching()
    {
        var navigation = new NavigationService();
        var search = new GlobalSearchViewModel(navigation);
        search.SetItems([Item("Helldivers 2")]);
        search.SetQuery("hel");

        var result = search.Results[0];
        search.SelectResultCommand.Execute(result);

        Assert.Equal(AppRoute.GameDetail, navigation.CurrentRoute);
        Assert.Equal(result.GameId, navigation.CurrentParameter);
        Assert.False(search.IsOpen);
    }

    private static LibraryItemViewModel Item(string title) =>
        new(GameId.New(), title, ProviderKind.Steam, "Steam", @"C:\Games", null);
}
