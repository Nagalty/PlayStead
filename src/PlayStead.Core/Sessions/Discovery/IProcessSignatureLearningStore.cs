using PlayStead.Core.Library;

namespace PlayStead.Core.Sessions.Discovery;

public interface IProcessSignatureLearningStore
{
    Task<ProcessSignatureLearningState?> LoadAsync(InstallationId installationId, CancellationToken cancellationToken);
    Task<bool> TrySaveAsync(ProcessSignatureLearningState state, Guid? expectedConcurrencyToken,
        CancellationToken cancellationToken);
}
