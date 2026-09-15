using PlayStead.Core.Library;
using PlayStead.Data.Database;
using PlayStead.Platform.Paths;
using PlayStead.Platform.SingleInstance;

namespace PlayStead.UI.Bootstrap;

public sealed class ApplicationStartupCoordinator
{
    private readonly Operations _operations;

    private int _stopStarted;
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

        var localState =
            await _operations.InitializeLocalStateAsync(
                cancellationToken);

        if (!localState.Health.IsHealthy)
        {
            await _operations.ShowDatabaseRecoveryRequiredAsync(
                localState.Health,
                cancellationToken);

            return StartResult.DatabaseUnhealthy;
        }

        await _operations.StartHostAsync(
            cancellationToken);

        await _operations.ShowCachedSnapshotAsync(
            localState.Snapshot,
            cancellationToken);

        _operations.BindInvocationReceived(
            HandleInvocationAsync);

        _operations.BindRescanRequested(
            RefreshAndApplyAsync);

        BackgroundRefreshTask =
            RefreshAndApplyAsync(
                cancellationToken);

        return StartResult.Started;
    }

    public async Task StopAsync(
        CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(
                ref _stopStarted,
                1) != 0)
        {
            return;
        }

        if (!_primaryAcquired)
        {
            return;
        }

        try
        {
            await _operations.StopPipeAsync(cancellationToken);

            if (BackgroundRefreshTask is { } refresh)
            {
                try
                {
                    await refresh.WaitAsync(cancellationToken);
                }
                catch (OperationCanceledException) when (refresh.IsCanceled)
                {
                    // Application cancellation leaves no durable refresh to publish.
                }
            }
        }
        finally
        {
            try
            {
                if (_hostBuilt && _operations.StopDiscoveryAsync is { } stopDiscovery)
                    await stopDiscovery(cancellationToken);
            }
            finally
            {
                try
                {
                    if (_hostBuilt)
                        await _operations.StopHostAsync(cancellationToken);
                }
                finally
                {
                    await _operations.ReleaseSingleInstanceAsync(cancellationToken);
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

    private async Task RefreshAndApplyAsync(
        CancellationToken cancellationToken)
    {
        var snapshot =
            await _operations.RefreshAsync(
                cancellationToken);

        await _operations.ApplySnapshotOnUiAsync(
            snapshot,
            cancellationToken);
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
        Func<CancellationToken, Task>? StopDiscoveryAsync = null);
}
