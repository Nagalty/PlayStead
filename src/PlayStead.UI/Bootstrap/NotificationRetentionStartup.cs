using PlayStead.Core.Notifications;

namespace PlayStead.UI.Bootstrap;

public sealed class NotificationRetentionStartup
{
    private readonly INotificationCenterService _service;

    public NotificationRetentionStartup(INotificationCenterService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        _service = service;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            await _service.PurgeExpiredResolvedAsync(cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
        }
    }
}
