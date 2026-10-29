using PlayStead.Core.LocalEnvironment;

namespace PlayStead.Core.ProviderInstallUpdate;

public sealed class ProviderInstallUpdateProtectionPolicyAdapter(IBeforeUpdateProtectionWorkflow workflow) : IProviderInstallUpdateProtectionService
{
    public Task ProtectBeforeUpdateAsync(ProviderInstallUpdateState state, CancellationToken cancellationToken) => workflow.HandleUpdateAsync(state, cancellationToken);
}
