using PlayStead.Core.Library;

namespace PlayStead.Core.ProviderActivity;

public interface IProviderActivityMetadataSource
{
    ProviderKind Provider { get; }
    Task<IReadOnlyList<ProviderActivityMetadata>> GetAsync(
        IReadOnlyCollection<GameInstallation> installations,
        CancellationToken cancellationToken);
}
