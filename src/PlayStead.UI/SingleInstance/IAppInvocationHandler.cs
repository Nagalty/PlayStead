using PlayStead.Platform.SingleInstance;

namespace PlayStead.UI.SingleInstance;

public interface IAppInvocationHandler
{
    Task HandleAsync(
        AppInvocation invocation,
        CancellationToken cancellationToken);
}
