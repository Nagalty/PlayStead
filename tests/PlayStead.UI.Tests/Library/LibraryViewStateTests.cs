using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using PlayStead.Core.Library;
using PlayStead.Core.Persistence;
using PlayStead.Core.Scanning;
using PlayStead.UI.Library;

namespace PlayStead.UI.Tests.Library;

public sealed class LibraryViewStateTests
{
    [Fact]
    public async Task Empty_library_shows_empty_state_and_hides_game_list()
    {
        var vm = new LibraryViewModel(
            new StubLibraryStore(
                new LibrarySnapshot(
                    Array.Empty<LogicalGame>(),
                    Array.Empty<GameInstallation>())));

        await vm.RefreshAsync(CancellationToken.None);

        RunSta(() =>
        {
            var view = new LibraryView { DataContext = vm };
            using var host = Show(view);

            var list = Assert.IsType<ItemsControl>(
                view.FindName("GameList"));

            var empty = Assert.IsType<StackPanel>(
                view.FindName("EmptyStatePanel"));

            Assert.Equal(Visibility.Collapsed, list.Visibility);
            Assert.Equal(Visibility.Visible, empty.Visibility);

            return 0;
        });
    }

    [Fact]
    public async Task Populated_library_shows_default_grid_and_hides_list_and_empty_state()
    {
        var now = DateTimeOffset.UtcNow;
        var gameId = GameId.New();

        var vm = new LibraryViewModel(
            new StubLibraryStore(
                new LibrarySnapshot(
                    [
                        new LogicalGame(
                            gameId,
                            "Arma Reforger",
                            false,
                            now,
                            now)
                    ],
                    [
                        new GameInstallation(
                            InstallationId.New(),
                            gameId,
                            ProviderKind.Steam,
                            "1874880",
                            @"G:\SteamLibrary\steamapps\common\Arma Reforger",
                            42_000_000_000,
                            true,
                            true,
                            now)
                    ])));

        await vm.RefreshAsync(CancellationToken.None);

        RunSta(() =>
        {
            var view = new LibraryView { DataContext = vm };
            using var host = Show(view);

            var grid = Assert.IsType<ItemsControl>(
                view.FindName("GameGridRows"));

            var list = Assert.IsType<ItemsControl>(
                view.FindName("GameList"));

            var empty = Assert.IsType<StackPanel>(
                view.FindName("EmptyStatePanel"));

            Assert.Equal(Visibility.Visible, grid.Visibility);
            Assert.Equal(Visibility.Collapsed, list.Visibility);
            Assert.Equal(Visibility.Collapsed, empty.Visibility);

            return 0;
        });
    }

    private static WindowHost Show(FrameworkElement content)
    {
        var window = new Window
        {
            Width = 1000,
            Height = 700,
            Content = content,
            ShowInTaskbar = false
        };

        window.Show();
        window.Dispatcher.Invoke(
            () => { },
            DispatcherPriority.DataBind);
        window.UpdateLayout();

        return new WindowHost(window);
    }

    private sealed class WindowHost(Window window) : IDisposable
    {
        public void Dispose() => window.Close();
    }

    private sealed class StubLibraryStore(
        LibrarySnapshot snapshot) : ILibraryStore
    {
        public Task ApplySourceScanAsync(
            SourceScanResult result,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<LibrarySnapshot> LoadSnapshotAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult(snapshot);
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
