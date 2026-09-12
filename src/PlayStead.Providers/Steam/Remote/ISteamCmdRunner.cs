namespace PlayStead.Providers.Steam.Remote;

public interface ISteamCmdRunner
{
    Task<SteamCmdRunResult> RunAsync(
        SteamCmdRequest request,
        CancellationToken cancellationToken);
}
