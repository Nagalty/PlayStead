using PlayStead.Core.Library;
using PlayStead.Data.Database;
using PlayStead.Platform.Paths;
using PlayStead.Platform.SingleInstance;
using System.Runtime.ExceptionServices;

namespace PlayStead.UI.Bootstrap;

public sealed class ApplicationStartupCoordinator
{
    private readonly Operations _operations;
    private readonly object _lifecycleGate = new();
    private readonly HashSet<Task> _refreshTasks = [];
    private Task? _shutdownTask;
    private bool _stopping;
    private bool _primaryAcquired;
    private bool _hostBuilt;

    public ApplicationStartupCoordinator(
        Operations operations)
    {
        _operations =
            operations
            ?? throw new ArgumentNullException(
                nameof(operations));
    }

    public Task? BackgroundRefreshTask
    {
        get;
        private set;
    }

    public async Task<StartResult> StartAsync(
        IReadOnlyList<string> args,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);

        var invocation =
            _operations.ParseInvocation(args);

        var singleInstanceResult =
            await _operations.AcquireSingleInstanceAsync(
                invocation,
                cancellationToken);

        if (singleInstanceResult ==
            SingleInstanceResult.Forwarded)
        {
            return StartResult.Forwarded;
        }

        _primaryAcquired = true;

        var layout =
            _operations.CreateUserDataLayout();

        _operations.EnsureDirectoriesExist(
            layout);

        await _operations.BuildHostAsync(
            layout,
            cancellationToken);

        _hostBuilt = true;

        if (_operations.EnsureMainWindowShownAsync is { } showWindow)
            await showWindow(cancellationToken);

        System.Diagnostics.Trace.WriteLine("[STARTUP] BEGIN InitializeLocalStateAsync");
        var localState =
            await _operations.InitializeLocalStateAsync(
                cancellationToken);
        System.Diagnostics.Trace.WriteLine("[STARTUP] END InitializeLocalStateAsync");

        if (!localState.Health.IsHealthy)
        {
            await _operations.ShowDatabaseRecoveryRequiredAsync(
                localState.Health,
                cancellationToken);

            return StartResult.DatabaseUnhealthy;
        }

        System.Diagnostics.Trace.WriteLine("[STARTUP] BEGIN StartHostAsync");
        await _operations.StartHostAsync(
            cancellationToken);
        System.Diagnostics.Trace.WriteLine("[STARTUP] END StartHostAsync");

        System.Diagnostics.Trace.WriteLine("[STARTUP] BEGIN ShowCachedSnapshotAsync");
        await _operations.ShowCachedSnapshotAsync(
            localState.Snapshot,
            cancellationToken);
        System.Diagnostics.Trace.WriteLine("[STARTUP] END ShowCachedSnapshotAsync");

        cancellationToken.ThrowIfCancellationRequested();

        _operations.SignalStartupReady?.Invoke();
        System.Diagnostics.Trace.WriteLine("[STARTUP] READY");

        _operations.BindInvocationReceived(
            HandleInvocationAsync);

        _operations.BindRescanRequested(
            RefreshAndApplyAsync);

        BackgroundRefreshTask =
            RefreshAndApplyAsync(
                cancellationToken);
        _ = ObserveInitialRefreshAsync(
            BackgroundRefreshTask,
            cancellationToken);

