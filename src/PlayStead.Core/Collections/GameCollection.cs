using PlayStead.Core.Library;

namespace PlayStead.Core.Collections;

public sealed record GameCollection(
    Guid Id,
    string Name,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record GameCollectionMembership(
    Guid CollectionId,
    GameId GameId);
