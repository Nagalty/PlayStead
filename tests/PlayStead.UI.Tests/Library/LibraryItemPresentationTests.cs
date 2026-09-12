using PlayStead.Core.Library;
using PlayStead.UI.Library;

namespace PlayStead.UI.Tests.Library;

public sealed class LibraryItemPresentationTests
{
    [Theory]
    [InlineData(42_000_000_000L, "42,0 Go")]
    [InlineData(1_500_000_000L, "1,5 Go")]
    public void InstalledSizeLabel_formats_known_decimal_gigabytes(
        long bytes,
        string expected)
    {
        var item = new LibraryItemViewModel(
            GameId.New(),
            "Game",
            ProviderKind.Steam,
            "Steam",
            @"G:\Game",
            bytes);

        Assert.Equal(expected, item.InstalledSizeLabel);
    }

    [Fact]
    public void InstalledSizeLabel_is_unknown_when_size_is_missing()
    {
        var item = new LibraryItemViewModel(
            GameId.New(),
            "Game",
            ProviderKind.Steam,
            "Steam",
            @"G:\Game",
            null);

        Assert.Equal("Taille inconnue", item.InstalledSizeLabel);
    }
}
