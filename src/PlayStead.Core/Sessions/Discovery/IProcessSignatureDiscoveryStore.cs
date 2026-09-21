namespace PlayStead.Core.Sessions.Discovery;

public interface IProcessSignatureDiscoveryStore
{
    Task<bool> TryInsertDiscoveredIfAbsentAsync(DiscoveredSignatureWrite write, CancellationToken cancellationToken);
    Task<bool> TryRevalidateDiscoveredAsync(DiscoveredSignatureWrite write, DiscoveredSignatureExpectation expected,
        CancellationToken cancellationToken);
    Task<bool> TryRestoreDiscoveredValidationAsync(Guid gameId, DiscoveredSignatureExpectation expected,
        CancellationToken cancellationToken);
    Task<bool> TryInvalidateDiscoveredAsync(Guid gameId, DiscoveredSignatureExpectation expected,
        CancellationToken cancellationToken);
}
