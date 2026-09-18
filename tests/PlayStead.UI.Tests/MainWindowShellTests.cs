using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace PlayStead.UI.Tests;

public sealed class MainWindowShellTests
{
    [Fact]
    public void MainWindow_exposes_the_authoritative_top_navigation_shell()
    {
        RunSta(() =>
        {
            var window =
                new MainWindow();

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

                Assert.Equal("Accueil", FindText(home));
                Assert.Equal("Bibliothèque", FindText(library));
                Assert.Equal("À signaler", FindText(attention));
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

    private static string FindText(DependencyObject root)
    {
        if (root is TextBlock textBlock) return textBlock.Text;
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        {
            var text = FindText(child);
            if (!string.IsNullOrEmpty(text)) return text;
        }
        return string.Empty;
    }

    private static T RunSta<T>(
        Func<T> action)
    {
        T? result =
            default;

        Exception? error =
            null;

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
