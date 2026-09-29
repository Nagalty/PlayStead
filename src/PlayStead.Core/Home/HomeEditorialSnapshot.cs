using PlayStead.Core.Library;
using PlayStead.Core.Media;
using PlayStead.Core.Notifications;
using PlayStead.Core.Sessions;
using PlayStead.Core.Shortlist;
using PlayStead.Core.ProviderActivity;

namespace PlayStead.Core.Home;

public enum HomeEditorialPrimarySourceKind
{
    GamesDuMoment,
    RecentCompletedSession,
    Placeholder
}

public sealed record HomeEditorialGame(
    GameId GameId,
    string Title,
    GameMediaIdentity? MediaIdentity);

public sealed record HomeEditorialPrimary(
    HomeEditorialPrimarySourceKind SourceKind,
    HomeEditorialGame? Game,
    DateTimeOffset? SessionStartedAtUtc = null,
    DateTimeOffset? SessionEndedAtUtc = null)
{
    public GameId? NavigationGameId => Game?.GameId;
}

public sealed record HomeEditorialActiveSession(
    HomeEditorialGame Game,
    DateTimeOffset StartedAtUtc)
{
    public GameId NavigationGameId => Game.GameId;
}

public sealed record HomeEditorialDormantGame(
    HomeEditorialGame Game,
    DateTimeOffset LastCompletedAtUtc,
    int DaysSinceLastPlayed,
    HomeEditorialLastPlayedSource LastPlayedSource = HomeEditorialLastPlayedSource.Observed)
{
    public GameId NavigationGameId => Game.GameId;
}

public enum HomeEditorialLastPlayedSource
{
    Observed,
    Provider
}

public sealed record HomeEditorialSnapshot(
    HomeEditorialActiveSession? ActiveSession,
    HomeEditorialPrimary Primary,
    HomeEditorialDormantGame? DormantGame,
    IReadOnlyList<HomeEditorialGame> GamesDuMoment,
    IReadOnlyList<HomeEditorialGame> RemainingGamesDuMoment,
    IReadOnlyList<GameSession> RecentHistory,
    WeeklyActivitySummary WeeklySummary,
    IReadOnlyList<AttentionItem> AttentionItems)
{
    public IReadOnlyList<HomeEditorialDormantGame> EligibleDormantGames { get; init; } = [];
}

public sealed record HomeEditorialProjectionInput(
    LibrarySnapshot Library,
    IReadOnlyList<GameSession> ActiveSessions,
    IReadOnlyList<GamesDuMomentEntry> GamesDuMoment,
    IReadOnlyList<GameSession> RecentCompletedSessions,
    WeeklyActivitySummary WeeklySummary,
    IReadOnlyList<AttentionItem> AttentionItems,
    DateTimeOffset EvaluatedAtUtc,
    IReadOnlyList<ProviderActivityMetadata>? ProviderActivity = null,
    IReadOnlyDictionary<GameId, EffectiveActivitySnapshot>? EffectiveActivity = null);
