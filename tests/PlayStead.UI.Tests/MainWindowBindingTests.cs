using PlayStead.UI.Tests.TestSupport;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Threading;
using PlayStead.Core.Library;
using PlayStead.Core.Persistence;
using PlayStead.Core.Scanning;
using PlayStead.UI.Library;
using PlayStead.UI.Shell;

namespace PlayStead.UI.Tests;

public sealed class MainWindowBindingTests
{
    [Fact]
    public async Task MainWindow_binds_LibraryView_to_the_registered_LibraryViewModel_after_library_navigation()
    {
        var now =
            new DateTimeOffset(
                2026,
                9,
                12,
                10,
                0,
                0,
                TimeSpan.Zero);

        var gameId =
            GameId.New();

        var snapshot =
            new LibrarySnapshot(
                [
                    new LogicalGame(
                        gameId,
                        "Arma Reforger",
                        IsHidden: false,
                        CreatedAtUtc: now,
                        UpdatedAtUtc: now)
                ],
                [
                    new GameInstallation(
                        InstallationId.New(),
                        gameId,
                        ProviderKind.Steam,
                        "1874880",
                        @"G:\SteamLibrary\steamapps\common\Arma Reforger",
                        42_000_000_000,
                        IsPreferred: true,
                        IsPresent: true,
                        LastSeenUtc: now)
                ]);

        var viewModel =
            new LibraryViewModel(
                new StubLibraryStore(
                    snapshot));

        await viewModel.RefreshAsync(
            CancellationToken.None);

        RunSta(() =>
        {
            var window =
                new MainWindow(
                    viewModel);

            window.Show();

            window.Dispatcher.Invoke(
                () => { },
                DispatcherPriority.DataBind);

            Assert.Same(
                viewModel,
                window.DataContext);

            var primaryNavigation =
                Assert.IsAssignableFrom<
                    System.Windows.FrameworkElement>(
                    window.FindName(
                        "PrimaryNavigation"));

            var shell =
                Assert.IsType<ShellViewModel>(
                    primaryNavigation.DataContext);

            shell.NavigateLibraryCommand.Execute(
                null);

            window.Dispatcher.Invoke(
                () => { },
                DispatcherPriority.Loaded);

            window.Dispatcher.Invoke(
                () => { },
                DispatcherPriority.DataBind);

            window.UpdateLayout();

            var content =
                Assert.IsType<ContentControl>(
                    window.FindName(
                        "MainContent"));

            var libraryView =
                Assert.IsType<LibraryView>(
                    content.Content);

            Assert.Same(
                viewModel,
                libraryView.DataContext);

            var gameList =
                Assert.IsType<ItemsControl>(
                    libraryView.FindName(
                        "GameList"));

            Assert.Same(
                viewModel.Items,
                gameList.ItemsSource);

            window.Close();

            return 0;
        });
    }

    private sealed class StubLibraryStore(
        LibrarySnapshot snapshot) :
        ILibraryStore
    {
        public Task ApplySourceScanAsync(
            SourceScanResult result,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<LibrarySnapshot> LoadSnapshotAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult(
                snapshot);
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
