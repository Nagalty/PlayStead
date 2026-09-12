namespace PlayStead.Core.Library;

public sealed record LogicalGame(
    GameId Id,
    string Title,
    bool IsHidden,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
