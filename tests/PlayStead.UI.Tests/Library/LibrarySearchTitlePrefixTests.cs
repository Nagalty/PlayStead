using PlayStead.Core.Library;
using PlayStead.UI.Library;

namespace PlayStead.UI.Tests.Library;

public sealed class LibrarySearchTitlePrefixTests
{
    private static readonly LibraryItemViewModel[] Items =
    [
        new(
            GameId.New(),
            "Arma Reforger",
            ProviderKind.Steam,
            "Steam",
            @"G:\SteamLibrary\steamapps\common\Arma Reforger",
            27_000_000_000),

        new(
            GameId.New(),
            "Arena Breakout: Infinite",
            ProviderKind.Steam,
            "Steam",
            @"H:\SteamLibrary\steamapps\common\ABInfinite",
            90_000_000_000),

        new(
            GameId.New(),
            "HELLDIVERS™ 2",
            ProviderKind.Steam,
            "Steam",
            @"G:\SteamLibrary\steamapps\common\Helldivers 2",
            24_000_000_000),

        new(
            GameId.New(),
            "Incursion Red River",
            ProviderKind.Steam,
            "Steam",
            @"H:\SteamLibrary\steamapps\common\PROJECT QUARANTINE",
            75_000_000_000)
    ];

    [Fact]
    public void Search_filters_by_display_title_prefix_from_the_first_character()
    {
        var a =
            LibrarySearchService.Search(
                Items,
                "A");

        Assert.NotEmpty(a);
        Assert.All(
            a,
            item =>
                Assert.StartsWith(
                    "A",
                    item.Title,
                    StringComparison.OrdinalIgnoreCase));

        Assert.DoesNotContain(
            a,
            item =>
                item.Title.Contains(
                    "HELLDIVERS",
                    StringComparison.OrdinalIgnoreCase));

        var ar =
            LibrarySearchService.Search(
                Items,
                "AR");

        Assert.NotEmpty(ar);
        Assert.All(
            ar,
            item =>
                Assert.StartsWith(
                    "AR",
                    item.Title,
                    StringComparison.OrdinalIgnoreCase));

        var arm =
            LibrarySearchService.Search(
                Items,
                "aRm");

        var result =
            Assert.Single(
                arm);

        Assert.Equal(
            "Arma Reforger",
            result.Title);
    }

    [Fact]
    public void Search_does_not_match_provider_or_install_path()
    {
        Assert.Empty(
            LibrarySearchService.Search(
                Items,
                "steam"));

        Assert.Empty(
            LibrarySearchService.Search(
                Items,
                "project quarantine"));
    }
}
