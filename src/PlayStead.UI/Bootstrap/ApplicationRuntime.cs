using PlayStead.Data.Database;
using PlayStead.Platform.SingleInstance;
using PlayStead.UI.Library;
using PlayStead.UI.SingleInstance;

namespace PlayStead.UI.Bootstrap;

public sealed class ApplicationRuntime
{
    private readonly LocalStartupPipeline _startupPipeline;
    private readonly LibraryViewModel _libraryViewModel;
    private readonly IAppInvocationHandler _invocationHandler;

    public ApplicationRuntime(
        LocalStartupPipeline startupPipeline,
        LibraryViewModel libraryViewModel,
        IAppInvocationHandler invocationHandler)
    {
        _startupPipeline = startupPipeline;
        _libraryViewModel = libraryViewModel;
        _invocationHandler = invocationHandler;
    }

    public async Task<DatabaseHealthResult> InitializeAsync(
        CancellationToken cancellationToken)
    {
        var state = await _startupPipeline.InitializeAsync(
            cancellationToken);

        if (!state.Health.IsHealthy)
        {
            return state.Health;
        }

        await _libraryViewModel.RefreshAsync(
            cancellationToken);

        return state.Health;
    }

    public async Task RefreshAsync(
        CancellationToken cancellationToken)
    {
        await _startupPipeline.RefreshAsync(
            cancellationToken);

        await _libraryViewModel.RefreshAsync(
            cancellationToken);
    }

    public Task HandleInvocationAsync(
        AppInvocation invocation,
        CancellationToken cancellationToken) =>
        _invocationHandler.HandleAsync(
            invocation,
            cancellationToken);
}
