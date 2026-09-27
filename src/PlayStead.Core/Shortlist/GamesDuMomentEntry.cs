namespace PlayStead.Core.Shortlist;

using PlayStead.Core.Library;

public sealed record GamesDuMomentEntry(GameId GameId, int Position, DateTimeOffset AddedAtUtc);
