namespace PlayStead.Providers.Steam;

public interface ISteamStoreAppDetailsClient
{
    Task<SteamStoreAppDetails?> GetAsync(string appId, CancellationToken cancellationToken);
}
