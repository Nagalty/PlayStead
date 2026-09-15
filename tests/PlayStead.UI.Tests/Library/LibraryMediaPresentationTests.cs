using PlayStead.UI.Library;

namespace PlayStead.UI.Tests.Library;

public sealed class LibraryMediaPresentationTests
{
    [Fact]
    public void New_item_has_no_cover()
    {
        var item = CreateItem();

        Assert.Null(item.CoverPath);
        Assert.False(item.HasCover);
    }

    [Fact]
    public void SetCoverPath_updates_cover_state_and_notifies_bindings()
    {
        var item = CreateItem();
        var raised = new List<string?>();

        item.PropertyChanged += (_, e) =>
            raised.Add(e.PropertyName);

        item.SetCoverPath(@"C:\Media\cover.jpg");

        Assert.Equal(
            @"C:\Media\cover.jpg",
            item.CoverPath);
        Assert.True(item.HasCover);
        Assert.Contains(
            nameof(LibraryItemViewModel.CoverPath),
            raised);
        Assert.Contains(
            nameof(LibraryItemViewModel.HasCover),
            raised);
    }

    private static LibraryItemViewModel CreateItem() =>
        new(
            default,
            "Arma Reforger",
            PlayStead.Core.Library.ProviderKind.Steam,
            "Steam",
            @"C:\Games\Arma Reforger",
            null,
            null,
            false);
}
