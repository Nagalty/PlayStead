namespace PlayStead.Providers.Steam.Remote;

public sealed record SteamCmdProcessRequest(
    string ExecutablePath,
    IReadOnlyList<string> Arguments);
