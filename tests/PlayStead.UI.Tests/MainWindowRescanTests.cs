using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using PlayStead.Core.Library;
using PlayStead.Core.Persistence;
using PlayStead.Core.Scanning;
using PlayStead.UI.Library;

namespace PlayStead.UI.Tests;

public sealed class MainWindowRescanTests
{
    [Fact]
    public void Nested_library_rescan_is_forwarded_by_MainWindow_exactly_once()
    {
        var viewModel = new LibraryViewModel(
            new EmptyStore());

        RunSta(() =>
        {
            var window = new MainWindow(viewModel);

            var count = 0;
            window.RescanRequested += (_, _) => count++;

            var content = Assert.IsType<ContentControl>(
                window.FindName("MainContent"));

            var libraryView = Assert.IsType<LibraryView>(
                content.Content);

            var button = Assert.IsType<Button>(
                libraryView.FindName("RescanButton"));

            button.RaiseEvent(
                new RoutedEventArgs(Button.ClickEvent));

            Assert.Equal(1, count);

            window.Close();

            return 0;
        });
    }

    private sealed class EmptyStore : ILibraryStore
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

    private static T RunSta<T>(Func<T> action)
    {
        T? result = default;
        Exception? error = null;

        var thread = new Thread(() =>
        {
            try { result = action(); }
            catch (Exception ex) { error = ex; }
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
