using PlayStead.Core.Updates;

namespace PlayStead.Providers.Updates;

public sealed class MicrosoftStoreAppUpdateService : IAppUpdateService
{
    private readonly string _currentVersion;
    private readonly MicrosoftStoreUpdateOptions _options;
    private readonly IMicrosoftStoreUpdateProbe? _updateProbe;

    public MicrosoftStoreAppUpdateService(
        string currentVersion,
        MicrosoftStoreUpdateOptions options,
        IMicrosoftStoreUpdateProbe? updateProbe = null)
    {
        _currentVersion = currentVersion;
        _options = options;
        _updateProbe = updateProbe;
        Current = AppUpdateState.Unknown(Channel, currentVersion);
    }

    public DistributionChannel Channel => DistributionChannel.MicrosoftStore;
    public AppUpdateState Current { get; private set; }

    public async Task<AppUpdateState> CheckAsync(CancellationToken cancellationToken = default)
    {
        if (_updateProbe is null)
                return Current = AppUpdateState.Unknown(Channel, _currentVersion);

        try
        {
            var available = await _updateProbe.HasUpdateAsync(cancellationToken).ConfigureAwait(false);
            if (!available)
                return Current = new AppUpdateState(
                    AppUpdateStatus.UpToDate,
                    Channel,
                    _currentVersion);

            Uri? actionUri = _options.ProductId is null
                ? null
                : new Uri($"ms-windows-store://pdp/?ProductId={Uri.EscapeDataString(_options.ProductId)}");
            return Current = new AppUpdateState(
                AppUpdateStatus.UpdateAvailable,
                Channel,
                _currentVersion,
                ActionUri: actionUri,
                Action: AppUpdateActionKind.OpenMicrosoftStore);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (PlatformNotSupportedException)
        {
            return Current = AppUpdateState.Unknown(Channel, _currentVersion);
        }
        catch (Exception exception)
        {
            return Current = new AppUpdateState(AppUpdateStatus.Error, Channel, _currentVersion, Error: exception.Message);
        }
    }
}
