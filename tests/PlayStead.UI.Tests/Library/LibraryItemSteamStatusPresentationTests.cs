using PlayStead.Core.Library;
using PlayStead.Core.Steam;
using PlayStead.UI.Library;

namespace PlayStead.UI.Tests.Library;

public sealed class LibraryItemSteamStatusPresentationTests
{
    [Theory]
    [InlineData(SteamUpdateState.UpToDate, "À jour")]
    [InlineData(SteamUpdateState.UpdateAvailable, "Mise à jour disponible")]
    [InlineData(SteamUpdateState.NewVersionDetected, "Nouvelle version détectée")]
    [InlineData(SteamUpdateState.Unknown, "État inconnu")]
    [InlineData(SteamUpdateState.Checking, "Vérification…")]
    public void SteamStatusLabel_maps_states_to_minimal_French_labels(
        SteamUpdateState state,
        string expected)
    {
        var item = CreateSteamItem(state);

        Assert.True(item.HasSteamStatus);
        Assert.Equal(expected, item.SteamStatusLabel);
    }

    [Fact]
    public void Non_Steam_item_has_no_Steam_status()
    {
        var item = new LibraryItemViewModel(
            GameId.New(),
            "GOG Game",
            ProviderKind.Gog,
            "GOG",
            @"C:\Games\GOG",
            10_000_000_000,
            SteamState: null);

        Assert.False(item.HasSteamStatus);
        Assert.Null(item.SteamState);
        Assert.Equal(string.Empty, item.SteamStatusLabel);
    }

    private static LibraryItemViewModel CreateSteamItem(
        SteamUpdateState state)
        => new(
            GameId.New(),
            "Steam Game",
            ProviderKind.Steam,
            "Steam",
            @"G:\SteamLibrary\steamapps\common\Game",
            42_000_000_000,
            state);
}
