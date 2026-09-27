namespace PlayStead.Core.ProviderActivity;

public interface IProviderActivityMetadataStore
{
    Task<IReadOnlyList<ProviderActivityMetadata>> GetAllAsync(CancellationToken cancellationToken);
    Task UpsertAsync(ProviderActivityMetadata metadata, CancellationToken cancellationToken);
}
