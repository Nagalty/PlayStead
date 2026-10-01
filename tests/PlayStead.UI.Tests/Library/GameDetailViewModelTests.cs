using PlayStead.Core.Library;
using PlayStead.Core.Steam;
using PlayStead.UI.Library;

namespace PlayStead.UI.Tests.Library;

public sealed class GameDetailViewModelTests
{
    [Fact]
    public void Localizes_manual_provider_label_for_game_detail()
    {
        var item = new LibraryItemViewModel(
            GameId.New(),
            "Manual Game",
            ProviderKind.Manual,
            "Manuel",
            @"D:\Games\ManualGame",
            null,
            SteamUpdateState.Unknown,
            IsSessionActive: false);

        var viewModel = new GameDetailViewModel(item);

        Assert.Equal("Manuel", viewModel.ProviderLabel);
    }

    [Fact]
    public void Exposes_real_library_metadata_without_invented_fields()
    {
        var item =
            new LibraryItemViewModel(
                GameId.New(),
                "Test Game",
                ProviderKind.Steam,
                "Steam",
                @"D:\Games\TestGame",
                42_000_000_000,
                SteamUpdateState.UpToDate,
                IsSessionActive: true);

        var viewModel =
            new GameDetailViewModel(
                item);

        Assert.Equal(
            item.GameId,
            viewModel.GameId);

        Assert.Equal(
            "Test Game",
            viewModel.Title);

        Assert.Equal(
            "Steam",
            viewModel.ProviderLabel);

        Assert.Equal(
            @"D:\Games\TestGame",
            viewModel.InstallPath);

        Assert.Equal(
            "42,0 Go",
            viewModel.InstalledSizeLabel);

        Assert.Equal(
            "À jour",
            viewModel.SteamStatusLabel);

        Assert.Equal(
            "En cours",
            viewModel.SessionStatusLabel);
    }
}
