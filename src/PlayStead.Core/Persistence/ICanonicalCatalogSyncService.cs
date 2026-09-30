using PlayStead.Core.Catalog;

namespace PlayStead.Core.Persistence;

public interface ICanonicalCatalogSyncService
{
    Task<bool> SyncAsync(CancellationToken cancellationToken);
}
