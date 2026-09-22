using System.Windows.Controls;
using PlayStead.UI.Library;
using PlayStead.UI.Tests.TestSupport;

namespace PlayStead.UI.Tests.Library;

[Collection(PlaySteadWpfApplicationCollection.Name)]
public sealed class LibraryViewTests
{
    [Fact]
    public void LibraryView_exposes_the_minimal_0_1_library_shell()
    {
        PlaySteadWpfTestResources.Run(() =>
        {
            var view = new LibraryView();

            var heading = Assert.IsType<TextBlock>(
                view.FindName("LibraryHeading"));

            var gameList = Assert.IsType<ItemsControl>(
                view.FindName("GameList"));

            var emptyState = Assert.IsType<TextBlock>(
                view.FindName("EmptyStateText"));

            var rescanButton = Assert.IsType<Button>(
                view.FindName("RescanButton"));

            Assert.Equal("Bibliothèque", heading.Text);
            Assert.Equal("Aucun jeu local détecté.", emptyState.Text);
            Assert.Equal("Relancer l’analyse", rescanButton.Content);
            Assert.NotNull(gameList);

            return 0;
        });
    }

}
