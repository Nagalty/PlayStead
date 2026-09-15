using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PlayStead.Core.Sessions;

namespace PlayStead.UI.Sessions;

public sealed class SessionMonitor : BackgroundService
{
    private readonly ISessionRuntime _runtime;
    private readonly SessionMonitorOptions _options;
    private readonly ILogger<SessionMonitor> _logger;
    private bool _captureUnavailable;
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
                    cancellationToken),
            NullLogger<SessionMonitor>.Instance)
    {
    }

    public SessionMonitor(ISessionRuntime runtime, SessionMonitorOptions options,
        ILogger<SessionMonitor> logger)
        : this(runtime, options, static (delay, cancellationToken) =>
            Task.Delay(delay, cancellationToken), logger)
    {
    }

    public SessionMonitor(
        ISessionRuntime runtime,
        SessionMonitorOptions options,
        Func<
            TimeSpan,
            CancellationToken,
            Task> delayAsync)
        : this(runtime, options, delayAsync, NullLogger<SessionMonitor>.Instance)
    {
    }

    public SessionMonitor(ISessionRuntime runtime, SessionMonitorOptions options,
        Func<TimeSpan, CancellationToken, Task> delayAsync,
        ILogger<SessionMonitor> logger)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(delayAsync);
        ArgumentNullException.ThrowIfNull(logger);

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
        _logger = logger;
    }

    public SessionRuntimeSnapshot? LatestSnapshot
    {
        get;
        private set;
    }

    public event Action<SessionRuntimeSnapshot>?
        SnapshotUpdated;

    public async Task RunAsync(
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            SessionRuntimeSnapshot? snapshot = null;
            try
            {
                snapshot = await _runtime.RefreshAsync(cancellationToken);
                _captureUnavailable = false;
            }
            catch (ProcessCaptureUnavailableException error)
            {
                // SessionRuntime has marked this failed capture as a discovery gap.
                // No snapshot means no false process absence or persisted heartbeat.
                if (!_captureUnavailable)
                    _logger.LogWarning(error,
                        "Process capture unavailable; session monitor will retry next cycle");
                _captureUnavailable = true;
            }
            if (snapshot is not null)
            {
                LatestSnapshot = snapshot;
                SnapshotUpdated?.Invoke(snapshot);
            }

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
