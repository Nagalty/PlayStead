namespace PlayStead.Providers.Steam.Remote;

public sealed class SteamCmdRunner :
    ISteamCmdRunner
{
    private readonly SteamCmdPathResolver _pathResolver;
    private readonly SteamCmdOptions _options;
    private readonly ISteamCmdProcessInvoker _processInvoker;

    public SteamCmdRunner(
        SteamCmdPathResolver pathResolver,
        SteamCmdOptions options,
        ISteamCmdProcessInvoker processInvoker)
    {
        ArgumentNullException.ThrowIfNull(pathResolver);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(processInvoker);

        if (options.QueryTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "SteamCMD query timeout must be greater than zero.");
        }

        _pathResolver = pathResolver;
        _options = options;
        _processInvoker = processInvoker;
    }

    public async Task<SteamCmdRunResult> RunAsync(
        SteamCmdRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        ValidateAppId(
            request.AppId);

        cancellationToken.ThrowIfCancellationRequested();

        var executablePath =
            _pathResolver.Resolve();

        if (executablePath is null)
        {
            throw new FileNotFoundException(
                "steamcmd.exe could not be resolved.");
        }

        var processRequest =
            new SteamCmdProcessRequest(
                executablePath,
                [
                    "+login",
                    "anonymous",
                    "+app_info_update",
                    "1",
                    "+app_info_print",
                    request.AppId,
                    "+quit"
                ]);

        using var timeoutCts =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);

        timeoutCts.CancelAfter(
            _options.QueryTimeout);

        try
        {
            var result =
                await _processInvoker.InvokeAsync(
                    processRequest,
                    timeoutCts.Token)
                .ConfigureAwait(false);

            return new SteamCmdRunResult(
                result.ExitCode,
                result.StandardOutput,
                result.StandardError,
                TimedOut: false,
                result.Duration);
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested &&
                  timeoutCts.IsCancellationRequested)
        {
            return new SteamCmdRunResult(
                ExitCode: null,
                StandardOutput: string.Empty,
                StandardError: string.Empty,
                TimedOut: true,
                Duration: _options.QueryTimeout);
        }
    }

    private static void ValidateAppId(
        string appId)
    {
        if (string.IsNullOrWhiteSpace(appId) ||
            !appId.All(char.IsAsciiDigit))
        {
            throw new ArgumentException(
                "Steam AppId must be numeric.",
                nameof(appId));
        }
    }
}
