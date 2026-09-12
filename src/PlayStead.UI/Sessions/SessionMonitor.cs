using Microsoft.Extensions.Hosting;
using PlayStead.Core.Sessions;

namespace PlayStead.UI.Sessions;

public sealed class SessionMonitor : BackgroundService
{
    private readonly ISessionRuntime _runtime;
    private readonly SessionMonitorOptions _options;
    private readonly Func<
        TimeSpan,
        CancellationToken,
        Task> _delayAsync;

    public SessionMonitor(
        ISessionRuntime runtime,
        SessionMonitorOptions options)
        : this(
            runtime,
            options,
            static (delay, cancellationToken) =>
                Task.Delay(
                    delay,
                    cancellationToken))
    {
    }

    public SessionMonitor(
        ISessionRuntime runtime,
        SessionMonitorOptions options,
        Func<
            TimeSpan,
            CancellationToken,
            Task> delayAsync)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(delayAsync);

        if (options.PollInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                options.PollInterval,
                "Session monitor poll interval must be greater than zero.");
        }

        _runtime = runtime;
        _options = options;
        _delayAsync = delayAsync;
    }

    public async Task RunAsync(
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await _runtime.RefreshAsync(
                cancellationToken);

            try
            {
                await _delayAsync(
                    _options.PollInterval,
                    cancellationToken);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
        }
    }

    protected override Task ExecuteAsync(
        CancellationToken stoppingToken)
        => RunAsync(stoppingToken);
}
