using PlayStead.Core.Catalog;
using PlayStead.Core.Library;

namespace PlayStead.Core.Identity;

public sealed record GameProviderIdentity
{
    public GameProviderIdentity(
        GameId gameId,
        ProviderKind provider,
        string externalId,
        ProviderIdentitySource source,
        CatalogConfidence confidence,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalId);
        if (createdAtUtc == default)
            throw new ArgumentException("CreatedAtUtc is required.", nameof(createdAtUtc));
        if (updatedAtUtc == default)
            throw new ArgumentException("UpdatedAtUtc is required.", nameof(updatedAtUtc));

        GameId = gameId;
        Provider = provider;
        ExternalId = externalId.Trim();
        Source = source;
        Confidence = confidence;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
    }

    public GameId GameId { get; }
    public ProviderKind Provider { get; }
    public string ExternalId { get; }
    public ProviderIdentitySource Source { get; }
    public CatalogConfidence Confidence { get; }
    public DateTimeOffset CreatedAtUtc { get; }
    public DateTimeOffset UpdatedAtUtc { get; }
}
