using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using PlayStead.UI.Navigation;
using PlayStead.UI.Shell;

namespace PlayStead.UI.Tests.Navigation;

public sealed class Task02ShellNavigationTests
{
    [Fact]
    public void MainWindow_exposes_the_authoritative_horizontal_primary_navigation()
    {
        RunSta(() =>
        {
            var window = new MainWindow();

            try
            {
                var wordmark =
                    Assert.IsType<TextBlock>(
                        window.FindName(
                            "PlaySteadWordmark"));

                var home =
                    Assert.IsType<Button>(
                        window.FindName(
                            "HomeNavButton"));

                var library =
                    Assert.IsType<Button>(
                        window.FindName(
                            "LibraryNavButton"));

                var attention =
                    Assert.IsType<Button>(
                        window.FindName(
                            "AttentionNavButton"));

                var settings =
                    Assert.IsType<Button>(
                        window.FindName(
                            "SettingsNavButton"));

                Assert.Equal(
                    "PlayStead",
                    wordmark.Text);

                Assert.Equal(
                    "Accueil",
                    Assert.IsType<TextBlock>(
                        home.Content).Text);

                Assert.Equal(
                    "Bibliothèque",
                    Assert.IsType<TextBlock>(
                        library.Content).Text);

                Assert.Equal(
                    "À signaler",
                    Assert.IsType<TextBlock>(
                        attention.Content).Text);

                Assert.Equal(
                    "Paramètres",
                    Assert.IsType<TextBlock>(
                        settings.Content).Text);

                Assert.Null(
                    window.FindName(
                        "SessionsNavButton"));

                return 0;
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void MainWindow_primary_navigation_buttons_drive_the_shell_view_model()
    {
        RunSta(() =>
        {
            var window = new MainWindow();

            try
            {
                window.Show();

                window.Dispatcher.Invoke(
                    () => { },
                    DispatcherPriority.DataBind);

                var navigationHost =
                    Assert.IsAssignableFrom<FrameworkElement>(
                        window.FindName(
                            "PrimaryNavigation"));

                var shell =
                    Assert.IsType<ShellViewModel>(
                        navigationHost.DataContext);

                RaiseClick(
                    window,
                    "LibraryNavButton");

                Assert.Equal(
                    AppRoute.Library,
                    shell.CurrentRoute);

                RaiseClick(
                    window,
                    "AttentionNavButton");

                Assert.Equal(
                    AppRoute.Attention,
                    shell.CurrentRoute);

                RaiseClick(
                    window,
                    "SettingsNavButton");

                Assert.Equal(
                    AppRoute.Settings,
                    shell.CurrentRoute);

                RaiseClick(
                    window,
                    "HomeNavButton");

                Assert.Equal(
                    AppRoute.Home,
                    shell.CurrentRoute);

                return 0;
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void MainWindow_source_wires_Alt_Left_to_shell_GoBackCommand()
    {
        var source =
            File.ReadAllText(
                FindRepositoryFile(
                    "src",
                    "PlayStead.UI",
                    "MainWindow.xaml.cs"));

        Assert.Contains(
            "ModifierKeys.Alt",
            source,
            StringComparison.Ordinal);

        Assert.Contains(
            "Key.Left",
            source,
            StringComparison.Ordinal);

        Assert.Contains(
            "GoBackCommand",
            source,
            StringComparison.Ordinal);

        Assert.Contains(
            "PreviewKeyDown",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindow_source_no_longer_owns_a_permanent_Sessions_nav_click_handler()
    {
        var source =
            File.ReadAllText(
                FindRepositoryFile(
                    "src",
                    "PlayStead.UI",
                    "MainWindow.xaml.cs"));

        Assert.DoesNotContain(
            "SessionsNavButton_OnClick",
            source,
            StringComparison.Ordinal);
    }

    private static void RaiseClick(
        MainWindow window,
        string name)
    {
        var button =
            Assert.IsType<Button>(
                window.FindName(
                    name));

        var command =
            Assert.IsAssignableFrom<System.Windows.Input.ICommand>(
                button.Command);

        Assert.True(
            command.CanExecute(
                button.CommandParameter));

        command.Execute(
            button.CommandParameter);
    }

    private static string FindRepositoryFile(
        params string[] relativeParts)
    {
        var starts =
            new[]
            {
                Directory.GetCurrentDirectory(),
                AppContext.BaseDirectory
            };

        foreach (var start in starts)
        {
            var current =
                new DirectoryInfo(
                    Path.GetFullPath(
                        start));

            while (current is not null)
            {
                var candidateParts =
                    new string[
                        relativeParts.Length + 1];

                candidateParts[0] =
                    current.FullName;

                Array.Copy(
                    relativeParts,
                    0,
                    candidateParts,
                    1,
                    relativeParts.Length);

                var candidate =
                    Path.Combine(
                        candidateParts);

                if (File.Exists(
                        candidate))
                {
                    return candidate;
                }

                current =
                    current.Parent;
            }
        }

        throw new FileNotFoundException(
            $"Could not locate repository file: {Path.Combine(relativeParts)}");
    }

    private static T RunSta<T>(
        Func<T> action)
    {
        T? result = default;
        Exception? error = null;

        var thread =
            new Thread(
                () =>
                {
                    try
                    {
                        result =
                            action();
                    }
                    catch (
                        Exception exception)
                    {
                        error =
                            exception;
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
