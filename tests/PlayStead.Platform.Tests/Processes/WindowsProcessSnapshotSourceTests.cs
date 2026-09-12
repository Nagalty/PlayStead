using System.ComponentModel;
using System.Diagnostics;
using PlayStead.Core.Sessions;
using PlayStead.Platform.Processes;

namespace PlayStead.Platform.Tests.Processes;

public sealed class WindowsProcessSnapshotSourceTests
{
    private static readonly DateTimeOffset T0 =
        new(2026, 9, 13, 0, 30, 0, TimeSpan.Zero);

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
