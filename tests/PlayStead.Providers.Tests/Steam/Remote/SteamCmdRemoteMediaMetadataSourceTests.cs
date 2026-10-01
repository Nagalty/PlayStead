using PlayStead.Providers.Steam;
using PlayStead.Providers.Steam.Remote;

namespace PlayStead.Providers.Tests.Steam.Remote;

public sealed class SteamCmdRemoteMediaMetadataSourceTests
{
    [Fact]
    public async Task Concurrent_requests_use_single_flight()
    {
        var runner = new FakeRunner(FixtureText("appinfo_media_007.txt"));
        var source = new SteamCmdRemoteMediaMetadataSource(
            runner,
            new SteamCmdAppInfoParser(),
            TimeProvider.System);

        var results = await Task.WhenAll(
            Enumerable.Range(0, 5).Select(_ => source.GetAsync("3768760", CancellationToken.None)));

        Assert.All(results, result => Assert.NotNull(result));
        Assert.Equal(1, runner.CallCount);
    }

    [Fact]
    public async Task Fresh_cache_avoids_second_steamcmd_call()
    {
        var runner = new FakeRunner(FixtureText("appinfo_media_007.txt"));
        var source = new SteamCmdRemoteMediaMetadataSource(
            runner,
            new SteamCmdAppInfoParser(),
            TimeProvider.System);

        _ = await source.GetAsync("3768760", CancellationToken.None);
        _ = await source.GetAsync("3768760", CancellationToken.None);

        Assert.Equal(1, runner.CallCount);
    }

    [Fact]
    public async Task Negative_result_is_cached_temporarily()
    {
        var runner = new FakeRunner("malformed");
        var source = new SteamCmdRemoteMediaMetadataSource(
            runner,
            new SteamCmdAppInfoParser(),
            TimeProvider.System);

        Assert.Null(await source.GetAsync("3768760", CancellationToken.None));
        Assert.Null(await source.GetAsync("3768760", CancellationToken.None));

        Assert.Equal(1, runner.CallCount);
    }

    private static string FixtureText(string name) =>
        File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "Steam",
            "Remote",
            name));

    private sealed class FakeRunner(string output) : ISteamCmdRunner
    {
        private int _callCount;

        public int CallCount => _callCount;

        public async Task<SteamCmdRunResult> RunAsync(
            SteamCmdRequest request,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _callCount);
            await Task.Delay(10, cancellationToken);
            return new SteamCmdRunResult(0, output, string.Empty, false, TimeSpan.FromMilliseconds(10));
        }
    }
}
