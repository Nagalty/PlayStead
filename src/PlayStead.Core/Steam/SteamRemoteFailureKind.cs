namespace PlayStead.Core.Steam;

public enum SteamRemoteFailureKind
{
    SteamCmdMissing,
    Timeout,
    NonZeroExitCode,
    MalformedOutput,
    BranchUnavailable
}
