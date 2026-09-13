using System.Reflection;
using PlayStead.Core.Sessions;
using PlayStead.UI.Sessions;

namespace PlayStead.UI.Tests.Sessions;

public sealed class Task09SessionMonitorPublicationTests
{
    private static readonly DateTimeOffset T0 =
        new(2026, 9, 13, 1, 20, 0, TimeSpan.Zero);

    [Fact]
    public async Task Successful_refresh_is_retained_and_published()
    {
        var expected =
            new SessionRuntimeSnapshot(
                T0,
                []);

        var runtime =
            new FixedSessionRuntime(
                expected);

        using var cancellation =
            new CancellationTokenSource();

        var sut =
            new SessionMonitor(
                runtime,
                SessionMonitorOptions.Default,
                (_, _) =>
                {
                    cancellation.Cancel();
                    return Task.CompletedTask;
                });

        var monitorType =
            typeof(SessionMonitor);

        var latestProperty =
            monitorType.GetProperty(
                "LatestSnapshot",
                BindingFlags.Instance | BindingFlags.Public);

        var snapshotEvent =
            monitorType.GetEvent(
                "SnapshotUpdated",
                BindingFlags.Instance | BindingFlags.Public);

        Assert.NotNull(latestProperty);
        Assert.NotNull(snapshotEvent);

        SessionRuntimeSnapshot? observed = null;

        Action<SessionRuntimeSnapshot> handler =
            snapshot => observed = snapshot;

        snapshotEvent.AddEventHandler(
            sut,
            handler);

        await sut.RunAsync(
            cancellation.Token);

        Assert.Same(
            expected,
            latestProperty.GetValue(sut));

        Assert.Same(
            expected,
            observed);
    }

    private sealed class FixedSessionRuntime :
        ISessionRuntime
    {
        private readonly SessionRuntimeSnapshot _snapshot;

        public FixedSessionRuntime(
            SessionRuntimeSnapshot snapshot)
        {
            _snapshot = snapshot;
        }

        public Task<SessionRuntimeSnapshot> RefreshAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(
                _snapshot);
        }
    }
}
