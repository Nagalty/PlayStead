using Microsoft.Data.Sqlite;
using PlayStead.Core.Sessions;
using PlayStead.Data.Database;
using PlayStead.Data.Sessions;

namespace PlayStead.Data.Tests.Sessions;

public sealed class SqliteSessionStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "PlayStead.Tests",
        Guid.NewGuid().ToString("N"));

    private static readonly Guid GameA =
        Guid.Parse("66666666-6666-6666-6666-666666666666");

    private static readonly Guid GameB =
        Guid.Parse("77777777-7777-7777-7777-777777777777");

    private static readonly DateTimeOffset T0 =
        new(2026, 9, 12, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Upsert_round_trips_active_session_and_updates_same_identity_when_ended()
    {
        var context = await CreateStoreAsync(GameA);

        var active = ActiveSession(
            Guid.Parse("88888888-8888-8888-8888-888888888888"),
            GameA,
            T0);

        await context.Store.UpsertAsync(
            active,
            CancellationToken.None);

        Assert.Equal(
            active,
            await context.Store.GetAsync(
                active.SessionId,
                CancellationToken.None));

        var ended = active with
        {
            LastSeenAtUtc = T0.AddMinutes(15),
            ObservedEndedAtUtc = T0.AddMinutes(15),
            State = SessionState.Ended,
            EndReason = SessionEndReason.ProcessExited,
            UpdatedAtUtc = T0.AddMinutes(16)
        };

        await context.Store.UpsertAsync(
            ended,
            CancellationToken.None);

        Assert.Equal(
            ended,
            await context.Store.GetAsync(
                ended.SessionId,
                CancellationToken.None));
    }

    [Fact]
    public async Task GetActive_returns_all_simultaneous_active_sessions_and_excludes_ended()
    {
        var context = await CreateStoreAsync(
            GameA,
            GameB);

        var first = ActiveSession(
            Guid.Parse("99999999-9999-9999-9999-999999999991"),
            GameA,
            T0);

        var second = ActiveSession(
            Guid.Parse("99999999-9999-9999-9999-999999999992"),
            GameB,
            T0.AddMinutes(1));

        var ended = ActiveSession(
            Guid.Parse("99999999-9999-9999-9999-999999999993"),
            GameA,
            T0.AddMinutes(2)) with
        {
            LastSeenAtUtc = T0.AddMinutes(3),
            ObservedEndedAtUtc = T0.AddMinutes(3),
            State = SessionState.Ended,
            EndReason = SessionEndReason.ProcessExited,
            UpdatedAtUtc = T0.AddMinutes(3)
        };

        await context.Store.UpsertAsync(
            first,
            CancellationToken.None);

        await context.Store.UpsertAsync(
            second,
            CancellationToken.None);

        await context.Store.UpsertAsync(
            ended,
            CancellationToken.None);

        var active = await context.Store.GetActiveAsync(
            CancellationToken.None);

        Assert.Equal(2, active.Count);
        Assert.Contains(
            active,
            x => x.SessionId == first.SessionId);
        Assert.Contains(
            active,
            x => x.SessionId == second.SessionId);
        Assert.DoesNotContain(
            active,
            x => x.SessionId == ended.SessionId);
    }

    [Fact]
    public async Task GetRecent_returns_newest_observed_start_first_and_honors_limit()
    {
        var context = await CreateStoreAsync(GameA);

        var first = ActiveSession(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1"),
            GameA,
            T0);

        var second = ActiveSession(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa2"),
            GameA,
            T0.AddMinutes(10));

        var third = ActiveSession(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa3"),
            GameA,
            T0.AddMinutes(20));

        await context.Store.UpsertAsync(first, CancellationToken.None);
        await context.Store.UpsertAsync(second, CancellationToken.None);
        await context.Store.UpsertAsync(third, CancellationToken.None);

        var recent = await context.Store.GetRecentAsync(
            2,
            CancellationToken.None);

        Assert.Equal(
            [third.SessionId, second.SessionId],
            recent.Select(x => x.SessionId).ToArray());
    }

    [Fact]
    public async Task GetByGame_returns_only_requested_game_newest_first()
    {
        var context = await CreateStoreAsync(
            GameA,
            GameB);

        var olderA = ActiveSession(
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb1"),
            GameA,
            T0);

        var gameB = ActiveSession(
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb2"),
            GameB,
            T0.AddMinutes(5));

        var newerA = ActiveSession(
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb3"),
            GameA,
            T0.AddMinutes(10));

        await context.Store.UpsertAsync(
            olderA,
            CancellationToken.None);

        await context.Store.UpsertAsync(
            gameB,
            CancellationToken.None);

        await context.Store.UpsertAsync(
            newerA,
            CancellationToken.None);

        var sessions =
            await context.Store.GetByGameAsync(
                GameA,
                CancellationToken.None);

        Assert.Equal(
            [newerA.SessionId, olderA.SessionId],
            sessions.Select(
                session => session.SessionId).ToArray());
    }

    private async Task<StoreContext> CreateStoreAsync(
        params Guid[] gameIds)
    {
        Directory.CreateDirectory(_root);

        var databasePath = Path.Combine(
            _root,
            $"{Guid.NewGuid():N}.db");

        var options = new DatabaseOptions(
            databasePath,
            Path.Combine(_root, "Backups"));

        await new DatabaseInitializer(options)
            .InitializeAsync(CancellationToken.None);

        foreach (var gameId in gameIds)
        {
            await InsertGameAsync(
                databasePath,
                gameId);
        }

        ISessionStore store =
            new SqliteSessionStore(options);

        return new StoreContext(
            store,
            databasePath);
    }

    private static async Task InsertGameAsync(
        string databasePath,
        Guid gameId)
    {
        await using var connection = new SqliteConnection(
            $"Data Source={databasePath};Pooling=False");

        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO games(
                game_id,
                title,
                is_hidden,
                created_utc,
                updated_utc)
            VALUES(
                $gameId,
                $title,
                0,
                $createdUtc,
                $updatedUtc);
            """;

        command.Parameters.AddWithValue(
            "$gameId",
            gameId.ToString());

        command.Parameters.AddWithValue(
            "$title",
            $"Game {gameId:N}");

        command.Parameters.AddWithValue(
            "$createdUtc",
            T0.ToString("O"));

        command.Parameters.AddWithValue(
            "$updatedUtc",
            T0.ToString("O"));

        await command.ExecuteNonQueryAsync();
    }

    private static GameSession ActiveSession(
        Guid sessionId,
        Guid gameId,
        DateTimeOffset startedAtUtc)
        => new(
            sessionId,
            gameId,
            startedAtUtc,
            startedAtUtc,
            null,
            SessionState.Active,
            null,
            SessionDetectionSource.ProcessMonitor,
            startedAtUtc,
            startedAtUtc);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();

        if (Directory.Exists(_root))
        {
            Directory.Delete(
                _root,
                recursive: true);
        }
    }

    private sealed record StoreContext(
        ISessionStore Store,
        string DatabasePath);
}
