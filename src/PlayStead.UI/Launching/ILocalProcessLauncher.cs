namespace PlayStead.UI.Launching;

public interface ILocalProcessLauncher
{
    bool Start(string executablePath, string workingDirectory, string? arguments);
}
