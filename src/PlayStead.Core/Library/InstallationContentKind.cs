namespace PlayStead.Core.Library;

public enum InstallationContentKind
{
    Unknown = 0,
    Game = 1,
    Tool = 2,
    Runtime = 3,
    Driver = 4,
    Sdk = 5,
    Application = 6
}

public static class InstallationContentKindPolicy
{
    public static bool IsGameEligible(this InstallationContentKind kind) =>
        kind is InstallationContentKind.Unknown or InstallationContentKind.Game;
}
