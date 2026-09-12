using PlayStead.Core.Sessions;
using PlayStead.UI.Sessions;

namespace PlayStead.UI.Tests.Sessions;

public sealed class SessionMonitorTests
{
    private static readonly DateTimeOffset T0 =
        new(2026, 9, 13, 0, 45, 0, TimeSpan.Zero);

    [Fact]
    public void Default_poll_interval_is_two_seconds()
    {
        Assert.Equal(
            TimeSpan.FromSeconds(2),
            SessionMonitorOptions.Default.PollInterval);
    }

    [Fact]
    public async Task RunAsync_refreshes_runtime_then_waits_the_configured_interval()
    {
        var runtime = new FakeSessionRuntime();
        var observedDelay = TimeSpan.Zero;

        using var cancellation =
            new CancellationTokenSource();

        var sut = new SessionMonitor(
            runtime,
            SessionMonitorOptions.Default,
            (delay, _) =>
            {
                observedDelay = delay;
                cancellation.Cancel();
                return Task.CompletedTask;
            });

        await sut.RunAsync(
            cancellation.Token);

        Assert.Equal(
            1,
            runtime.RefreshCount);

        Assert.Equal(
            TimeSpan.FromSeconds(2),
            observedDelay);
    }

    [Fact]
    public async Task RunAsync_honors_cancellation_before_first_refresh()
    {
        var runtime = new FakeSessionRuntime();

        using var cancellation =
            new CancellationTokenSource();

        await cancellation.CancelAsync();

        var sut = new SessionMonitor(
            runtime,
            SessionMonitorOptions.Default,
            (_, _) => Task.CompletedTask);

        await sut.RunAsync(
            cancellation.Token);

        Assert.Equal(
            0,
            runtime.RefreshCount);
    }

    private sealed class FakeSessionRuntime :
        ISessionRuntime
    {
        public int RefreshCount { get; private set; }

        public Task<SessionRuntimeSnapshot> RefreshAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            RefreshCount++;

            return Task.FromResult(
                new SessionRuntimeSnapshot(
                    T0,
                    []));
        }
    }
}
