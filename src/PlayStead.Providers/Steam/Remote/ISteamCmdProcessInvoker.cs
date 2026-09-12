namespace PlayStead.Providers.Steam.Remote;

public interface ISteamCmdProcessInvoker
{
    Task<SteamCmdProcessResult> InvokeAsync(
        SteamCmdProcessRequest request,
        CancellationToken cancellationToken);
}
