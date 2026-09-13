using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using PlayStead.Platform.Paths;
using PlayStead.UI.Bootstrap;
using PlayStead.UI.Navigation;

namespace PlayStead.UI.Tests.Navigation;

public sealed class Task10SessionsNavigationTests
{
    [Fact]
    public void Shared_navigation_service_opens_Sessions_contextually_without_a_permanent_primary_nav_button()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "PlayStead.Tests",
                nameof(Task10SessionsNavigationTests),
                Guid.NewGuid().ToString("N"));

        var layout =
            UserDataLayout.FromRoot(
                root);

        layout.EnsureDirectoriesExist();

        using var host =
            PlaySteadHost.Build(
                layout);

        try
        {
            RunSta(() =>
            {
                var navigation =
                    host.Services
                        .GetRequiredService<
                            NavigationService>();

                var window =
                    host.Services
                        .GetRequiredService<
                            MainWindow>();

                try
                {
                    Assert.Null(
                        window.FindName(
                            "SessionsNavButton"));

                    navigation.Navigate(
                        new NavigationRequest(
                            AppRoute.Sessions));

                    var content =
                        Assert.IsType<ContentControl>(
                            window.FindName(
                                "MainContent"));

                    Assert.Equal(
                        "PlayStead.UI.Sessions.SessionsView",
                        content.Content?
                            .GetType()
                            .FullName);

                    return 0;
                }
                finally
                {
                    var policy =
                        host.Services.GetService(
                            typeof(
                                PlayStead.UI.Tray.WindowClosePolicy));

                    policy?
                        .GetType()
                        .GetMethod(
                            "RequestExit")
                        ?.Invoke(
                            policy,
                            null);

                    window.WindowStartupLocation =
                        WindowStartupLocation.Manual;

                    window.Left = 100;
                    window.Top = 100;
                    window.Width = 1280;
                    window.Height = 800;

                    window.Close();
                }
            });
        }
        finally
        {
            if (Directory.Exists(
                    root))
            {
                Directory.Delete(
                    root,
                    recursive: true);
            }
        }
    }

    [Fact]
    public void Sessions_route_can_return_to_Home_through_the_shared_navigation_history()
    {
        var navigation =
            new NavigationService();

        navigation.Navigate(
            new NavigationRequest(
                AppRoute.Sessions));

        Assert.Equal(
            AppRoute.Sessions,
            navigation.CurrentRoute);

        Assert.True(
            navigation.CanGoBack);

        Assert.True(
            navigation.GoBack());

        Assert.Equal(
            AppRoute.Home,
            navigation.CurrentRoute);
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
