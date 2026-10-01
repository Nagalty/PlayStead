using PlayStead.Core.Library;
using PlayStead.Core.Media;
using PlayStead.Core.Sessions;
using PlayStead.Core.ProviderActivity;

namespace PlayStead.Core.Home;

public static class HomeEditorialProjector
{
    public static readonly TimeSpan DormantThreshold = TimeSpan.FromDays(45);

    public static HomeEditorialSnapshot Project(
        HomeEditorialProjectionInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.Library);
        ArgumentNullException.ThrowIfNull(input.ActiveSessions);
        ArgumentNullException.ThrowIfNull(input.GamesDuMoment);
        ArgumentNullException.ThrowIfNull(input.RecentCompletedSessions);
        ArgumentNullException.ThrowIfNull(input.WeeklySummary);
        ArgumentNullException.ThrowIfNull(input.AttentionItems);

        var games = input.Library.Games
            .GroupBy(game => game.Id)
            .ToDictionary(group => group.Key, group => group.First());
        var installations = input.Library.Installations
            .Where(installation => installation.IsPresent)
            .GroupBy(installation => installation.GameId)
            .ToDictionary(group => group.Key, group => group.ToArray());

        HomeEditorialGame? Resolve(Guid gameId)
        {
            var id = new GameId(gameId);
            if (!games.TryGetValue(id, out var game) ||
                !installations.TryGetValue(id, out var availableInstallations))
            {
                return null;
            }

            var installation = availableInstallations
                    .FirstOrDefault(candidate => candidate.IsPreferred)
                ?? availableInstallations[0];

            GameMediaIdentity? mediaIdentity = null;
            try
            {
                mediaIdentity = GameMediaIdentityFactory.Create(game.Id, installation, game.Title);
            }
            catch (ArgumentException)
            {
                // Media is optional; a present library game remains navigable.
            }

            return new HomeEditorialGame(game.Id, game.Title, mediaIdentity);
        }

        var recentHistory = input.RecentCompletedSessions
            .Where(IsCompleted)
            .ToArray();

        HomeEditorialActiveSession? activeSession = null;

        foreach (var session in input.ActiveSessions
                     .Where(session => session.State == SessionState.Active)
                     .OrderByDescending(session => session.ObservedStartedAtUtc)
                     .ThenBy(session => session.SessionId))
        {
            var game = Resolve(session.GameId);
            if (game is null)
            {
                continue;
            }

            activeSession = new HomeEditorialActiveSession(
                game,
                session.ObservedStartedAtUtc);
            break;
        }

        var gamesDuMoment = input.GamesDuMoment
            .Select(entry => Resolve(entry.GameId.Value))
            .Where(game => game is not null)
            .Cast<HomeEditorialGame>()
            .ToArray();

        HomeEditorialPrimary? primary = null;
        foreach (var game in gamesDuMoment)
        {
            primary = new HomeEditorialPrimary(
                HomeEditorialPrimarySourceKind.GamesDuMoment,
                game);
            break;
        }

        if (primary is null)
        {
            foreach (var session in recentHistory
                         .OrderByDescending(item => item.ObservedEndedAtUtc)
                         .ThenByDescending(item => item.ObservedStartedAtUtc)
                         .ThenByDescending(item => item.SessionId))
            {
                var game = Resolve(session.GameId);
                if (game is null)
                {
                    continue;
                }

                primary = new HomeEditorialPrimary(
                    HomeEditorialPrimarySourceKind.RecentCompletedSession,
                    game,
                    session.ObservedStartedAtUtc,
                    session.ObservedEndedAtUtc);
                break;
            }
        }

        primary ??= new HomeEditorialPrimary(
            HomeEditorialPrimarySourceKind.Placeholder,
            null);

        var remainingGamesDuMoment = gamesDuMoment
            .Where(game => game.GameId != primary.NavigationGameId)
            .ToArray();

        var excludedGameIds = input.ActiveSessions
            .Where(session => session.State == SessionState.Active)
            .Select(session => new GameId(session.GameId))
            .ToHashSet();
        if (primary.NavigationGameId is GameId primaryGameId)
        {
            excludedGameIds.Add(primaryGameId);
        }
        excludedGameIds.UnionWith(
            remainingGamesDuMoment.Select(game => game.GameId));

