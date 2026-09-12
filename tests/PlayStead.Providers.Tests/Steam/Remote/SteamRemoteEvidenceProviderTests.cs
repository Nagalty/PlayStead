using PlayStead.Core.Steam;
using PlayStead.Providers.Steam.Remote;

namespace PlayStead.Providers.Tests.Steam.Remote;

public sealed class SteamRemoteEvidenceProviderTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 12, 19, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task QueryAsync_returns_parser_success()
    {
        var expected = new SteamRemoteEvidenceResult(
            SteamRemoteEvidenceStatus.Success,
            new SteamRemoteEvidence(
                "730",
                "public",
                "101",
                new Dictionary<string, string>
                {
                    ["731"] = "111"
                },
                Now,
                SteamRemoteEvidenceSource.SteamCmdAnonymous),
            FailureKind: null);

        var runner = new FakeRunner(
            new SteamCmdRunResult(
                ExitCode: 0,
                StandardOutput: "APPINFO",
                StandardError: string.Empty,
                TimedOut: false,
                Duration: TimeSpan.FromMilliseconds(20)));

        var parser = new FakeParser(expected);

        var sut = new SteamRemoteEvidenceProvider(
            runner,
            parser,
            new FixedTimeProvider(Now));

        var actual = await sut.QueryAsync(
            "730",
            "public",
            CancellationToken.None);

        Assert.Equal(expected, actual);

        Assert.Equal("730", runner.LastRequest?.AppId);
        Assert.Equal("730", parser.LastAppId);
        Assert.Equal("public", parser.LastBranchName);
        Assert.Equal("APPINFO", parser.LastRawOutput);
        Assert.Equal(Now, parser.LastObservedAtUtc);
    }

    [Fact]
    public async Task QueryAsync_maps_missing_SteamCmd_to_refresh_failed()
    {
        var runner = new ThrowingRunner(
            new FileNotFoundException(
                "steamcmd.exe could not be resolved."));

        var parser = new FakeParser(
            new SteamRemoteEvidenceResult(
                SteamRemoteEvidenceStatus.RefreshFailed,
                Evidence: null,
                SteamRemoteFailureKind.MalformedOutput));

        var sut = new SteamRemoteEvidenceProvider(
            runner,
            parser,
            new FixedTimeProvider(Now));

        var actual = await sut.QueryAsync(
            "730",
            "public",
            CancellationToken.None);

        Assert.Equal(
            SteamRemoteEvidenceStatus.RefreshFailed,
            actual.Status);

        Assert.Null(actual.Evidence);
        Assert.Equal(
            SteamRemoteFailureKind.SteamCmdMissing,
            actual.FailureKind);

        Assert.Equal(0, parser.CallCount);
    }

    [Fact]
    public async Task QueryAsync_maps_runner_timeout_to_refresh_failed()
    {
        var runner = new FakeRunner(
            new SteamCmdRunResult(
                ExitCode: null,
                StandardOutput: string.Empty,
                StandardError: string.Empty,
                TimedOut: true,
                Duration: TimeSpan.FromSeconds(30)));

        var parser = new FakeParser(
            new SteamRemoteEvidenceResult(
                SteamRemoteEvidenceStatus.RefreshFailed,
                Evidence: null,
                SteamRemoteFailureKind.MalformedOutput));

        var sut = new SteamRemoteEvidenceProvider(
            runner,
            parser,
            new FixedTimeProvider(Now));

        var actual = await sut.QueryAsync(
            "730",
            "public",
            CancellationToken.None);

        Assert.Equal(
            SteamRemoteEvidenceStatus.RefreshFailed,
            actual.Status);

        Assert.Equal(
            SteamRemoteFailureKind.Timeout,
            actual.FailureKind);

        Assert.Equal(0, parser.CallCount);
    }

    [Fact]
    public async Task QueryAsync_maps_non_zero_exit_code_to_refresh_failed()
    {
        var runner = new FakeRunner(
            new SteamCmdRunResult(
                ExitCode: 7,
                StandardOutput: "partial",
                StandardError: "failed",
                TimedOut: false,
                Duration: TimeSpan.FromMilliseconds(50)));

        var parser = new FakeParser(
            new SteamRemoteEvidenceResult(
                SteamRemoteEvidenceStatus.RefreshFailed,
                Evidence: null,
                SteamRemoteFailureKind.MalformedOutput));

        var sut = new SteamRemoteEvidenceProvider(
            runner,
            parser,
            new FixedTimeProvider(Now));

        var actual = await sut.QueryAsync(
            "730",
            "public",
            CancellationToken.None);

        Assert.Equal(
            SteamRemoteEvidenceStatus.RefreshFailed,
            actual.Status);

        Assert.Equal(
            SteamRemoteFailureKind.NonZeroExitCode,
            actual.FailureKind);

        Assert.Equal(0, parser.CallCount);
    }

    [Fact]
    public async Task QueryAsync_preserves_caller_cancellation()
    {
        var runner = new ThrowingRunner(
            new OperationCanceledException());

        var parser = new FakeParser(
            new SteamRemoteEvidenceResult(
                SteamRemoteEvidenceStatus.RefreshFailed,
                Evidence: null,
                SteamRemoteFailureKind.MalformedOutput));

        var sut = new SteamRemoteEvidenceProvider(
            runner,
            parser,
            new FixedTimeProvider(Now));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => sut.QueryAsync(
                "730",
                "public",
                CancellationToken.None));
    }

    private sealed class FakeRunner :
        ISteamCmdRunner
    {
        private readonly SteamCmdRunResult _result;

        public FakeRunner(SteamCmdRunResult result)
        {
            _result = result;
        }

        public SteamCmdRequest? LastRequest { get; private set; }

        public Task<SteamCmdRunResult> RunAsync(
            SteamCmdRequest request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(_result);
        }
    }

    private sealed class ThrowingRunner :
        ISteamCmdRunner
    {
        private readonly Exception _exception;

        public ThrowingRunner(Exception exception)
        {
            _exception = exception;
        }

        public Task<SteamCmdRunResult> RunAsync(
            SteamCmdRequest request,
            CancellationToken cancellationToken)
            => Task.FromException<SteamCmdRunResult>(
                _exception);
    }

    private sealed class FakeParser :
        ISteamCmdAppInfoParser
    {
        private readonly SteamRemoteEvidenceResult _result;

        public FakeParser(
            SteamRemoteEvidenceResult result)
        {
            _result = result;
        }

        public int CallCount { get; private set; }

        public string? LastAppId { get; private set; }

        public string? LastBranchName { get; private set; }

        public string? LastRawOutput { get; private set; }

        public DateTimeOffset? LastObservedAtUtc { get; private set; }

        public SteamRemoteEvidenceResult Parse(
            string appId,
            string branchName,
            string rawOutput,
            DateTimeOffset observedAtUtc)
        {
            CallCount++;
            LastAppId = appId;
            LastBranchName = branchName;
            LastRawOutput = rawOutput;
            LastObservedAtUtc = observedAtUtc;

            return _result;
        }
    }

    private sealed class FixedTimeProvider :
        TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() =>
            _utcNow;
    }
}
