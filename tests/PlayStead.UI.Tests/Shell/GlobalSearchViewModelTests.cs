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

    [Fact]
    public void Collection_name_finds_member_game_without_duplicate_results()
    {
        var first = Item("Helldivers 2");
        var second = Item("Deep Rock Galactic");
        var search = new GlobalSearchViewModel(new NavigationService());
        search.SetItems([first, second]);
        search.SetCollectionNames(new Dictionary<GameId, IReadOnlyList<string>>
        {
            [first.GameId] = ["Coop du vendredi", "Coop FPS"],
            [second.GameId] = ["Coop du vendredi"]
        });

        search.SetQuery("coop");

        Assert.Equal(["Deep Rock Galactic", "Helldivers 2"], search.Results.Select(x => x.Title));
        Assert.Equal(2, search.Results.Count);
    }

    [Fact]
    public void Title_matches_rank_before_collection_matches()
    {
        var titleMatch = Item("Coop Quest");
        var collectionMatch = Item("Helldivers 2");
        var search = new GlobalSearchViewModel(new NavigationService());
        search.SetItems([collectionMatch, titleMatch]);
        search.SetCollectionNames(new Dictionary<GameId, IReadOnlyList<string>>
        {
            [collectionMatch.GameId] = ["Coop du vendredi"]
        });

        search.SetQuery("coop");

        Assert.Equal("Coop Quest", search.Results[0].Title);
        Assert.Equal("Helldivers 2", search.Results[1].Title);
    }

    [Fact]
    public void Collection_projection_refresh_removes_old_name_and_matches_new_name()
    {
        var item = Item("Helldivers 2");
        var search = new GlobalSearchViewModel(new NavigationService());
        search.SetItems([item]);
        search.SetCollectionNames(new Dictionary<GameId, IReadOnlyList<string>>
        {
            [item.GameId] = ["Coop"]
        });
        search.SetQuery("coop");
        Assert.Single(search.Results);

        search.SetCollectionNames(new Dictionary<GameId, IReadOnlyList<string>>
        {
            [item.GameId] = ["Soirée"]
        });
        search.SetQuery("coop");
        Assert.Empty(search.Results);

        search.SetQuery("soiree");
        Assert.Single(search.Results);
    }

    private static LibraryItemViewModel Item(string title) =>
        new(GameId.New(), title, ProviderKind.Steam, "Steam", @"C:\Games", null);
}
