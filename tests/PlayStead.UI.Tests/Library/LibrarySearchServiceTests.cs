using PlayStead.Core.Library;
using PlayStead.UI.Library;

namespace PlayStead.UI.Tests.Library;

public sealed class LibrarySearchServiceTests
{
    [Fact]
    public void Search_matches_display_title_prefix_case_insensitively()
    {
        var items =
            new[]
            {
                new LibraryItemViewModel(
                    GameId.New(), "Arma Reforger", ProviderKind.Steam, "Steam",
                    @"D:\Games\Arma Reforger", 27_000_000_000),
                new LibraryItemViewModel(
                    GameId.New(), "HELLDIVERS™ 2", ProviderKind.Steam, "Steam",
                    @"D:\Games\Helldivers 2", 24_000_000_000),
                new LibraryItemViewModel(
                    GameId.New(), "Incursion Red River", ProviderKind.Manual, "Manual",
                    @"H:\SteamLibrary\steamapps\common\PROJECT QUARANTINE", 42_000_000_000)
            };

        Assert.Single(LibrarySearchService.Search(items, "arm"));
        Assert.Equal("Arma Reforger", LibrarySearchService.Search(items, "ARM")[0].Title);
        Assert.Empty(LibrarySearchService.Search(items, "steam"));
        Assert.Empty(LibrarySearchService.Search(items, "project quarantine"));
    }
}
