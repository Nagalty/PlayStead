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
    private readonly Func<int, WindowsProcessImagePathResolution> _fallbackExecutablePathReader;
    private readonly Func<string, bool> _forensicTraceFilter;

    public WindowsProcessSnapshotSource()
        : this(
            Process.GetProcesses,
            ReadExecutablePath,
            ReadStartedAtUtc,
            new WindowsProcessImagePathReader().ResolveWithDiagnostics,
            _ => false)
    {
    }

    public WindowsProcessSnapshotSource(
        Func<string, bool> forensicTraceFilter)
        : this(
            Process.GetProcesses,
            ReadExecutablePath,
            ReadStartedAtUtc,
            new WindowsProcessImagePathReader().ResolveWithDiagnostics,
            forensicTraceFilter)
    {
    }

    public WindowsProcessSnapshotSource(
        Func<Process[]> processProvider,
        Func<Process, string?> executablePathReader,
        Func<Process, DateTimeOffset?> startTimeReader)
        : this(
            processProvider,
            executablePathReader,
            startTimeReader,
            _ => new WindowsProcessImagePathResolution(
                null,
                null,
                "NotAttempted",
                null),
            _ => true)
    {
    }

    internal WindowsProcessSnapshotSource(
        Func<Process[]> processProvider,
        Func<Process, string?> executablePathReader,
        Func<Process, DateTimeOffset?> startTimeReader,
        Func<int, string?> fallbackExecutablePathReader)
        : this(
            processProvider,
            executablePathReader,
            startTimeReader,
            processId =>
            {
                var path = fallbackExecutablePathReader(processId);
                return new WindowsProcessImagePathResolution(
                    path,
                    path,
                    string.IsNullOrWhiteSpace(path) ? "Unavailable" : null,
                    null);
            },
            _ => true)
    {
    }

    private WindowsProcessSnapshotSource(
        Func<Process[]> processProvider,
        Func<Process, string?> executablePathReader,
        Func<Process, DateTimeOffset?> startTimeReader,
        Func<int, WindowsProcessImagePathResolution> fallbackExecutablePathReader,
        Func<string, bool> forensicTraceFilter)
    {
        ArgumentNullException.ThrowIfNull(processProvider);
        ArgumentNullException.ThrowIfNull(executablePathReader);
        ArgumentNullException.ThrowIfNull(startTimeReader);
        ArgumentNullException.ThrowIfNull(fallbackExecutablePathReader);
        ArgumentNullException.ThrowIfNull(forensicTraceFilter);

        _processProvider = processProvider;
        _executablePathReader = executablePathReader;
        _startTimeReader = startTimeReader;
        _fallbackExecutablePathReader = fallbackExecutablePathReader;
        _forensicTraceFilter = forensicTraceFilter;
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

                var managedPath =
                    TryRead(
                        () => _executablePathReader(process));
                var executablePath = managedPath;
                var fallbackAttempted = false;
                var fallback = new WindowsProcessImagePathResolution(
                    null,
                    null,
                    "NotAttempted",
                    null);

                if (string.IsNullOrWhiteSpace(executablePath))
                {
                    fallbackAttempted = true;
                    fallback =
                        TryRead(
                            () => _fallbackExecutablePathReader(process.Id))
                        ?? new WindowsProcessImagePathResolution(
                            null,
                            null,
                            "FallbackException",
                            null);
                    executablePath = fallback.NormalizedPath;
                }

                if (string.IsNullOrWhiteSpace(executablePath))
                {
                    executablePath = null;
                }

                var executableName =
                    ResolveExecutableName(
                        process,
                        executablePath);

                if (string.IsNullOrWhiteSpace(executableName))
                {
                    continue;
                }

                if (_forensicTraceFilter(executableName))
                {
                    Trace.WriteLine(
                        $"[PROCESS-FORENSIC] Snapshot ProcessName={executableName} Pid={process.Id} " +
                        $"ManagedPath={Format(managedPath)} ManagedPathResult={Result(managedPath)} " +
                        $"FallbackAttempted={fallbackAttempted.ToString().ToLowerInvariant()} " +
                        $"FallbackResult={(fallbackAttempted ? Result(fallback.NormalizedPath) : "not-attempted")} " +
                        $"FallbackPath={Format(fallback.RawPath)} NormalizedPath={Format(executablePath)} " +
                        $"NativeFailure={Format(fallback.FailureCategory)} " +
                        $"NativeError={fallback.NativeErrorCode?.ToString() ?? "<null>"}");
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

    public async Task<ProcessCaptureResult> CaptureWithQualityAsync(
        CancellationToken cancellationToken)
        => new(await CaptureAsync(cancellationToken), true);

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

    private static string Format(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "<null>" : value;

    private static string Result(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "fail" : "success";

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
