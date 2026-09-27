namespace PlayStead.Providers.Steam;

/// <summary>Watches local Steam manifests and coalesces changes into provider refreshes.</summary>
public sealed class SteamInstallUpdateLiveRefreshService : IDisposable
{
    private readonly WindowsSteamRootLocator _rootLocator;
    private readonly SteamLibraryFoldersReader _folders;
    private Func<CancellationToken, Task>? _refresh;
    private readonly object _gate = new();
    private readonly Dictionary<string, Timer> _debounce = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<FileSystemWatcher> _watchers = [];
    private Timer? _periodic;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private DateTimeOffset? _deactivatedAtUtc;
    private bool _started;
    private bool _disposed;

    public SteamInstallUpdateLiveRefreshService(
        WindowsSteamRootLocator rootLocator,
        SteamLibraryFoldersReader folders,
        Func<CancellationToken, Task>? refresh = null)
    {
        _rootLocator = rootLocator;
        _folders = folders;
        _refresh = refresh;
    }

    public void SetRefresh(Func<CancellationToken, Task> refresh) => _refresh = refresh;

    public TimeSpan DebounceWindow { get; init; } = TimeSpan.FromMilliseconds(750);
    public TimeSpan FallbackInterval { get; init; } = TimeSpan.FromMinutes(15);
    public TimeSpan FocusThreshold { get; init; } = TimeSpan.FromMinutes(5);

    public void NotifyDeactivated(DateTimeOffset atUtc) => _deactivatedAtUtc = atUtc;

    public void NotifyActivated(DateTimeOffset atUtc)
    {
        var deactivated = _deactivatedAtUtc;
        _deactivatedAtUtc = null;
        if (deactivated is not null && atUtc - deactivated.Value >= FocusThreshold)
            QueueRefresh("focus");
    }

    public void Start()
    {
        lock (_gate)
        {
            if (_started || _disposed)
                return;
            _started = true;
        }

        try
        {
            var root = _rootLocator.TryLocate();
            if (root is not null)
            {
                foreach (var library in _folders.Read(root).Distinct(StringComparer.OrdinalIgnoreCase))
                    Watch(Path.Combine(library, "steamapps"));
            }
        }
        catch
        {
            // A missing/inaccessible Steam installation is recoverable by fallback refresh.
        }

        _periodic = new Timer(_ => QueueRefresh("periodic"), null, FallbackInterval, FallbackInterval);
    }

    private void Watch(string directory)
    {
        if (!Directory.Exists(directory))
            return;

        var watcher = new FileSystemWatcher(directory, "appmanifest_*.acf")
        {
            IncludeSubdirectories = false,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true
        };
        watcher.Changed += OnManifestChanged;
        watcher.Created += OnManifestChanged;
        watcher.Renamed += OnManifestRenamed;
        watcher.Error += OnWatcherError;
        lock (_gate)
            _watchers.Add(watcher);
    }

    private void OnManifestChanged(object sender, FileSystemEventArgs e) => QueueForPath(e.FullPath);
    private void OnManifestRenamed(object sender, RenamedEventArgs e) => QueueForPath(e.FullPath);

    private void QueueForPath(string path)
    {
        var file = Path.GetFileNameWithoutExtension(path);
        const string prefix = "appmanifest_";
        if (!file.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
            !uint.TryParse(file[prefix.Length..], out _))
            return;

        lock (_gate)
        {
            if (_disposed)
                return;
            if (_debounce.Remove(file, out var previous))
                previous.Dispose();
            _debounce[file] = new Timer(_ =>
            {
                lock (_gate)
                {
                    if (_debounce.Remove(file, out var current))
                        current.Dispose();
                }
                QueueRefresh(file);
            }, null, DebounceWindow, Timeout.InfiniteTimeSpan);
        }
    }

    private void OnWatcherError(object sender, ErrorEventArgs e) => QueueRefresh("watcher-error");
    private void QueueRefresh(string reason) => _ = RefreshSafelyAsync();

    private async Task RefreshSafelyAsync()
    {
        var refresh = _refresh;
        if (refresh is null)
            return;
        if (!await _refreshGate.WaitAsync(0).ConfigureAwait(false))
            return;
        try { await refresh(CancellationToken.None).ConfigureAwait(false); }
        catch (OperationCanceledException) { }
        catch { }
        finally { _refreshGate.Release(); }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _periodic?.Dispose();
            _periodic = null;
            foreach (var timer in _debounce.Values) timer.Dispose();
            _debounce.Clear();
            foreach (var watcher in _watchers)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }
            _watchers.Clear();
            _refreshGate.Dispose();
        }
    }
}
