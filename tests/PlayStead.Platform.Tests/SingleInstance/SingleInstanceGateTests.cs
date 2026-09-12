using PlayStead.Platform.SingleInstance;

namespace PlayStead.Platform.Tests.SingleInstance;

public sealed class SingleInstanceGateTests
{
    [Fact]
    public async Task Second_gate_forwards_invocation_to_primary_server_exactly_once()
    {
        var key = $"PlayStead.Tests.{Guid.NewGuid():N}";
        var mutexName = $@"Local\{key}";
        var pipeName = $"{key}.Invocation";

        using var primaryGate =
            new SingleInstanceGate(mutexName, pipeName);

        var firstResult = await primaryGate.TryAcquireAsync(
            AppInvocation.Default,
            CancellationToken.None);

        Assert.Equal(
            SingleInstanceResult.Primary,
            firstResult);

        await using var server =
            new NamedPipeInvocationServer(pipeName);

        var received = new TaskCompletionSource<AppInvocation>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var receiveCount = 0;

        server.InvocationReceived += (_, invocation) =>
        {
            Interlocked.Increment(ref receiveCount);
            received.TrySetResult(invocation);
        };

        await server.StartAsync(CancellationToken.None);

        var expected = new AppInvocation(
            Activate: true,
            DeepLink: "playstead://library/game/730");

        using var secondaryGate =
            new SingleInstanceGate(mutexName, pipeName);

        var secondResult = await secondaryGate.TryAcquireAsync(
            expected,
            CancellationToken.None);

        Assert.Equal(
            SingleInstanceResult.Forwarded,
            secondResult);

        var actual = await received.Task.WaitAsync(
            TimeSpan.FromSeconds(5));

        Assert.Equal(expected, actual);

        await Task.Delay(100);

        Assert.Equal(
            1,
            Volatile.Read(ref receiveCount));
    }

    [Fact]
    public async Task Disposing_primary_gate_releases_ownership_for_next_gate()
    {
        var key = $"PlayStead.Tests.{Guid.NewGuid():N}";
        var mutexName = $@"Local\{key}";
        var pipeName = $"{key}.Invocation";

        using (var firstGate =
               new SingleInstanceGate(mutexName, pipeName))
        {
            var firstResult = await firstGate.TryAcquireAsync(
                AppInvocation.Default,
                CancellationToken.None);

            Assert.Equal(
                SingleInstanceResult.Primary,
                firstResult);
        }

        using var nextGate =
            new SingleInstanceGate(mutexName, pipeName);

        var nextResult = await nextGate.TryAcquireAsync(
            AppInvocation.Default,
            CancellationToken.None);

        Assert.Equal(
            SingleInstanceResult.Primary,
            nextResult);
    }
}