        return StartResult.Started;
    }

    public Task StopAsync(
        CancellationToken cancellationToken)
    {
        Task shutdown;
        lock (_lifecycleGate)
        {
            if (_shutdownTask is null)
            {
                _stopping = true;
                var refreshes = _refreshTasks.Where(task => !task.IsCompleted).ToList();
                if (BackgroundRefreshTask is { } startup && !refreshes.Contains(startup))
                    refreshes.Add(startup);
                _shutdownTask = Task.Run(() => StopCoreAsync(refreshes.ToArray()),
                    CancellationToken.None);
            }
            shutdown = _shutdownTask;
        }
        return shutdown.WaitAsync(cancellationToken);
    }

    private async Task StopCoreAsync(Task[] refreshes)
    {
        if (!_primaryAcquired)
            return;

        try
        {
            Exception? pipeFailure = null;
            try
            {
                await _operations.StopPipeAsync(CancellationToken.None);
            }
            catch (Exception error)
            {
                pipeFailure = error;
            }

            if (refreshes.Length != 0)
            {
                try
                {
                    await Task.WhenAll(refreshes);
                }
                catch (OperationCanceledException) when (
                    refreshes.All(task => task.IsCompleted && !task.IsFaulted))
                {
                    // Application cancellation leaves no durable refresh to publish.
                }
                catch (Exception refreshFailure) when (pipeFailure is not null)
                {
                    throw new AggregateException(pipeFailure, refreshFailure);
                }
            }

            if (pipeFailure is not null)
                ExceptionDispatchInfo.Capture(pipeFailure).Throw();
        }
        finally
        {
            try
            {
                if (_hostBuilt && _operations.StopDiscoveryAsync is { } stopDiscovery)
                    await stopDiscovery(CancellationToken.None);
            }
            finally
            {
                try
                {
                    if (_hostBuilt)
                        await _operations.StopHostAsync(CancellationToken.None);
                }
                finally
                {
                    await _operations.ReleaseSingleInstanceAsync(CancellationToken.None);
                }
            }
        }
    }

    private Task HandleInvocationAsync(
        AppInvocation invocation,
        CancellationToken cancellationToken) =>
        _operations.HandleInvocationAsync(
            invocation,
            cancellationToken);

    private Task RefreshAndApplyAsync(
        CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_lifecycleGate)
        {
            if (_stopping)
                return Task.FromCanceled(new CancellationToken(canceled: true));
            _refreshTasks.Add(completion.Task);
        }
        _ = CompleteRefreshAndApplyAsync(completion, cancellationToken);
        return completion.Task;
    }

    private async Task CompleteRefreshAndApplyAsync(
        TaskCompletionSource completion,
        CancellationToken cancellationToken)
    {
        StartupForensicTrace.Write("RefreshAndApply.Begin");
        try
        {
            var snapshot = await _operations.RefreshAsync(cancellationToken);
            await _operations.ApplySnapshotOnUiAsync(snapshot, cancellationToken);
            completion.TrySetResult();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            completion.TrySetCanceled(cancellationToken);
        }
        catch (Exception error)
        {
            completion.TrySetException(error);
        }
        finally
        {
            StartupForensicTrace.Write("RefreshAndApply.End");
            lock (_lifecycleGate)
                _refreshTasks.Remove(completion.Task);
        }
    }

    private static async Task ObserveInitialRefreshAsync(
        Task refreshTask,
        CancellationToken cancellationToken)
    {
        try
        {
            await refreshTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Application shutdown cancels the tracked refresh and is joined by StopAsync.
        }
        catch (Exception error)
        {
            System.Diagnostics.Trace.TraceWarning(
                "Initial refresh failed; cached startup snapshot remains available. {0}",
                error);
        }
    }

    public enum StartResult
    {
        Started,
        Forwarded,
        DatabaseUnhealthy
    }

    public sealed record Operations(
        Func<IReadOnlyList<string>, AppInvocation> ParseInvocation,
        Func<
            AppInvocation,
            CancellationToken,
            Task<SingleInstanceResult>>
            AcquireSingleInstanceAsync,
        Func<UserDataLayout> CreateUserDataLayout,
        Action<UserDataLayout> EnsureDirectoriesExist,
        Func<
            UserDataLayout,
            CancellationToken,
            Task>
            BuildHostAsync,
        Func<
            CancellationToken,
            Task<LocalStartupState>>
            InitializeLocalStateAsync,
        Func<
            CancellationToken,
            Task>
            StartHostAsync,
        Func<
            LibrarySnapshot,
            CancellationToken,
            Task>
            ShowCachedSnapshotAsync,
        Func<
            DatabaseHealthResult,
            CancellationToken,
            Task>
            ShowDatabaseRecoveryRequiredAsync,
        Action<
            Func<
                AppInvocation,
                CancellationToken,
                Task>>
            BindInvocationReceived,
        Action<
            Func<
                CancellationToken,
                Task>>
            BindRescanRequested,
        Func<
            AppInvocation,
            CancellationToken,
            Task>
            HandleInvocationAsync,
        Func<
            CancellationToken,
            Task<LibrarySnapshot>>
            RefreshAsync,
        Func<
            LibrarySnapshot,
            CancellationToken,
            Task>
            ApplySnapshotOnUiAsync,
        Func<
            CancellationToken,
            Task>
            StopPipeAsync,
        Func<
            CancellationToken,
            Task>
            StopHostAsync,
        Func<
            CancellationToken,
            Task>
            ReleaseSingleInstanceAsync,
        Func<CancellationToken, Task>? StopDiscoveryAsync = null,
        Func<CancellationToken, Task>? EnsureMainWindowShownAsync = null,
        Action? SignalStartupReady = null);
}
