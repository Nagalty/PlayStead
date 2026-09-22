using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using PlayStead.Platform.Paths;
using PlayStead.UI.Bootstrap;
using PlayStead.UI.Tests.TestSupport;

namespace PlayStead.UI.Tests.Tray;

[Collection(PlaySteadWpfApplicationCollection.Name)]
public sealed class Task09TrayLifecycleTests
{
    [Fact]
    public void App_uses_explicit_shutdown_mode()
    {
        var source = File.ReadAllText(
            FindRepositoryFile(
                "src",
                "PlayStead.UI",
                "App.xaml"));

        Assert.Contains(
            "ShutdownMode=\"OnExplicitShutdown\"",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void UI_project_enables_Windows_Forms_interop_for_native_notify_icon()
    {
        var source = File.ReadAllText(
            FindRepositoryFile(
                "src",
                "PlayStead.UI",
                "PlayStead.UI.csproj"));

        Assert.Contains(
            "<UseWindowsForms>true</UseWindowsForms>",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Host_registers_tray_icon_service_and_window_close_policy()
    {
        var assembly = typeof(MainWindow).Assembly;

        var trayType = assembly.GetType(
            "PlayStead.UI.Tray.TrayIconService");

        var policyType = assembly.GetType(
            "PlayStead.UI.Tray.WindowClosePolicy");

        Assert.NotNull(trayType);
        Assert.NotNull(policyType);

        var root = Path.Combine(
            Path.GetTempPath(),
            "PlayStead.Tests",
            Guid.NewGuid().ToString("N"));

        using var host = PlaySteadHost.Build(
            UserDataLayout.FromRoot(root));

        var probe =
            host.Services.GetRequiredService<IServiceProviderIsService>();

        Assert.True(probe.IsService(trayType));
        Assert.True(probe.IsService(policyType));
    }

    [Fact]
    public void Tray_service_exposes_open_quit_status_and_exit_contract()
    {
        var trayType = typeof(MainWindow).Assembly.GetType(
            "PlayStead.UI.Tray.TrayIconService");

        Assert.NotNull(trayType);

        Assert.NotNull(
            trayType.GetMethod(
                "Start",
                BindingFlags.Instance | BindingFlags.Public));

        Assert.NotNull(
            trayType.GetMethod(
                "OpenAsync",
                BindingFlags.Instance | BindingFlags.Public));

        Assert.NotNull(
            trayType.GetMethod(
                "RequestExit",
                BindingFlags.Instance | BindingFlags.Public));

        Assert.NotNull(
            trayType.GetMethod(
                "UpdateSessionCount",
                BindingFlags.Instance | BindingFlags.Public));

        Assert.NotNull(
            trayType.GetEvent(
                "ExitRequested",
                BindingFlags.Instance | BindingFlags.Public));
    }

    [Theory]
    [InlineData(0, "PlayStead — aucune session active")]
    [InlineData(1, "PlayStead — 1 session active")]
    [InlineData(3, "PlayStead — 3 sessions actives")]
    public void Tray_status_text_describes_active_session_count(
        int activeSessionCount,
        string expected)
    {
        var statusType = typeof(MainWindow).Assembly.GetType(
            "PlayStead.UI.Tray.TrayStatusText");

        Assert.NotNull(statusType);

        var format = statusType.GetMethod(
            "Format",
            BindingFlags.Static | BindingFlags.Public);

        Assert.NotNull(format);

        var actual = Assert.IsType<string>(
            format.Invoke(
                null,
                [activeSessionCount]));

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Closing_DI_created_main_window_hides_it_and_it_can_be_shown_again()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "PlayStead.Tests",
            Guid.NewGuid().ToString("N"));

        var layout = UserDataLayout.FromRoot(root);
        layout.EnsureDirectoriesExist();

        using var host = PlaySteadHost.Build(layout);

        RunSta(
            () =>
            {
                var window =
                    host.Services.GetRequiredService<MainWindow>();

                try
                {
                    window.Show();

                    window.Close();

                    Assert.False(window.IsVisible);

                    window.Show();

                    Assert.True(window.IsVisible);
                }
                finally
                {
                    RequestExplicitExitIfAvailable(
                        host.Services);

                    if (window.IsVisible)
                    {
                        window.Close();
                    }
                }
            });
    }

    private static void RequestExplicitExitIfAvailable(
        IServiceProvider services)
    {
        var policyType = typeof(MainWindow).Assembly.GetType(
            "PlayStead.UI.Tray.WindowClosePolicy");

        if (policyType is null)
        {
            return;
        }

        var policy = services.GetService(policyType);

        policyType.GetMethod(
                "RequestExit",
                BindingFlags.Instance | BindingFlags.Public)
            ?.Invoke(
                policy,
                null);
    }

    private static void RunSta(
        Action action)
        => PlaySteadWpfTestResources.Run(action);

    private static string FindRepositoryFile(
        params string[] relativeParts)
    {
        var starts = new[]
        {
            Directory.GetCurrentDirectory(),
            AppContext.BaseDirectory
        };

        foreach (var start in starts)
        {
            var current = new DirectoryInfo(
                Path.GetFullPath(start));

            while (current is not null)
            {
                var candidateParts =
                    new string[relativeParts.Length + 1];

                candidateParts[0] = current.FullName;

                Array.Copy(
                    relativeParts,
                    0,
                    candidateParts,
                    1,
                    relativeParts.Length);

                var candidate = Path.Combine(candidateParts);

                if (File.Exists(candidate))
                {
                    return candidate;
                }

                current = current.Parent;
            }
        }

        throw new FileNotFoundException(
            $"Could not locate repository file: {Path.Combine(relativeParts)}");
    }
}
