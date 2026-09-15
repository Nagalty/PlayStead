using System.ComponentModel;
using System.Diagnostics;
using PlayStead.Core.Sessions;
using PlayStead.Platform.Processes;

namespace PlayStead.Platform.Tests.Processes;

public sealed class WindowsProcessCaptureQualityTests
{
    [Fact]
    public async Task Quality_capture_enumerates_once_and_disposes_each_process()
    {
        var enumerationCount = 0;
        var captured = Process.GetProcessById(Environment.ProcessId);
        var sut = new WindowsProcessSnapshotSource(
            () => { enumerationCount++; return [captured]; },
            _ => @"C:\Games\Game.exe", _ => DateTimeOffset.UtcNow);

        IProcessSnapshotSource source = sut;
        var result = await source.CaptureWithQualityAsync(CancellationToken.None);

        Assert.True(result.IsComplete);
        Assert.Equal(1, enumerationCount);
        Assert.Equal(Environment.ProcessId, Assert.Single(result.Processes).ProcessId);
        Assert.Throws<InvalidOperationException>(() => _ = captured.Handle);
    }

    [Fact]
    public async Task Inaccessible_metadata_keeps_capture_complete_and_row_present()
    {
        var sut = new WindowsProcessSnapshotSource(
            () => [Process.GetProcessById(Environment.ProcessId)],
            _ => throw new UnauthorizedAccessException(),
            _ => throw new Win32Exception());

        var result = await sut.CaptureWithQualityAsync(CancellationToken.None);

        Assert.True(result.IsComplete);
        var row = Assert.Single(result.Processes);
        Assert.Equal(Environment.ProcessId, row.ProcessId);
        Assert.Null(row.ExecutablePath);
        Assert.Null(row.StartedAtUtc);
    }

    [Fact]
    public async Task Enumeration_failure_is_not_reported_as_empty_complete_capture()
    {
        var sut = new WindowsProcessSnapshotSource(
            () => throw new InvalidOperationException("enumeration failed"),
            _ => null, _ => null);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.CaptureWithQualityAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Cancellation_during_enumeration_disposes_all_captured_processes()
    {
        using var cancellation = new CancellationTokenSource();
        var first = Process.GetProcessById(Environment.ProcessId);
        var second = Process.GetProcessById(Environment.ProcessId);
        var sut = new WindowsProcessSnapshotSource(
            () => [first, second],
            _ => { cancellation.Cancel(); return @"C:\Games\Game.exe"; },
            _ => DateTimeOffset.UtcNow);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => sut.CaptureWithQualityAsync(cancellation.Token));
        Assert.Throws<InvalidOperationException>(() => _ = first.Handle);
        Assert.Throws<InvalidOperationException>(() => _ = second.Handle);
    }
}
