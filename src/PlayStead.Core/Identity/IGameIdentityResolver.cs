namespace PlayStead.Core.Identity;

public interface IGameIdentityResolver
{
    Task<IdentityResolutionResult> ResolveAsync(
        GameIdentityObservation observation,
        CancellationToken cancellationToken);
}
