using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Controls;
using PlayStead.UI.Library;

namespace PlayStead.UI.Tests.Library;

public sealed class LibraryViewTests
{
    [Fact]
    public void LibraryView_exposes_the_minimal_0_1_library_shell()
    {
        RunSta(() =>
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

    private static T RunSta<T>(Func<T> action)
    {
        T? result = default;
        Exception? error = null;

        var thread = new Thread(() =>
        {
            try
            {
                result = action();
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (error is not null)
        {
            ExceptionDispatchInfo.Capture(error).Throw();
        }

        return result!;
    }
}
