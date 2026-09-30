namespace PlayStead.UI.Launching;

public interface IProcessIdentityLauncher
{
    LaunchedProcessIdentity? StartWithIdentity(
        string executablePath,
        string workingDirectory,
        string? arguments);
}
