namespace PlayStead.UI.Steam;

public interface ISteamReferenceRuntime
{
    SteamReferenceSnapshot Current { get; }

    Task<SteamReferenceSnapshot> LoadCachedAsync(
        CancellationToken cancellationToken);

    Task<SteamReferenceSnapshot> RefreshStaleAsync(
        CancellationToken cancellationToken);

    Task<SteamReferenceSnapshot> RefreshAllAsync(
        CancellationToken cancellationToken);
}
