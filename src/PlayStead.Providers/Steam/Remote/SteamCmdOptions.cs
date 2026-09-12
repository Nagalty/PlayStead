namespace PlayStead.Providers.Steam.Remote;

public sealed record SteamCmdOptions(
    string? ExecutablePath,
    TimeSpan QueryTimeout)
{
    public static SteamCmdOptions Default { get; } =
        new(
            ExecutablePath: null,
            QueryTimeout: TimeSpan.FromSeconds(30));
}
