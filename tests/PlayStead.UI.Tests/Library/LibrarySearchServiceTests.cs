using PlayStead.Core.Library;
using PlayStead.UI.Library;

namespace PlayStead.UI.Tests.Library;

public sealed class LibrarySearchServiceTests
{
    [Fact]
    public void Search_matches_existing_local_metadata_case_insensitively()
    {
        var items =
            new[]
            {
                new LibraryItemViewModel(
                    GameId.New(),
                    "S.T.A.L.K.E.R. 2",
                    ProviderKind.Steam,
                    "Steam",
                    @"D:\Games\STALKER2",
                    120_000_000_000),

                new LibraryItemViewModel(
                    GameId.New(),
                    "Incursion Red River",
                    ProviderKind.Manual,
                    "Manual",
                    @"H:\SteamLibrary\steamapps\common\PROJECT QUARANTINE",
                    42_000_000_000),

                new LibraryItemViewModel(
                    GameId.New(),
                    "Ready or Not",
                    ProviderKind.Epic,
                    "Epic",
                    @"E:\Games\ReadyOrNot",
                    55_000_000_000)
            };

        Assert.Single(
            LibrarySearchService.Search(
                items,
                "stalker"));

        Assert.Single(
            LibrarySearchService.Search(
                items,
                "manual"));

        Assert.Single(
            LibrarySearchService.Search(
                items,
                "project quarantine"));

        Assert.Empty(
            LibrarySearchService.Search(
                items,
                "this does not exist"));
    }
}
