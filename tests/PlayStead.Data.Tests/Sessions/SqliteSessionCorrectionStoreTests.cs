using Microsoft.Data.Sqlite;
using PlayStead.Core.Sessions;
using PlayStead.Data.Database;
using PlayStead.Data.Sessions;

namespace PlayStead.Data.Tests.Sessions;

public sealed class SqliteSessionCorrectionStoreTests : IDisposable
{
    private readonly string _root =
        Path.Combine(
            Path.GetTempPath(),
            "PlayStead.Tests",
            Guid.NewGuid().ToString("N"));

    private static readonly Guid GameId =
        Guid.Parse(
            "aaaaaaaa-1111-4444-8888-aaaaaaaaaaaa");

    private static readonly Guid SessionId =
        Guid.Parse(
            "aaaaaaaa-2222-4444-8888-aaaaaaaaaaaa");

    private static readonly DateTimeOffset T0 =
        new(2026, 9, 13, 1, 5, 0, TimeSpan.Zero);

    [Fact]
    public async Task Upsert_round_trips_manual_correction_without_changing_observed_session()
    {
        var context =
            await CreateContextAsync();

        var observed =
            EndedSession();

        await context.SessionStore.UpsertAsync(
            observed,
            CancellationToken.None);

        var correction =
            new SessionCorrection(
                SessionId,
                CorrectedStartedAtUtc:
                    T0.AddMinutes(-4),
                CorrectedEndedAtUtc:
                    T0.AddMinutes(64),
                CorrectedAtUtc:
                    T0.AddMinutes(70));

        await context.CorrectionStore.UpsertAsync(
            correction,
            CancellationToken.None);

        Assert.Equal(
            correction,
            await context.CorrectionStore.GetAsync(
                SessionId,
                CancellationToken.None));

        Assert.Equal(
            observed,
            await context.SessionStore.GetAsync(
                SessionId,
                CancellationToken.None));
    }

    [Fact]
    public async Task Upsert_replaces_correction_for_same_session_identity()
    {
        var context =
            await CreateContextAsync();

        await context.SessionStore.UpsertAsync(
            EndedSession(),
            CancellationToken.None);

        var first =
            new SessionCorrection(
                SessionId,
                CorrectedStartedAtUtc:
                    T0.AddMinutes(-2),
                CorrectedEndedAtUtc:
                    null,
                CorrectedAtUtc:
                    T0.AddMinutes(70));

        var second =
            new SessionCorrection(
                SessionId,
                CorrectedStartedAtUtc:
                    T0.AddMinutes(-6),
                CorrectedEndedAtUtc:
                    T0.AddMinutes(66),
                CorrectedAtUtc:
                    T0.AddMinutes(80));

        await context.CorrectionStore.UpsertAsync(
            first,
            CancellationToken.None);

        await context.CorrectionStore.UpsertAsync(
            second,
            CancellationToken.None);

        Assert.Equal(
            second,
            await context.CorrectionStore.GetAsync(
                SessionId,
                CancellationToken.None));
    }

    [Fact]
    public async Task Get_returns_null_when_session_has_no_manual_correction()
    {
        var context =
            await CreateContextAsync();

        await context.SessionStore.UpsertAsync(
            EndedSession(),
            CancellationToken.None);

        Assert.Null(
            await context.CorrectionStore.GetAsync(
                SessionId,
                CancellationToken.None));
    }

    private async Task<StoreContext>
        CreateContextAsync()
    {
        Directory.CreateDirectory(_root);

        var databasePath =
            Path.Combine(
                _root,
                $"{Guid.NewGuid():N}.db");

        var options =
            new DatabaseOptions(
                databasePath,
                Path.Combine(
                    _root,
                    "Backups"));

        await new DatabaseInitializer(options)
            .InitializeAsync(
                CancellationToken.None);

        await InsertGameAsync(
            databasePath);

        return new StoreContext(
            new SqliteSessionStore(options),
            new SqliteSessionCorrectionStore(options));
    }

    private static async Task InsertGameAsync(
        string databasePath)
    {
        await using var connection =
            new SqliteConnection(
                $"Data Source={databasePath};Pooling=False");

        await connection.OpenAsync();

        var command =
            connection.CreateCommand();

        command.CommandText = """
            INSERT INTO games(
                game_id,
                title,
                is_hidden,
                created_utc,
                updated_utc)
            VALUES(
                $gameId,
                'Correction Test Game',
                0,
                $createdUtc,
                $updatedUtc);
            """;

        command.Parameters.AddWithValue(
            "$gameId",
            GameId.ToString());

        command.Parameters.AddWithValue(
            "$createdUtc",
            T0.ToString("O"));

        command.Parameters.AddWithValue(
            "$updatedUtc",
            T0.ToString("O"));

        await command.ExecuteNonQueryAsync();
    }

    private static GameSession EndedSession()
        => new(
            SessionId,
            GameId,
            T0,
            T0.AddMinutes(60),
            T0.AddMinutes(60),
            SessionState.Ended,
            SessionEndReason.ProcessExited,
            SessionDetectionSource.ProcessMonitor,
            T0,
            T0.AddMinutes(60));

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
        ISessionStore SessionStore,
        ISessionCorrectionStore CorrectionStore);
}
