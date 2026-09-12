using System.ComponentModel;
using System.Diagnostics;
using PlayStead.Core.Sessions;

namespace PlayStead.Platform.Processes;

public sealed class WindowsProcessSnapshotSource :
    IProcessSnapshotSource
{
    private readonly Func<Process[]> _processProvider;
    private readonly Func<Process, string?> _executablePathReader;
    private readonly Func<Process, DateTimeOffset?> _startTimeReader;

    public WindowsProcessSnapshotSource()
        : this(
            Process.GetProcesses,
            ReadExecutablePath,
            ReadStartedAtUtc)
    {
    }

    public WindowsProcessSnapshotSource(
        Func<Process[]> processProvider,
        Func<Process, string?> executablePathReader,
        Func<Process, DateTimeOffset?> startTimeReader)
    {
        ArgumentNullException.ThrowIfNull(processProvider);
        ArgumentNullException.ThrowIfNull(executablePathReader);
        ArgumentNullException.ThrowIfNull(startTimeReader);

        _processProvider = processProvider;
        _executablePathReader = executablePathReader;
        _startTimeReader = startTimeReader;
    }

    public Task<IReadOnlyList<ProcessSnapshot>> CaptureAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var processes = _processProvider();

        var snapshots =
            new List<ProcessSnapshot>(processes.Length);

        foreach (var process in processes)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                var executablePath =
                    TryRead(
                        () => _executablePathReader(process));

                var executableName =
                    ResolveExecutableName(
                        process,
                        executablePath);

                if (string.IsNullOrWhiteSpace(executableName))
                {
                    continue;
                }

                var startedAtUtc =
                    TryRead(
                        () => _startTimeReader(process));

                snapshots.Add(
                    new ProcessSnapshot(
                        process.Id,
                        executableName,
                        executablePath,
                        startedAtUtc));
            }
            finally
            {
                process.Dispose();
            }
        }

        return Task.FromResult<IReadOnlyList<ProcessSnapshot>>(
            snapshots);
    }

    private static string? ResolveExecutableName(
        Process process,
        string? executablePath)
    {
        if (!string.IsNullOrWhiteSpace(executablePath))
        {
            var fromPath =
                Path.GetFileName(executablePath);

            if (!string.IsNullOrWhiteSpace(fromPath))
            {
                return fromPath;
            }
        }

        var processName =
            TryRead(
                () => process.ProcessName);

        if (string.IsNullOrWhiteSpace(processName))
        {
            return null;
        }

        return processName.EndsWith(
                ".exe",
                StringComparison.OrdinalIgnoreCase)
            ? processName
            : $"{processName}.exe";
    }

    private static string? ReadExecutablePath(
        Process process)
        => process.MainModule?.FileName;

    private static DateTimeOffset? ReadStartedAtUtc(
        Process process)
        => new DateTimeOffset(
                process.StartTime)
            .ToUniversalTime();

    private static T? TryRead<T>(
        Func<T?> reader)
    {
        try
        {
            return reader();
        }
        catch (Win32Exception)
        {
            return default;
        }
        catch (UnauthorizedAccessException)
        {
            return default;
        }
        catch (InvalidOperationException)
        {
            return default;
        }
        catch (NotSupportedException)
        {
            return default;
        }
    }
}
