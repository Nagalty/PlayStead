using PlayStead.Core.Library;

namespace PlayStead.Core.ProviderInstallUpdate;

public interface IProviderInstallUpdateStateSource
{
    ProviderKind Provider { get; }

    Task<IReadOnlyList<ProviderInstallUpdateState>> GetAsync(
        IReadOnlyCollection<GameInstallation> installations,
        CancellationToken cancellationToken);
}
