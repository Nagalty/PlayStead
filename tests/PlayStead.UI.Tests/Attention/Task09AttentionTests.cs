using System.IO;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Data.Sqlite;
using PlayStead.Core.Library;
using PlayStead.Core.Persistence;
using PlayStead.Core.Scanning;
using PlayStead.Data.Database;
using PlayStead.Platform.Paths;
using PlayStead.UI.Bootstrap;
using PlayStead.UI.Controls;
using PlayStead.UI.Library;
using PlayStead.UI.Navigation;
using PlayStead.UI.Tray;
using PlayStead.UI.Tests.TestSupport;

namespace PlayStead.UI.Tests.Attention;

[Collection(PlaySteadWpfApplicationCollection.Name)]
public sealed class Task09AttentionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void No_actionable_local_data_shows_an_explicit_empty_page(bool ordinaryScanData)
    {
        WithWindow(ordinaryScanData, (window, services) =>
        {
            OpenAttention(window);
            var page = RequireAttentionPage(window);
            Assert.NotNull(page.DataContext);
            Assert.Equal("PlayStead.UI.Attention.AttentionViewModel", page.DataContext.GetType().FullName);
            Assert.Same(page.DataContext, services.GetService(page.DataContext.GetType()));

            // Assert the rendered state without prescribing new VM constructors/properties.
            window.Show();
            window.UpdateLayout();
            window.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
            var elements = Descendants(page).ToArray();
            Assert.Contains(elements.OfType<EmptyState>(), state =>
                state.IsVisible && !string.IsNullOrWhiteSpace(state.Message));
            Assert.All(elements.OfType<ItemsControl>().Where(control => control.IsVisible),
                control => Assert.Empty(control.Items.Cast<object>()));
        });
    }

    [Fact]
    public void Existing_top_nav_opens_authoritative_Attention_page_and_Back_preserves_Library()
    {
        WithWindow(false, (window, services) =>
        {
            var navigation = services.GetRequiredService<NavigationService>();
            var library = services.GetRequiredService<LibraryViewModel>();
            library.SetViewMode(LibraryViewMode.List);
            library.SetSearchQuery("local context");
            navigation.Navigate(new NavigationRequest(AppRoute.Library));
            var content = Assert.IsType<ContentControl>(window.FindName("MainContent"));
            var originalLibraryView = content.Content;

            OpenAttention(window);
            Assert.Equal(AppRoute.Attention, navigation.CurrentRoute);
            var page = RequireAttentionPage(window);
            Assert.NotNull(page.DataContext);
            Assert.Equal("PlayStead.UI.Attention.AttentionViewModel", page.DataContext.GetType().FullName);
            Assert.Same(page.DataContext, services.GetService(page.DataContext.GetType()));

            Assert.True(navigation.GoBack());
            Assert.Equal(AppRoute.Library, navigation.CurrentRoute);
            Assert.Same(originalLibraryView, content.Content);
            Assert.Equal(LibraryViewMode.List, library.ViewMode);
            Assert.Equal("local context", library.SearchQuery);
        });
    }

    private static FrameworkElement RequireAttentionPage(MainWindow window)
    {
        var content = Assert.IsType<ContentControl>(window.FindName("MainContent"));
        Assert.True(content.Content is FrameworkElement,
            "AppRoute.Attention must render its page; MainContent.Content is currently null.");
        var page = (FrameworkElement)content.Content;
        Assert.Equal("PlayStead.UI.Attention.AttentionView", page.GetType().FullName);
        return page;
    }

    private static void OpenAttention(MainWindow window)
    {
        var button = Assert.IsType<Button>(window.FindName("AttentionNavButton"));
        var command = Assert.IsAssignableFrom<ICommand>(button.Command);
        Assert.True(command.CanExecute(button.CommandParameter));
        command.Execute(button.CommandParameter);
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        yield return root;
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            foreach (var child in Descendants(VisualTreeHelper.GetChild(root, index)))
                yield return child;
        }
    }

    private static void WithWindow(bool ordinaryScanData, Action<MainWindow, IServiceProvider> assertion)
    {
        PlaySteadWpfTestResources.Run(() =>
        {
            var root = Path.Combine(Path.GetTempPath(), "PlayStead.Tests",
                nameof(Task09AttentionTests), Guid.NewGuid().ToString("N"));
            IHost? host = null;
            try
            {
                var layout = UserDataLayout.FromRoot(root);
                layout.EnsureDirectoriesExist();
                host = PlaySteadHost.Build(layout);
                var services = host.Services;
                services.GetRequiredService<DatabaseInitializer>()
                    .InitializeAsync(CancellationToken.None).GetAwaiter().GetResult();

                if (ordinaryScanData)
                {
                    var store = services.GetRequiredService<ILibraryStore>();
                    var observed = new DateTimeOffset(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);
                    var success = SourceScanResult.Success(ProviderKind.Steam, observed,
                        [DiscoveredInstallation.Create(ProviderKind.Steam, "111", "Local game",
                            Path.Combine(root, "Game"), null, observed)],
                        ["Ordinary scanner diagnostic"]);
                    // Repeated successful discovery is not a decision. Generic scan failures
                    // preserve cached installations; they are not persisted user issues.
                    for (var index = 0; index < 2; index++)
                    {
                        store.ApplySourceScanAsync(success, CancellationToken.None).GetAwaiter().GetResult();
                        store.ApplySourceScanAsync(SourceScanResult.Failure(ProviderKind.Steam,
                            observed, "IOException", "Temporary scanner failure"),
                            CancellationToken.None).GetAwaiter().GetResult();
                    }
                    Assert.Single(store.LoadSnapshotAsync(CancellationToken.None)
                        .GetAwaiter().GetResult().Installations);
                }

                services.GetRequiredService<LibraryViewModel>()
                    .RefreshAsync(CancellationToken.None).GetAwaiter().GetResult();
                var window = services.GetRequiredService<MainWindow>();
                try
                {
                    window.Show();
                    window.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
                    assertion(window, services);
                }
                finally
                {
                    services.GetRequiredService<WindowClosePolicy>().RequestExit();
                    window.WindowStartupLocation = WindowStartupLocation.Manual;
                    window.Left = 100;
                    window.Top = 100;
                    window.Close();
                }
            }
            finally
            {
                host?.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
                host?.Dispose();
                SqliteConnection.ClearAllPools();
                if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            }
        });
    }
}
