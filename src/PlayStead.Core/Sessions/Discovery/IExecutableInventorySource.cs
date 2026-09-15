namespace PlayStead.Core.Sessions.Discovery;

public interface IExecutableInventorySource
{
    Task<ExecutableInventory> InventoryAsync(
        InstallationScope scope, CancellationToken cancellationToken);
}
