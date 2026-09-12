using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Threading;

namespace PlayStead.UI.SingleInstance;

public sealed class MainWindowActivator : IWindowActivator
{
    private readonly IServiceProvider _services;

    public MainWindowActivator(IServiceProvider services)
    {
        _services = services;
    }

    public async Task ActivateAsync(
        CancellationToken cancellationToken)
    {
        var application = Application.Current
            ?? throw new InvalidOperationException(
                "The WPF application is not running.");

        var dispatcher = application.Dispatcher;

        if (dispatcher.CheckAccess())
        {
            ActivateCore(
                _services.GetRequiredService<MainWindow>());

            return;
        }

        await dispatcher.InvokeAsync(
            () => ActivateCore(
                _services.GetRequiredService<MainWindow>()),
            DispatcherPriority.Normal,
            cancellationToken);
    }

    private static void ActivateCore(
        MainWindow window)
    {
        if (!window.IsVisible)
        {
            window.Show();
        }

        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }

        if (window.Activate())
        {
            return;
        }

        window.Topmost = true;
        window.Topmost = false;
        window.Activate();
    }
}
