using PlayStead.Providers.Steam.Remote;

namespace PlayStead.Providers.Tests.Steam.Remote;

public sealed class SteamCmdRunnerTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "PlayStead.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task RunAsync_builds_exact_anonymous_app_info_arguments()
    {
        var executablePath = CreateFakeExecutableFile();
        var invoker = new RecordingInvoker(
            new SteamCmdProcessResult(
                ExitCode: 0,
                StandardOutput: "ok",
                StandardError: string.Empty,
                Duration: TimeSpan.FromMilliseconds(12)));

        var sut = CreateRunner(
            executablePath,
            TimeSpan.FromSeconds(30),
            invoker);

        var result = await sut.RunAsync(
            new SteamCmdRequest("730"),
            CancellationToken.None);

        Assert.NotNull(invoker.LastRequest);

        Assert.Equal(
            executablePath,
            invoker.LastRequest!.ExecutablePath);

        Assert.Equal(
            [
                "+login",
                "anonymous",
                "+app_info_update",
                "1",
                "+app_info_print",
                "730",
                "+quit"
            ],
            invoker.LastRequest.Arguments);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("ok", result.StandardOutput);
        Assert.False(result.TimedOut);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("730-beta")]
    [InlineData("7 30")]
    public async Task RunAsync_rejects_non_numeric_app_ids_before_process_launch(
        string appId)
    {
        var executablePath = CreateFakeExecutableFile();
        var invoker = new RecordingInvoker(
            new SteamCmdProcessResult(
                0,
                string.Empty,
                string.Empty,
                TimeSpan.Zero));

        var sut = CreateRunner(
            executablePath,
            TimeSpan.FromSeconds(30),
            invoker);

        await Assert.ThrowsAsync<ArgumentException>(
            () => sut.RunAsync(
                new SteamCmdRequest(appId),
                CancellationToken.None));

        Assert.Null(invoker.LastRequest);
    }

    [Fact]
    public async Task RunAsync_throws_when_SteamCmd_cannot_be_resolved()
    {
        var missingPath = Path.Combine(
            _root,
            "missing",
            "steamcmd.exe");

        var invoker = new RecordingInvoker(
            new SteamCmdProcessResult(
                0,
                string.Empty,
                string.Empty,
                TimeSpan.Zero));

        var sut = CreateRunner(
            missingPath,
            TimeSpan.FromSeconds(30),
            invoker);

        await Assert.ThrowsAsync<FileNotFoundException>(
            () => sut.RunAsync(
                new SteamCmdRequest("730"),
                CancellationToken.None));

        Assert.Null(invoker.LastRequest);
    }

    [Fact]
    public async Task RunAsync_returns_timeout_and_cancels_the_process_invoker()
    {
        var executablePath = CreateFakeExecutableFile();
        var invoker = new BlockingInvoker();

        var sut = CreateRunner(
            executablePath,
            TimeSpan.FromMilliseconds(50),
            invoker);

        var result = await sut.RunAsync(
            new SteamCmdRequest("730"),
            CancellationToken.None);

        Assert.True(result.TimedOut);
        Assert.Null(result.ExitCode);
        Assert.True(invoker.CancellationObserved);
    }

    [Fact]
    public async Task RunAsync_propagates_caller_cancellation()
    {
        var executablePath = CreateFakeExecutableFile();
        var invoker = new BlockingInvoker();

        var sut = CreateRunner(
            executablePath,
            TimeSpan.FromSeconds(30),
            invoker);

        using var cts = new CancellationTokenSource(
            TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => sut.RunAsync(
                new SteamCmdRequest("730"),
                cts.Token));

        Assert.True(invoker.CancellationObserved);
    }

    private SteamCmdRunner CreateRunner(
        string executablePath,
        TimeSpan timeout,
        ISteamCmdProcessInvoker invoker)
    {
        var options = new SteamCmdOptions(
            executablePath,
            timeout);

        return new SteamCmdRunner(
            new SteamCmdPathResolver(options),
            options,
            invoker);
    }

    private string CreateFakeExecutableFile()
    {
        var path = Path.Combine(
            _root,
            "steamcmd.exe");

        Directory.CreateDirectory(_root);
        File.WriteAllText(path, string.Empty);

        return Path.GetFullPath(path);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(
                _root,
                recursive: true);
        }
    }

    private sealed class RecordingInvoker :
        ISteamCmdProcessInvoker
    {
        private readonly SteamCmdProcessResult _result;

        public RecordingInvoker(
            SteamCmdProcessResult result)
        {
            _result = result;
        }

        public SteamCmdProcessRequest? LastRequest { get; private set; }

        public Task<SteamCmdProcessResult> InvokeAsync(
            SteamCmdProcessRequest request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(_result);
        }
    }

    private sealed class BlockingInvoker :
        ISteamCmdProcessInvoker
    {
        public bool CancellationObserved { get; private set; }

        public async Task<SteamCmdProcessResult> InvokeAsync(
            SteamCmdProcessRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(
                    Timeout.InfiniteTimeSpan,
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                CancellationObserved = true;
                throw;
            }

            throw new InvalidOperationException(
                "Unreachable test path.");
        }
    }
}
