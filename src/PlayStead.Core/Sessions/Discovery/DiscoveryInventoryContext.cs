namespace PlayStead.Core.Sessions.Discovery;

public sealed record DiscoveryInventoryContext(
    ExecutableInventory Inventory, bool HasAmbiguousInstallation);
