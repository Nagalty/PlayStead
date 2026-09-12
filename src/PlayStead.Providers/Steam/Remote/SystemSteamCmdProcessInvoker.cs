using System.Diagnostics;

namespace PlayStead.Providers.Steam.Remote;

public sealed class SystemSteamCmdProcessInvoker :
    ISteamCmdProcessInvoker
{
    public async Task<SteamCmdProcessResult> InvokeAsync(
        SteamCmdProcessRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            request.ExecutablePath);

        var startInfo = new ProcessStartInfo
        {
            FileName = request.ExecutablePath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        foreach (var argument in request.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process
        {
            StartInfo = startInfo
        };

        var stopwatch = Stopwatch.StartNew();

        if (!process.Start())
        {
            throw new InvalidOperationException(
                $"Unable to start process '{request.ExecutablePath}'.");
        }

        var stdoutTask =
            process.StandardOutput.ReadToEndAsync();

        var stderrTask =
            process.StandardError.ReadToEndAsync();

        try
        {
            await process.WaitForExitAsync(
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            TryKillProcessTree(process);

            try
            {
                await process.WaitForExitAsync(
                    CancellationToken.None);
            }
            catch (InvalidOperationException)
            {
            }

            throw;
        }
        finally
        {
            stopwatch.Stop();
        }

        var standardOutput =
            await stdoutTask;

        var standardError =
            await stderrTask;

        return new SteamCmdProcessResult(
            process.ExitCode,
            standardOutput,
            standardError,
            stopwatch.Elapsed);
    }

    private static void TryKillProcessTree(
        Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(
                    entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
    }
}
