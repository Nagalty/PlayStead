using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Controls;
using PlayStead.UI.Library;

namespace PlayStead.UI.Tests;

public sealed class MainWindowShellTests
{
    [Fact]
    public void MainWindow_exposes_the_minimal_top_navigation_shell()
    {
        RunSta(() =>
        {
            var window = new MainWindow();

            var wordmark = Assert.IsType<TextBlock>(
                window.FindName("PlaySteadWordmark"));

            var home = Assert.IsType<TextBlock>(
                window.FindName("HomeNavText"));

            var library = Assert.IsType<TextBlock>(
                window.FindName("LibraryNavText"));

            var content = Assert.IsType<ContentControl>(
                window.FindName("MainContent"));

            Assert.Equal("PlayStead", wordmark.Text);
            Assert.Equal("Accueil", home.Text);
            Assert.Equal("Bibliothèque", library.Text);
            Assert.IsType<LibraryView>(content.Content);

            window.Close();

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
