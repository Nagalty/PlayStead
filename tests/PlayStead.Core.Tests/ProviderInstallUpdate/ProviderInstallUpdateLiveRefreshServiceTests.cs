using PlayStead.Core.ProviderInstallUpdate;

namespace PlayStead.Core.Tests.ProviderInstallUpdate;

public sealed class ProviderInstallUpdateLiveRefreshServiceTests
{
    [Fact]
    public async Task Trigger_is_single_flight_and_coalesced()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        using var service = new ProviderInstallUpdateLiveRefreshService();
        service.SetRefresh(async _ =>
        {
            Interlocked.Increment(ref calls);
            entered.SetResult();
            await release.Task;
        });

        service.Trigger();
        service.Trigger();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        release.SetResult();
        await Task.Delay(50);

        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task Timer_refreshes_and_dispose_stops_future_ticks()
    {
        var calls = 0;
        using var service = new ProviderInstallUpdateLiveRefreshService { Interval = TimeSpan.FromMilliseconds(20) };
        service.SetRefresh(_ => { Interlocked.Increment(ref calls); return Task.CompletedTask; });
        service.Start();
        for (var i = 0; i < 50 && Volatile.Read(ref calls) == 0; i++) await Task.Delay(10);
        Assert.True(calls > 0);
        var before = calls;
        service.Dispose();
        await Task.Delay(50);
        Assert.Equal(before, calls);
    }
}
