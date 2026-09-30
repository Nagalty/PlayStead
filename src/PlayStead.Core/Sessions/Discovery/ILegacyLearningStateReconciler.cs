using PlayStead.Core.Library;

namespace PlayStead.Core.Sessions.Discovery;

public interface ILegacyLearningStateReconciler
{
    Task ReconcileAsync(ProviderKind provider, InstallationScope scope,
        CancellationToken cancellationToken);
}
