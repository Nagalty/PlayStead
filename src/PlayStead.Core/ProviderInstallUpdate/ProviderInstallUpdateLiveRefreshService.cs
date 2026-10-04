namespace PlayStead.Core.ProviderInstallUpdate;

/// <summary>Provider-neutral, coalescing timer for lightweight install/update refreshes.</summary>
public sealed class ProviderInstallUpdateLiveRefreshService : IDisposable
{
    private readonly object _gate = new();
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private Timer? _timer;
    private Func<CancellationToken, Task>? _refresh;
    private bool _queued;
    private bool _rerun;
    private bool _started;
    private bool _disposed;

    public TimeSpan Interval { get; init; } = TimeSpan.FromSeconds(30);

    public void SetRefresh(Func<CancellationToken, Task> refresh) => _refresh = refresh;

    public void Start()
    {
        lock (_gate)
        {
            if (_started || _disposed) return;
            _started = true;
            _timer = new Timer(_ => Trigger(), null, Interval, Interval);
        }
    }

    public void Trigger()
    {
        lock (_gate)
        {
            if (_disposed) return;
            if (_queued) { _rerun = true; return; }
            _queued = true;
        }
        _ = RunAsync();
    }

    private async Task RunAsync()
    {
        try
        {
            if (!await _refreshGate.WaitAsync(0).ConfigureAwait(false)) return;
            try
            {
                var refresh = _refresh;
                if (refresh is not null) await refresh(CancellationToken.None).ConfigureAwait(false);
            }
            finally { _refreshGate.Release(); }
        }
        catch (OperationCanceledException) { }
        catch { }
        finally
        {
            var rerun = false;
            lock (_gate)
            {
                rerun = _rerun;
                _rerun = false;
                if (!rerun) _queued = false;
            }
            if (rerun) _ = RunAsync();
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _timer?.Dispose();
            _timer = null;
        }
        _refreshGate.Dispose();
    }
}
