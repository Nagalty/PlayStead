using Microsoft.Data.Sqlite;
using PlayStead.Core.Sessions;
using PlayStead.Data.Database;
using PlayStead.Data.Sessions;

namespace PlayStead.Data.Tests.Sessions;

public sealed class SqliteProcessSignatureStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "PlayStead.Tests",
        Guid.NewGuid().ToString("N"));

    private static readonly Guid GameId =
        Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private static readonly DateTimeOffset T0 =
        new(2026, 9, 12, 21, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Upsert_round_trips_origin_timestamp_and_ordered_entries()
    {
        var store = await CreateStoreAsync();

        var expected = new ProcessSignature(
            GameId,
            new ProcessSignatureEntry[]
            {
                new(
                    "Game.exe",
                    ProcessSignatureEntryKind.Main),
                new(
                    "Launcher.exe",
                    ProcessSignatureEntryKind.Auxiliary),
                new(
                    "Server.exe",
                    ProcessSignatureEntryKind.Excluded)
            },
            ProcessSignatureOrigin.Manual,
            T0);

        await store.UpsertAsync(
            expected,
            CancellationToken.None);

        var actual = await store.GetAsync(
            GameId,
            CancellationToken.None);

        Assert.NotNull(actual);
        Assert.Equal(expected.GameId, actual!.GameId);
        Assert.Equal(expected.Origin, actual.Origin);
        Assert.Equal(expected.UpdatedAtUtc, actual.UpdatedAtUtc);
        Assert.Equal(
            expected.Entries.ToArray(),
            actual.Entries.ToArray());
    }

    [Fact]
    public async Task Upsert_replaces_previous_entry_set_without_leaving_stale_rows()
    {
        var store = await CreateStoreAsync();

        await store.UpsertAsync(
            new ProcessSignature(
                GameId,
                new ProcessSignatureEntry[]
                {
                    new(
                        "Game.exe",
                        ProcessSignatureEntryKind.Main),
                    new(
                        "Launcher.exe",
                        ProcessSignatureEntryKind.Auxiliary)
                },
                ProcessSignatureOrigin.Discovered,
                T0),
            CancellationToken.None);

        var replacement = new ProcessSignature(
            GameId,
            new ProcessSignatureEntry[]
            {
                new(
                    "Game-Win64-Shipping.exe",
                    ProcessSignatureEntryKind.Main)
            },
            ProcessSignatureOrigin.Manual,
            T0.AddMinutes(5));

        await store.UpsertAsync(
            replacement,
            CancellationToken.None);

        var actual = await store.GetAsync(
            GameId,
            CancellationToken.None);

        Assert.NotNull(actual);
        Assert.Equal(
            replacement.GameId,
            actual!.GameId);
        Assert.Equal(
            replacement.Origin,
            actual.Origin);
        Assert.Equal(
            replacement.UpdatedAtUtc,
            actual.UpdatedAtUtc);
        Assert.Equal(
            replacement.Entries.ToArray(),
            actual.Entries.ToArray());
    }

    [Fact]
    public async Task GetAll_returns_each_persisted_signature_once()
    {
        var secondGameId =
            Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

        var context = await CreateStoreContextAsync(
            GameId,
            secondGameId);

        await context.Store.UpsertAsync(
            Signature(
                GameId,
                "First.exe"),
            CancellationToken.None);

        await context.Store.UpsertAsync(
            Signature(
                secondGameId,
                "Second.exe"),
            CancellationToken.None);

        var all = await context.Store.GetAllAsync(
            CancellationToken.None);

        Assert.Equal(2, all.Count);
        Assert.Equal(
            new[] { GameId, secondGameId }.OrderBy(x => x),
            all.Select(x => x.GameId).OrderBy(x => x));
    }

    private async Task<IProcessSignatureStore> CreateStoreAsync()
    {
        var context = await CreateStoreContextAsync(GameId);
        return context.Store;
    }

    private async Task<StoreContext> CreateStoreContextAsync(
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

        IProcessSignatureStore store =
            new SqliteProcessSignatureStore(options);

        return new StoreContext(
            store,
            databasePath);
    }

    private static ProcessSignature Signature(
        Guid gameId,
        string executableName)
        => new(
            gameId,
            new ProcessSignatureEntry[]
            {
                new(
                    executableName,
                    ProcessSignatureEntryKind.Main)
            },
            ProcessSignatureOrigin.Manual,
            T0);

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
        IProcessSignatureStore Store,
        string DatabasePath);
}
