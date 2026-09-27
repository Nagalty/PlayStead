using System.Diagnostics;
using PlayStead.Core.Updates;

namespace PlayStead.UI.Bootstrap;

/// <summary>Starts the selected channel check after the shell has signalled Ready.</summary>
public sealed class AppUpdateCoordinator
{
    private readonly IAppUpdateService _service;
    private readonly IAppUpdatePackageDownloader? _packageDownloader;
    private readonly IAppUpdateInstaller? _installer;
    private readonly object _gate = new();
    private Task? _checkTask;
    private bool _started;
    private bool _installationStarted;
    private Task? _downloadTask;

    public AppUpdateCoordinator(IAppUpdateService service, IAppUpdatePackageDownloader? packageDownloader = null, IAppUpdateInstaller? installer = null)
    {
        _service = service;
        _packageDownloader = packageDownloader;
        _installer = installer;
        LastState = service.Current;
    }

    public AppUpdateState? LastState { get; private set; }

    public event EventHandler<AppUpdateState>? StateChanged;

    public void StartPostReadyCheck(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_started)
                return;

            _started = true;
            _checkTask = CheckAsync(cancellationToken);
        }
    }

    public Task? CurrentCheck
    {
        get
        {
            lock (_gate)
                return _checkTask;
        }
    }

    public bool CanDownload =>
        _packageDownloader is not null &&
        LastState is { Status: AppUpdateStatus.UpdateAvailable, Channel: DistributionChannel.GitHub, Action: AppUpdateActionKind.DownloadAndInstall, ActionUri: not null, AvailableVersion: not null };

    public Task? CurrentDownload
    {
        get
        {
            lock (_gate)
                return _downloadTask;
        }
    }

    public bool CanInstall => _installer is not null && LastState is
        { Status: AppUpdateStatus.ReadyToInstall, Channel: DistributionChannel.GitHub, LocalPackagePath: not null, AvailableVersion: not null };

    public bool StartInstallation()
    {
        lock (_gate)
        {
            if (!CanInstall || _installer is null) return false;
            if (_installationStarted) return false;
            _installationStarted = true;
            var started = _installer.TryStart(LastState!);
            if (!started) _installationStarted = false;
            return started;
        }
    }

    public void StartGitHubDownload()
    {
        lock (_gate)
        {
            if (_downloadTask is { IsCompleted: false } || !CanDownload)
                return;
            _downloadTask = DownloadAsync();
        }
    }

    private async Task CheckAsync(CancellationToken cancellationToken)
    {
        try
        {
            LastState = await _service.CheckAsync(cancellationToken).ConfigureAwait(false);
            StateChanged?.Invoke(this, LastState);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            Trace.WriteLine($"[APP-UPDATE] CheckFailed Type={exception.GetType().Name}");
            LastState = new AppUpdateState(
                AppUpdateStatus.Error,
                _service.Channel,
                _service.Current.CurrentVersion,
                Error: exception.Message);
            StateChanged?.Invoke(this, LastState);
        }
    }

    private async Task DownloadAsync()
    {
        var state = LastState;
        if (_packageDownloader is null || state is not
            {
                Status: AppUpdateStatus.UpdateAvailable,
                Channel: DistributionChannel.GitHub,
                Action: AppUpdateActionKind.DownloadAndInstall,
                ActionUri: not null,
                AvailableVersion: not null
            })
            return;

        SetState(state with
        {
            Status = AppUpdateStatus.Downloading,
            BytesReceived = 0,
            TotalBytes = null,
            Error = null
        });
        try
        {
            var progress = new Progress<AppUpdateDownloadProgress>(value =>
                SetState(LastState! with
                {
                    Status = AppUpdateStatus.Downloading,
                    BytesReceived = value.BytesReceived,
                    TotalBytes = value.TotalBytes
                }));
            var result = await _packageDownloader.DownloadAsync(
                state.ActionUri,
                state.ExpectedSha256 ?? throw new InvalidOperationException("The validated manifest hash is unavailable."),
                state.AvailableVersion,
                progress).ConfigureAwait(false);
            SetState(state with
            {
                Status = AppUpdateStatus.ReadyToInstall,
                LocalPackagePath = result.LocalPackagePath,
                BytesReceived = result.BytesReceived,
                TotalBytes = result.TotalBytes,
                Error = null
            });
        }
        catch (Exception exception)
        {
            Trace.WriteLine($"[APP-UPDATE] DownloadFailed Type={exception.GetType().Name}");
            SetState(state with { Status = AppUpdateStatus.Error, Error = exception.Message, LocalPackagePath = null });
        }
    }

    private void SetState(AppUpdateState state)
    {
        LastState = state;
        StateChanged?.Invoke(this, state);
    }
}
