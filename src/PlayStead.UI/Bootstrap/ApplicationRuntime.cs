using PlayStead.Data.Database;
using PlayStead.Platform.SingleInstance;
using PlayStead.Core.Library;
using PlayStead.UI.Library;
using PlayStead.UI.SingleInstance;
using PlayStead.Core.ProviderActivity;
using PlayStead.Core.Persistence;
using PlayStead.Core.ProviderGameMetadata;
using PlayStead.Core.ProviderInstallUpdate;
using PlayStead.Providers.Steam;

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

    public ApplicationRuntime(
        LocalStartupPipeline startupPipeline,
        LibraryViewModel libraryViewModel,
        IAppInvocationHandler invocationHandler,
        ProviderActivityReconciliationService providerActivity,
        ILibraryStore libraryStore)
    {
        _startupPipeline = startupPipeline;
        _libraryViewModel = libraryViewModel;
        _invocationHandler = invocationHandler;
        _providerActivity = providerActivity;
        _libraryStore = libraryStore;
    }

    public ApplicationRuntime(
        LocalStartupPipeline startupPipeline,
        LibraryViewModel libraryViewModel,
        IAppInvocationHandler invocationHandler,
        ProviderActivityReconciliationService providerActivity,
        ILibraryStore libraryStore,
        ProviderGameMetadataReconciliationService providerGameMetadata)
        : this(startupPipeline, libraryViewModel, invocationHandler, providerActivity, libraryStore) =>
        _providerGameMetadata = providerGameMetadata;

    public ApplicationRuntime(
        LocalStartupPipeline startupPipeline,
        LibraryViewModel libraryViewModel,
        IAppInvocationHandler invocationHandler,
        ProviderActivityReconciliationService providerActivity,
        ILibraryStore libraryStore,
        ProviderGameMetadataReconciliationService providerGameMetadata,
        ProviderGameMetadataOnlineReconciliationService onlineProviderGameMetadata,
        ProviderInstallUpdateStateReconciliationService providerInstallUpdates,
        SteamInstallUpdateLiveRefreshService steamLiveRefresh)
        : this(startupPipeline, libraryViewModel, invocationHandler, providerActivity, libraryStore, providerGameMetadata, providerInstallUpdates, steamLiveRefresh)
    {
        _onlineProviderGameMetadata = onlineProviderGameMetadata;
    }

    public ApplicationRuntime(
        LocalStartupPipeline startupPipeline,
        LibraryViewModel libraryViewModel,
        IAppInvocationHandler invocationHandler,
        ProviderActivityReconciliationService providerActivity,
        ILibraryStore libraryStore,
        ProviderGameMetadataReconciliationService providerGameMetadata,
        ProviderInstallUpdateStateReconciliationService providerInstallUpdates,
        SteamInstallUpdateLiveRefreshService steamLiveRefresh)
        : this(startupPipeline, libraryViewModel, invocationHandler, providerActivity, libraryStore, providerGameMetadata)
    {
        _providerInstallUpdates = providerInstallUpdates;
        _steamLiveRefresh = steamLiveRefresh;
        _steamLiveRefresh.SetRefresh(RefreshAsync);
    }

    public async Task<DatabaseHealthResult> InitializeAsync(
        CancellationToken cancellationToken)
    {
        var state = await InitializeLocalStateAsync(cancellationToken);
        return state.Health;
    }

    public async Task<LocalStartupState> InitializeLocalStateAsync(
        CancellationToken cancellationToken)
    {
        var state = await _startupPipeline.InitializeAsync(
            cancellationToken);

        if (!state.Health.IsHealthy)
        {
            return state;
        }

        await StartupForensicTrace.MeasureAsync("LibraryViewModel", () => _libraryViewModel.RefreshAsync(cancellationToken));
        if (_providerActivity is not null && _libraryStore is not null)
        {
            await StartupForensicTrace.MeasureAsync("ProviderActivityMetadata", async () =>
                await _providerActivity.RefreshAsync(await _libraryStore.LoadSnapshotAsync(cancellationToken), cancellationToken));
        }
        if (_providerGameMetadata is not null && _libraryStore is not null)
            await StartupForensicTrace.MeasureAsync("ProviderGameMetadata", async () =>
                await _providerGameMetadata.RefreshAsync(await _libraryStore.LoadSnapshotAsync(cancellationToken), cancellationToken));
        if (_providerInstallUpdates is not null && _libraryStore is not null)
            await StartupForensicTrace.MeasureAsync("ProviderInstallUpdate", async () =>
                await _providerInstallUpdates.RefreshAsync((await _libraryStore.LoadSnapshotAsync(cancellationToken)).Installations, cancellationToken));

        _steamLiveRefresh?.Start();

        return state;
    }

    public async Task<LibrarySnapshot> RefreshAsync(
        CancellationToken cancellationToken)
    {
        LibrarySnapshot? snapshot = null;
        await StartupForensicTrace.MeasureAsync("Refresh.LocalPipeline", async () =>
            snapshot = await _startupPipeline.RefreshAsync(cancellationToken));

        await StartupForensicTrace.MeasureAsync("Refresh.LibraryViewModel", () => _libraryViewModel.RefreshAsync(cancellationToken));
        if (_providerActivity is not null && _libraryStore is not null)
        {
            await StartupForensicTrace.MeasureAsync("Refresh.ProviderActivityMetadata", async () =>
                await _providerActivity.RefreshAsync(await _libraryStore.LoadSnapshotAsync(cancellationToken), cancellationToken));
        }
        if (_providerGameMetadata is not null && _libraryStore is not null)
            await StartupForensicTrace.MeasureAsync("Refresh.ProviderGameMetadata", async () =>
                await _providerGameMetadata.RefreshAsync(await _libraryStore.LoadSnapshotAsync(cancellationToken), cancellationToken));
        if (_providerInstallUpdates is not null && _libraryStore is not null)
            await StartupForensicTrace.MeasureAsync("Refresh.ProviderInstallUpdate", async () =>
                await _providerInstallUpdates.RefreshAsync((await _libraryStore.LoadSnapshotAsync(cancellationToken)).Installations, cancellationToken));

        StartPostReadyEnrichment(_postReadyCancellationToken);

        return snapshot!;
    }

    public void StartPostReadyEnrichment(CancellationToken cancellationToken)
    {
        if (_onlineProviderGameMetadata is null || _libraryStore is null)
            return;

        lock (_onlineRefreshGate)
        {
            _postReadyCancellationToken = cancellationToken;
            if (_onlineRefreshTask is { IsCompleted: false })
                return;

            _onlineRefreshTask = RunPostReadyEnrichmentAsync(cancellationToken);
        }
    }

    private async Task RunPostReadyEnrichmentAsync(CancellationToken cancellationToken)
    {
        try
        {
            var snapshot = await _libraryStore!.LoadSnapshotAsync(cancellationToken);
            await StartupForensicTrace.MeasureAsync(
                "PostReady.ProviderGameMetadata",
                () => _onlineProviderGameMetadata!.RefreshAsync(snapshot, cancellationToken));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            System.Diagnostics.Trace.WriteLine($"[STARTUP-STORE] PostReadyFailure Type={exception.GetType().Name}");
        }
    }

    public Task HandleInvocationAsync(
        AppInvocation invocation,
        CancellationToken cancellationToken) =>
        _invocationHandler.HandleAsync(
            invocation,
            cancellationToken);
}
