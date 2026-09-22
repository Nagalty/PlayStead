using PlayStead.UI.Tests.TestSupport;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using PlayStead.Core.Library;
using PlayStead.Core.Persistence;
using PlayStead.Core.Scanning;
using PlayStead.UI.Library;
using PlayStead.UI.Shell;

namespace PlayStead.UI.Tests;

public sealed class MainWindowRescanTests
{
    [Fact]
    public void Nested_library_rescan_is_forwarded_by_MainWindow_exactly_once_after_library_navigation()
    {
        var viewModel =
            new LibraryViewModel(
                new EmptyStore());

        RunSta(() =>
        {
            var window =
                new MainWindow(
                    viewModel);

            var count =
                0;

            window.RescanRequested +=
                (_, _) =>
                    count++;

            window.Show();

            window.Dispatcher.Invoke(
                () => { },
                DispatcherPriority.DataBind);

            var primaryNavigation =
                Assert.IsAssignableFrom<
                    FrameworkElement>(
                    window.FindName(
                        "PrimaryNavigation"));

            var shell =
                Assert.IsType<ShellViewModel>(
                    primaryNavigation.DataContext);

            shell.NavigateLibraryCommand.Execute(
                null);

            window.Dispatcher.Invoke(
                () => { },
                DispatcherPriority.DataBind);

            var content =
                Assert.IsType<ContentControl>(
                    window.FindName(
                        "MainContent"));

            var libraryView =
                Assert.IsType<LibraryView>(
                    content.Content);

            var button =
                Assert.IsType<Button>(
                    libraryView.FindName(
                        "RescanButton"));

            button.RaiseEvent(
                new RoutedEventArgs(
                    Button.ClickEvent));

            Assert.Equal(
                1,
                count);

            window.Close();

            return 0;
        });
    }

    private sealed class EmptyStore :
        ILibraryStore
    {
        public Task ApplySourceScanAsync(
            SourceScanResult result,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<LibrarySnapshot> LoadSnapshotAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult(
                new LibrarySnapshot(
                    Array.Empty<LogicalGame>(),
                    Array.Empty<GameInstallation>()));
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
