using PlayStead.Platform.SingleInstance;

namespace PlayStead.UI.SingleInstance;

public sealed class AppInvocationHandler : IAppInvocationHandler
{
    private readonly IWindowActivator _windowActivator;

    public AppInvocationHandler(
        IWindowActivator windowActivator)
    {
        _windowActivator = windowActivator;
    }

    public async Task HandleAsync(
        AppInvocation invocation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(invocation);

        if (invocation.Activate)
        {
            await _windowActivator.ActivateAsync(
                cancellationToken);
        }

        // Deep-link routing is intentionally deferred in 0.1.
        // Null/empty and unsupported links are safely ignored.
    }
}
