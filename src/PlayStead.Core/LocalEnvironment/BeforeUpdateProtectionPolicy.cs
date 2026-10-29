using PlayStead.Core.Library;

namespace PlayStead.Core.LocalEnvironment;

public enum BeforeUpdateProtectionPolicy
{
    Always = 0,
    Ask = 1,
    Never = 2
}

public interface IBeforeUpdateProtectionPolicyStore
{
    Task<BeforeUpdateProtectionPolicy> GetAsync(InstallationId installationId, CancellationToken cancellationToken);
    Task SetAsync(InstallationId installationId, BeforeUpdateProtectionPolicy policy, CancellationToken cancellationToken);
}

public sealed record BeforeUpdateProtectionPrompt(
    Guid Id,
    InstallationId InstallationId,
    Guid GameId,
    string GameTitle,
    string TargetBuildId,
    int EligibleConfigurationCount);

public interface IBeforeUpdateProtectionWorkflow
{
    IReadOnlyList<BeforeUpdateProtectionPrompt> PendingPrompts { get; }
    string? LastFailureMessage { get; }
    string? LastStatusMessage { get; }
    event EventHandler? Changed;
    Task HandleUpdateAsync(PlayStead.Core.ProviderInstallUpdate.ProviderInstallUpdateState state, CancellationToken cancellationToken);
    Task<bool> AcceptAsync(Guid promptId, CancellationToken cancellationToken);
    bool Decline(Guid promptId);
}
