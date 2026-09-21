using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using PlayStead.Core.Sessions;
using PlayStead.Platform.Processes;

namespace PlayStead.Platform.Tests.Processes;

public sealed class WindowsProcessSnapshotSourceTests
{
    private static readonly DateTimeOffset T0 =
        new(2026, 9, 13, 0, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task Capture_traces_managed_failure_and_successful_native_fallback_without_changing_snapshot()
    {
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var listener = new TextWriterTraceListener(output);
        Trace.Listeners.Add(listener);
        try
        {
            var expectedPath = @"C:\Games\ObservedGame.exe";
            var sut = new WindowsProcessSnapshotSource(
                () => [Process.GetProcessById(Environment.ProcessId)],
                _ => throw new UnauthorizedAccessException(),
                _ => T0,
                _ => expectedPath);

            var snapshot = Assert.Single(await sut.CaptureAsync(CancellationToken.None));

            Assert.Equal(expectedPath, snapshot.ExecutablePath);
            Assert.Equal(T0, snapshot.StartedAtUtc);
            listener.Flush();
            var trace = output.ToString();
            Assert.Contains("[PROCESS-FORENSIC]", trace, StringComparison.Ordinal);
            Assert.Contains("ManagedPathResult=fail", trace, StringComparison.Ordinal);
            Assert.Contains("FallbackAttempted=true", trace, StringComparison.Ordinal);
            Assert.Contains("FallbackResult=success", trace, StringComparison.Ordinal);
            Assert.Contains($"FallbackPath={expectedPath}", trace, StringComparison.Ordinal);
            Assert.Contains($"NormalizedPath={expectedPath}", trace, StringComparison.Ordinal);
        }
        finally
        {
            Trace.Listeners.Remove(listener);
        }
    }

    [Fact]
    public async Task Capture_includes_current_process_with_real_windows_metadata()
    {
        IProcessSnapshotSource sut =
            new WindowsProcessSnapshotSource();

        var snapshots = await sut.CaptureAsync(
            CancellationToken.None);

        var current = Assert.Single(
            snapshots,
            x => x.ProcessId == Environment.ProcessId);

        Assert.False(
            string.IsNullOrWhiteSpace(
                current.ExecutableName));

        Assert.EndsWith(
            ".exe",
            current.ExecutableName,
            StringComparison.OrdinalIgnoreCase);

        Assert.False(
            string.IsNullOrWhiteSpace(
                current.ExecutablePath));

        Assert.Equal(
            current.ExecutableName,
            Path.GetFileName(current.ExecutablePath),
            ignoreCase: true);

        Assert.NotNull(
            current.StartedAtUtc);
    }

    [Fact]
    public async Task Capture_keeps_process_when_executable_path_metadata_is_inaccessible()
    {
        var sut = new WindowsProcessSnapshotSource(
            processProvider:
                () => [Process.GetProcessById(Environment.ProcessId)],
            executablePathReader:
                _ => throw new UnauthorizedAccessException(
                    "Path metadata denied."),
            startTimeReader:
                _ => T0);

        var snapshots = await sut.CaptureAsync(
            CancellationToken.None);

        var snapshot = Assert.Single(snapshots);

        Assert.Equal(
            Environment.ProcessId,
            snapshot.ProcessId);

        Assert.False(
            string.IsNullOrWhiteSpace(
                snapshot.ExecutableName));

        Assert.Null(
            snapshot.ExecutablePath);

        Assert.Equal(
            T0,
            snapshot.StartedAtUtc);
    }

    [Fact]
    public async Task Capture_does_not_use_native_fallback_when_managed_path_is_available()
    {
        var fallbackCalls = 0;
        var expectedPath = @"C:\Games\ManagedGame.exe";
        var sut = new WindowsProcessSnapshotSource(
            () => [Process.GetProcessById(Environment.ProcessId)],
            _ => expectedPath,
            _ => T0,
            _ =>
            {
                fallbackCalls++;
                return @"C:\Games\FallbackGame.exe";
            });

        var snapshot = Assert.Single(
            await sut.CaptureAsync(CancellationToken.None));

        Assert.Equal(expectedPath, snapshot.ExecutablePath);
        Assert.Equal(0, fallbackCalls);
    }

    [Fact]
    public async Task Capture_uses_native_fallback_when_managed_path_is_inaccessible()
    {
        var expectedPath = @"C:\Games\NativeGame.exe";
        var sut = new WindowsProcessSnapshotSource(
            () => [Process.GetProcessById(Environment.ProcessId)],
            _ => throw new UnauthorizedAccessException(),
            _ => T0,
            processId =>
            {
                Assert.Equal(Environment.ProcessId, processId);
                return expectedPath;
            });

        var snapshot = Assert.Single(
            await sut.CaptureAsync(CancellationToken.None));

        Assert.Equal(expectedPath, snapshot.ExecutablePath);
        Assert.Equal("NativeGame.exe", snapshot.ExecutableName);
    }

    [Fact]
    public async Task Capture_keeps_path_unresolved_when_both_readers_fail()
    {
        var sut = new WindowsProcessSnapshotSource(
            () => [Process.GetProcessById(Environment.ProcessId)],
            _ => throw new InvalidOperationException(),
            _ => T0,
            _ => string.Empty);

        var snapshot = Assert.Single(
            await sut.CaptureAsync(CancellationToken.None));

        Assert.Null(snapshot.ExecutablePath);
    }

    [Fact]
    public async Task Capture_keeps_process_when_start_time_metadata_is_inaccessible()
    {
        var expectedPath =
            @"C:\PlayStead.Tests\FakeGame.exe";

        var sut = new WindowsProcessSnapshotSource(
            processProvider:
                () => [Process.GetProcessById(Environment.ProcessId)],
            executablePathReader:
                _ => expectedPath,
            startTimeReader:
                _ => throw new Win32Exception(
                    "Start time unavailable."));

        var snapshots = await sut.CaptureAsync(
            CancellationToken.None);

        var snapshot = Assert.Single(snapshots);

        Assert.Equal(
            expectedPath,
            snapshot.ExecutablePath);

        Assert.EndsWith(
            ".exe",
            snapshot.ExecutableName,
            StringComparison.OrdinalIgnoreCase);

        Assert.Null(
            snapshot.StartedAtUtc);
    }

    [Fact]
    public async Task Capture_honors_cancellation_before_enumeration()
    {
        var providerWasCalled = false;

        var sut = new WindowsProcessSnapshotSource(
            processProvider:
                () =>
                {
                    providerWasCalled = true;
                    return [];
                },
            executablePathReader:
                _ => null,
            startTimeReader:
                _ => null);

        using var cancellation =
            new CancellationTokenSource();

        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => sut.CaptureAsync(cancellation.Token));

        Assert.False(providerWasCalled);
    }
}