        var evaluatedAtUtc = input.EvaluatedAtUtc.ToUniversalTime();
        var providerActivity = (input.ProviderActivity ?? [])
            .Where(item => item.LastPlayedAtUtc is not null)
            .GroupBy(item => item.GameId)
            .ToDictionary(group => group.Key, group => group.First());
        var observedLastPlayed = recentHistory
            .GroupBy(session => new GameId(session.GameId))
            .ToDictionary(group => group.Key, group => group.Max(session => session.ObservedEndedAtUtc)!.Value.ToUniversalTime());
        var effectiveActivity = input.EffectiveActivity ?? new Dictionary<GameId, EffectiveActivitySnapshot>();
        var effectiveLastPlayed = effectiveActivity
            .Where(pair => pair.Value.EffectiveLastPlayedAtUtc is not null)
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        var dormantCandidates = observedLastPlayed.Keys
            .Concat(providerActivity.Keys)
            .Concat(effectiveLastPlayed.Keys)
            .Distinct()
            .Select(gameId => new
            {
                GameId = gameId,
                LastCompletedAtUtc = effectiveLastPlayed.TryGetValue(gameId, out var effective)
                    ? effective.EffectiveLastPlayedAtUtc
                    : observedLastPlayed.TryGetValue(gameId, out var observed) ? observed : (DateTimeOffset?)null
            });
        var eligibleDormantGames = dormantCandidates
            .Where(candidate => !excludedGameIds.Contains(candidate.GameId))
            .Where(candidate => providerActivity.ContainsKey(candidate.GameId) || candidate.LastCompletedAtUtc is not null)
            .Select(candidate =>
            {
                if (effectiveLastPlayed.TryGetValue(candidate.GameId, out var effectiveSnapshot) &&
                    effectiveSnapshot.EffectiveLastPlayedAtUtc is { } effectiveLastPlayedAtUtc)
                {
                    var effective = effectiveLastPlayedAtUtc.ToUniversalTime();
                    var source = effectiveSnapshot.EffectiveLastPlayedSource is
                        EffectiveActivitySource.ProviderLifetime or EffectiveActivitySource.ProviderRecoveredSessions
                        ? HomeEditorialLastPlayedSource.Provider
                        : HomeEditorialLastPlayedSource.Observed;
                    return new DormantCandidate(candidate.GameId, effective, source, Resolve(candidate.GameId.Value), evaluatedAtUtc - effective);
                }
                if (providerActivity.TryGetValue(candidate.GameId, out var provider) && provider.LastPlayedAtUtc is { } providerLastPlayed)
                {
                    var effective = providerLastPlayed.ToUniversalTime();
                    return new DormantCandidate(candidate.GameId, effective, HomeEditorialLastPlayedSource.Provider, Resolve(candidate.GameId.Value), evaluatedAtUtc - effective);
                }
                var observedEffective = candidate.LastCompletedAtUtc!.Value;
                return new DormantCandidate(candidate.GameId, observedEffective, HomeEditorialLastPlayedSource.Observed, Resolve(candidate.GameId.Value), evaluatedAtUtc - observedEffective);
            })
            .Where(candidate =>
                candidate.Game is not null &&
                candidate.Elapsed >= DormantThreshold)
            .OrderBy(candidate => candidate.LastPlayedAtUtc)
            .ThenBy(candidate => candidate.GameId.Value)
            .Select(candidate => new HomeEditorialDormantGame(
                candidate.Game!,
                candidate.LastPlayedAtUtc,
                (int)Math.Floor(candidate.Elapsed.TotalDays),
                candidate.LastPlayedSource))
            .ToArray();

        return new HomeEditorialSnapshot(
            activeSession,
            primary,
            eligibleDormantGames.FirstOrDefault(),
            gamesDuMoment,
            remainingGamesDuMoment,
            recentHistory,
            input.WeeklySummary,
            input.AttentionItems.ToArray())
        {
            EligibleDormantGames = eligibleDormantGames
        };
    }

    private static bool IsCompleted(GameSession session) =>
        session.ObservedEndedAtUtc is not null &&
        session.State is SessionState.Ended or SessionState.Recovered;

    private sealed record DormantCandidate(
        GameId GameId,
        DateTimeOffset LastPlayedAtUtc,
        HomeEditorialLastPlayedSource LastPlayedSource,
        HomeEditorialGame? Game,
        TimeSpan Elapsed);
}
