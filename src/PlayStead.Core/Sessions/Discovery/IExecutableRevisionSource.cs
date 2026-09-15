namespace PlayStead.Core.Sessions.Discovery;

public interface IExecutableRevisionSource
{
    Task<ExecutableRevisionResult> ReadAsync(
        InstallationScope scope, string executablePath,
        CancellationToken cancellationToken);
}
