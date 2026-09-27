using PlayStead.Providers.Steam;

namespace PlayStead.Providers.Tests.Steam;

public sealed class SteamInstallUpdateLiveRefreshServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "PlaySteadLiveRefresh", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Activation_after_threshold_triggers_one_refresh()
    {
        var calls = 0;
        using var sut = Create(() => { Interlocked.Increment(ref calls); return Task.CompletedTask; });
        var now = DateTimeOffset.UtcNow;
        sut.NotifyDeactivated(now - TimeSpan.FromMinutes(5));
        sut.NotifyActivated(now);
        await EventuallyAsync(() => Volatile.Read(ref calls) == 1);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Activation_before_threshold_does_not_refresh()
    {
        var calls = 0;
        using var sut = Create(() => { Interlocked.Increment(ref calls); return Task.CompletedTask; });
        var now = DateTimeOffset.UtcNow;
        sut.NotifyDeactivated(now - TimeSpan.FromMinutes(4));
        sut.NotifyActivated(now);
        await Task.Delay(40);
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task Repeated_activation_events_do_not_spam_refresh()
    {
        var calls = 0;
        using var sut = Create(() => { Interlocked.Increment(ref calls); return Task.CompletedTask; });
        var now = DateTimeOffset.UtcNow;
        sut.NotifyDeactivated(now - TimeSpan.FromMinutes(6));
        sut.NotifyActivated(now);
        sut.NotifyActivated(now.AddSeconds(1));
        await EventuallyAsync(() => Volatile.Read(ref calls) == 1);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Refresh_in_progress_is_not_run_concurrently()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var active = 0;
        var maxActive = 0;
        using var sut = Create(async () =>
        {
            var current = Interlocked.Increment(ref active);
            Interlocked.Exchange(ref maxActive, Math.Max(maxActive, current));
            entered.SetResult();
            await release.Task;
            Interlocked.Decrement(ref active);
        });
        var now = DateTimeOffset.UtcNow;
        sut.NotifyDeactivated(now - TimeSpan.FromMinutes(6));
        sut.NotifyActivated(now);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        sut.NotifyDeactivated(now - TimeSpan.FromMinutes(6));
        sut.NotifyActivated(now.AddSeconds(1));
        release.SetResult();
        await Task.Delay(40);
        Assert.Equal(1, maxActive);
    }

    [Fact]
    public void Disposal_prevents_later_focus_refresh()
    {
        var calls = 0;
        var sut = Create(() => { Interlocked.Increment(ref calls); return Task.CompletedTask; });
        sut.Dispose();
        var now = DateTimeOffset.UtcNow;
        sut.NotifyDeactivated(now - TimeSpan.FromMinutes(6));
        sut.NotifyActivated(now);
        Assert.Equal(0, calls);
    }

    [Fact]
    public void Fallback_interval_remains_at_least_fifteen_minutes()
    {
        using var sut = Create(null);
        Assert.True(sut.FallbackInterval >= TimeSpan.FromMinutes(15));
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private SteamInstallUpdateLiveRefreshService Create(Func<Task>? refresh)
    {
        Directory.CreateDirectory(Path.Combine(_root, "steamapps"));
        return new SteamInstallUpdateLiveRefreshService(
            new WindowsSteamRootLocator([_root]),
            new SteamLibraryFoldersReader(),
            refresh is null ? null : _ => refresh());
    }

    private static async Task EventuallyAsync(Func<bool> predicate)
    {
        for (var i = 0; i < 40; i++)
        {
            if (predicate()) return;
            await Task.Delay(25);
        }
        Assert.True(predicate());
    }
}
