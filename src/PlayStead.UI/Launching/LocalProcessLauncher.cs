using System.Diagnostics;

namespace PlayStead.UI.Launching;

public sealed class LocalProcessLauncher : ILocalProcessLauncher, IProcessIdentityLauncher
{
    public bool Start(string executablePath, string workingDirectory, string? arguments)
        => StartWithIdentity(executablePath, workingDirectory, arguments) is not null;

    public LaunchedProcessIdentity? StartWithIdentity(
        string executablePath,
        string workingDirectory,
        string? arguments)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory = workingDirectory,
            Arguments = arguments ?? string.Empty,
            UseShellExecute = false
        });
        if (process is null)
            return null;

        try
        {
            return new LaunchedProcessIdentity(
                process.Id,
                process.StartTime.ToUniversalTime());
        }
        catch
        {
            process.Dispose();
            return null;
        }
    }
}
