namespace PlayStead.Core.Updates;

public interface IAppUpdateService
{
    DistributionChannel Channel { get; }

    AppUpdateState Current { get; }

    Task<AppUpdateState> CheckAsync(CancellationToken cancellationToken = default);
}
