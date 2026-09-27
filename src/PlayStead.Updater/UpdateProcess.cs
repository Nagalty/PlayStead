using System.Diagnostics;

namespace PlayStead.Updater;

public interface IUpdateProcessController
{
    Task WaitForExitAsync(int processId, TimeSpan timeout, CancellationToken cancellationToken);
    bool Start(string executablePath, string arguments);
}

public sealed class SystemUpdateProcessController : IUpdateProcessController
{
    public async Task WaitForExitAsync(int processId, TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (processId <= 0) return;
        try
        {
            using var process = Process.GetProcessById(processId);
            if (process.HasExited) return;
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(timeout);
            await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (ArgumentException) { }
        catch (InvalidOperationException) { }
    }

    public bool Start(string executablePath, string arguments)
    {
        return Process.Start(new ProcessStartInfo(executablePath, arguments) { UseShellExecute = true }) is not null;
    }
}
