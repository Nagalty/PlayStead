using PlayStead.Core.Steam;

namespace PlayStead.Providers.Steam.Remote;

public sealed class SteamRemoteEvidenceProvider :
    ISteamRemoteEvidenceProvider
{
    private readonly ISteamCmdRunner _runner;
    private readonly ISteamCmdAppInfoParser _parser;
    private readonly TimeProvider _timeProvider;

    public SteamRemoteEvidenceProvider(
        ISteamCmdRunner runner,
        ISteamCmdAppInfoParser parser,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentNullException.ThrowIfNull(parser);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _runner = runner;
        _parser = parser;
        _timeProvider = timeProvider;
    }

    public async Task<SteamRemoteEvidenceResult> QueryAsync(
        string appId,
        string branchName,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);
        ArgumentException.ThrowIfNullOrWhiteSpace(branchName);

        SteamCmdRunResult runResult;

        try
        {
            runResult = await _runner.RunAsync(
                new SteamCmdRequest(appId),
                cancellationToken)
                .ConfigureAwait(false);
        }
        catch (FileNotFoundException)
        {
            return Failure(
                SteamRemoteFailureKind.SteamCmdMissing);
        }
        catch (OperationCanceledException)
        {
            throw;
        }

        if (runResult.TimedOut)
        {
            return Failure(
                SteamRemoteFailureKind.Timeout);
        }

        if (runResult.ExitCode is null or not 0)
        {
            return Failure(
                SteamRemoteFailureKind.NonZeroExitCode);
        }

        return _parser.Parse(
            appId,
            branchName,
            runResult.StandardOutput,
            _timeProvider.GetUtcNow());
    }

    private static SteamRemoteEvidenceResult Failure(
        SteamRemoteFailureKind failureKind)
        => new(
            SteamRemoteEvidenceStatus.RefreshFailed,
            Evidence: null,
            failureKind);
}
