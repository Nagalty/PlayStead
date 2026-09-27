using PlayStead.UI.Tests.TestSupport;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using System.Windows.Threading;
using System.Windows.Media;
using PlayStead.UI.Navigation;
using PlayStead.UI.Shell;

namespace PlayStead.UI.Tests.Navigation;

public sealed class Task02ShellNavigationTests
{
    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) yield return match;
            foreach (var descendant in FindVisualChildren<T>(child)) yield return descendant;
        }
    }

    private static IEnumerable<string> FindLogicalText(DependencyObject root)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(root))
        {
            if (child is TextBlock text) yield return text.Text;
            if (child is DependencyObject dependencyObject)
            {
                foreach (var value in FindLogicalText(dependencyObject)) yield return value;
            }
        }
    }
    [Fact]
    public void MainWindow_exposes_the_authoritative_horizontal_primary_navigation()
    {
        RunSta(() =>
        {
            var window = new MainWindow();

            try
            {
                var wordmark =
                    Assert.IsType<Image>(
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

                Assert.True(double.IsNaN(wordmark.Width));
                Assert.Equal(60, wordmark.Height);
                Assert.Equal(System.Windows.Media.Stretch.Uniform, wordmark.Stretch);
                Assert.IsAssignableFrom<System.Windows.Media.Imaging.BitmapSource>(wordmark.Source);
                Assert.Contains("playstead-logo-horizontal.png", wordmark.Source.ToString(), StringComparison.OrdinalIgnoreCase);

                Assert.Contains(
                    "Accueil",
                    FindLogicalText(home));
                Assert.Contains(
                    "Bibliothèque",
                    FindLogicalText(library));
                Assert.Contains(
                    "À signaler",
                    FindLogicalText(attention));
                Assert.Equal("Paramètres", settings.GetValue(AutomationProperties.NameProperty));

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
                        result = PlaySteadWpfTestResources.Run(() => action());
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
