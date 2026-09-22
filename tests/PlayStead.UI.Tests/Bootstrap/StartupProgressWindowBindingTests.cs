using System.Runtime.ExceptionServices;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using PlayStead.Platform.Paths;
using PlayStead.UI.Bootstrap;
using PlayStead.UI.Navigation;
using PlayStead.UI.Tests.TestSupport;

namespace PlayStead.UI.Tests.Bootstrap;

[Collection(PlaySteadWpfApplicationCollection.Name)]
public sealed class StartupProgressWindowBindingTests
{
    [Fact]
    public void Main_window_overlay_tracks_the_host_singleton_startup_state()
    {
        RunSta(() =>
        {
            var layout = UserDataLayout.FromRoot(Path.Combine(
                Path.GetTempPath(), "PlayStead.Tests", "StartupProgress", Guid.NewGuid().ToString("N")));
            using var host = PlaySteadHost.Build(layout);
            var state = host.Services.GetRequiredService<StartupProgressState>();
            var window = host.Services.GetRequiredService<MainWindow>();

            Assert.Same(state, window.StartupProgress);
            var property = typeof(MainWindow).GetProperty(nameof(MainWindow.StartupProgress));
            Assert.NotNull(property);
            Assert.Equal(NullabilityState.NotNull, new NullabilityInfoContext().Create(property!).ReadState);
            Assert.False(property.CanWrite);

            host.Services.GetRequiredService<NavigationService>()
                .Navigate(new NavigationRequest(AppRoute.Library));
            state.Begin();
            window.Show();
            window.UpdateLayout();
            window.Dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
            var overlay = Assert.IsAssignableFrom<FrameworkElement>(window.FindName("StartupOverlay"));
            Assert.Equal(Visibility.Visible, overlay.Visibility);
            var progressBar = Assert.IsType<System.Windows.Controls.ProgressBar>(window.FindName("StartupProgressBar"));
            Assert.True(progressBar.IsIndeterminate);

            state.Report(StartupStage.EnrichingCatalog, "Enrichissement du catalogue Steam", 18, 30);
            window.Dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
            Assert.False(progressBar.IsIndeterminate);
            Assert.Equal(30, progressBar.Maximum);
            Assert.Equal(18, progressBar.Value);
            var countLabel = Assert.IsType<System.Windows.Controls.TextBlock>(window.FindName("StartupProgressCountLabel"));
            Assert.Equal("18 / 30 jeux", countLabel.Text);
            Assert.Equal(Visibility.Visible, countLabel.Visibility);

            state.Ready();
            window.Dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
            Assert.False(state.IsBusy);
            Assert.False(progressBar.IsIndeterminate);
            Assert.Equal(Visibility.Collapsed, overlay.Visibility);
            Assert.Equal(Visibility.Collapsed, countLabel.Visibility);

            window.Close();
            return 0;
        });
    }

    private static T RunSta<T>(Func<T> action) => PlaySteadWpfTestResources.Run(action);
}
