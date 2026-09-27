using PlayStead.Core.Library;

namespace PlayStead.Core.ProviderGameMetadata;

public interface IProviderGameMetadataSource
{
    ProviderKind Provider { get; }
    Task<IReadOnlyList<ProviderGameMetadataPatch>> GetAsync(
        LibrarySnapshot snapshot,
        CancellationToken cancellationToken);
}
