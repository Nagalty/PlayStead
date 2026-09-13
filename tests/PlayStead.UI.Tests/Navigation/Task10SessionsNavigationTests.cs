using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using PlayStead.UI.Library;

namespace PlayStead.UI.Tests.Navigation;

public sealed class Task10SessionsNavigationTests
{
    [Fact]
    public void MainWindow_exposes_sessions_navigation_and_switches_between_library_and_sessions()
    {
        RunSta(() =>
        {
            var window = new MainWindow();

            try
            {
                var content = Assert.IsType<ContentControl>(
                    window.FindName("MainContent"));

                Assert.IsType<LibraryView>(
                    content.Content);

                var sessionsButton = Assert.IsType<Button>(
                    window.FindName("SessionsNavButton"));

                var libraryButton = Assert.IsType<Button>(
                    window.FindName("LibraryNavButton"));

                sessionsButton.RaiseEvent(
                    new RoutedEventArgs(
                        Button.ClickEvent));

                Assert.Equal(
                    "PlayStead.UI.Sessions.SessionsView",
                    content.Content?.GetType().FullName);

                libraryButton.RaiseEvent(
                    new RoutedEventArgs(
                        Button.ClickEvent));

                Assert.IsType<LibraryView>(
                    content.Content);

                return 0;
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void MainWindow_keeps_the_existing_library_label_and_adds_a_sessions_label()
    {
        RunSta(() =>
        {
            var window = new MainWindow();

            try
            {
                var libraryText = Assert.IsType<TextBlock>(
                    window.FindName("LibraryNavText"));

                var sessionsText = Assert.IsType<TextBlock>(
                    window.FindName("SessionsNavText"));

                Assert.Equal(
                    "Bibliothèque",
                    libraryText.Text);

                Assert.Equal(
                    "Sessions",
                    sessionsText.Text);

                return 0;
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static T RunSta<T>(
        Func<T> action)
    {
        T? result = default;
        Exception? error = null;

        var thread = new Thread(
            () =>
            {
                try
                {
                    result = action();
                }
                catch (Exception exception)
                {
                    error = exception;
                }
            });

        thread.SetApartmentState(
            ApartmentState.STA);

        thread.Start();
        thread.Join();

        if (error is not null)
        {
            ExceptionDispatchInfo
                .Capture(error)
                .Throw();
        }

        return result!;
    }
}
