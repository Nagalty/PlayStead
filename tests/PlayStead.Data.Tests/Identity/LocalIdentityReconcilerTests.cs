using Microsoft.Data.Sqlite;
using PlayStead.Core.Catalog;
using PlayStead.Core.Identity;
using PlayStead.Core.Library;
using PlayStead.Data.Database;
using PlayStead.Data.Identity;

namespace PlayStead.Data.Tests.Identity;

public sealed class LocalIdentityReconcilerTests : IDisposable
{
    private static readonly DateTimeOffset ObservedAt =
        DateTimeOffset.Parse("2026-09-16T18:00:00Z");

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "PlayStead.Tests",
        "Reconciler",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Reconcile_updates_canonical_link_and_preserves_game_installation_and_session()
    {
        var fixture = await CreateFixtureAsync("direct.db");
        var target = CatalogContentId.New();
        var before = await ReadProtectedRowsAsync(
            fixture.Options.DatabasePath,
            fixture.GameId,
            fixture.InstallationId,
            fixture.SessionId);

        await new SqliteLocalIdentityReconciler(fixture.Options)
            .ReconcileAsync(
                fixture.GameId,
                target,
                ExactEvidence(target),
                ObservedAt,
                CancellationToken.None);

        var after = await ReadProtectedRowsAsync(
            fixture.Options.DatabasePath,
            fixture.GameId,
            fixture.InstallationId,
            fixture.SessionId);
        var resolution = await ReadResolutionAsync(
            fixture.Options.DatabasePath,
            fixture.GameId);

        Assert.Equal(before.GameId, after.GameId);
        Assert.Equal(before.InstallationId, after.InstallationId);
        Assert.Equal(before.SessionId, after.SessionId);
        Assert.Equal(target.ToString(), after.CanonicalContentId);
        Assert.Equal(IdentityResolutionState.MatchConfirmed, resolution.State);
        Assert.Null(resolution.ProvisionalId);
        Assert.Equal(target.ToString(), resolution.CandidateContentId);
    }

    [Fact]
    public async Task Reconcile_preserves_existing_provisional_id_and_created_timestamp()
    {
        var fixture = await CreateFixtureAsync("existing-temp.db");
        var store = new SqliteIdentityResolutionStore(fixture.Options);
        var provisional = await store.GetOrCreateProvisionalAsync(
            fixture.GameId,
            NoMatchEvidence(),
            ObservedAt,
            CancellationToken.None);
        var target = CatalogContentId.New();

        await new SqliteLocalIdentityReconciler(fixture.Options)
            .ReconcileAsync(
                fixture.GameId,
                target,
                ExactEvidence(target),
                ObservedAt.AddHours(1),
                CancellationToken.None);

        var resolution = await ReadResolutionAsync(
            fixture.Options.DatabasePath,
            fixture.GameId);

        Assert.Equal(
            provisional.ProvisionalIdentityId?.ToString(),
            resolution.ProvisionalId);
        Assert.Equal(
            ObservedAt.ToString("O"),
            resolution.CreatedUtc);
        Assert.Equal(IdentityResolutionState.MatchConfirmed, resolution.State);
    }

    [Fact]
    public async Task Reconcile_is_idempotent_for_the_same_target()
    {
        var fixture = await CreateFixtureAsync("idempotent.db");
        var target = CatalogContentId.New();
        var reconciler = new SqliteLocalIdentityReconciler(fixture.Options);

        await reconciler.ReconcileAsync(
            fixture.GameId,
            target,
            ExactEvidence(target),
            ObservedAt,
            CancellationToken.None);
        var first = await ReadResolutionAsync(
            fixture.Options.DatabasePath,
            fixture.GameId);

        await reconciler.ReconcileAsync(
            fixture.GameId,
            target,
            ExactEvidence(target),
            ObservedAt.AddHours(1),
            CancellationToken.None);
        var second = await ReadResolutionAsync(
            fixture.Options.DatabasePath,
            fixture.GameId);

        Assert.Equal(first.ProvisionalId, second.ProvisionalId);
        Assert.Equal(first.State, second.State);
        Assert.Equal(first.CandidateContentId, second.CandidateContentId);
        Assert.Equal(first.CreatedUtc, second.CreatedUtc);
    }

    [Fact]
    public async Task Different_local_games_can_reconcile_to_the_same_catalog_content()
    {
        var first = await CreateFixtureAsync("shared-one.db", Game(1));
        var second = await CreateFixtureAsync("shared-two.db", Game(2));
        var target = CatalogContentId.New();
        var reconcilerOne = new SqliteLocalIdentityReconciler(first.Options);
        var reconcilerTwo = new SqliteLocalIdentityReconciler(second.Options);

        await reconcilerOne.ReconcileAsync(
            first.GameId, target, ExactEvidence(target), ObservedAt,
            CancellationToken.None);
        await reconcilerTwo.ReconcileAsync(
            second.GameId, target, ExactEvidence(target), ObservedAt,
            CancellationToken.None);

        Assert.Equal(
            target.ToString(),
            (await ReadProtectedRowsAsync(
                first.Options.DatabasePath,
                first.GameId,
                first.InstallationId,
                first.SessionId)).CanonicalContentId);
        Assert.Equal(
            target.ToString(),
            (await ReadProtectedRowsAsync(
                second.Options.DatabasePath,
                second.GameId,
                second.InstallationId,
                second.SessionId)).CanonicalContentId);
    }

    [Fact]
    public async Task Reconcile_of_unknown_game_rolls_back_without_writing_resolution()
    {
        var fixture = await CreateFixtureAsync("missing.db");
        var missing = Game(99);
        var target = CatalogContentId.New();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new SqliteLocalIdentityReconciler(fixture.Options)
                .ReconcileAsync(
                    missing,
                    target,
                    ExactEvidence(target),
                    ObservedAt,
                    CancellationToken.None));

        Assert.Null(await ReadResolutionOrNullAsync(
            fixture.Options.DatabasePath,
            missing));
        Assert.Null(await ReadCanonicalAsync(
            fixture.Options.DatabasePath,
            fixture.GameId));
    }

    [Fact]
    public async Task Cancelled_reconcile_propagates_without_mutation()
    {
        var fixture = await CreateFixtureAsync("cancelled.db");
        var target = CatalogContentId.New();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new SqliteLocalIdentityReconciler(fixture.Options)
                .ReconcileAsync(
                    fixture.GameId,
                    target,
                    ExactEvidence(target),
                    ObservedAt,
                    cancellation.Token));

        Assert.Null(await ReadCanonicalAsync(
            fixture.Options.DatabasePath,
            fixture.GameId));
        Assert.Null(await ReadResolutionOrNullAsync(
            fixture.Options.DatabasePath,
            fixture.GameId));
    }

    private async Task<Fixture> CreateFixtureAsync(
        string fileName,
        GameId? gameId = null)
    {
        Directory.CreateDirectory(_root);
        var options = new DatabaseOptions(
            Path.Combine(_root, fileName),
            Path.Combine(_root, "Backups"));
        await new DatabaseInitializer(options)
            .InitializeAsync(CancellationToken.None);

        var actualGameId = gameId ?? Game(1);
        var installationId = InstallationId.New();
        var sessionId = Guid.NewGuid().ToString("D");
        await using var connection = new SqliteConnection(
            $"Data Source={options.DatabasePath};Pooling=False;Foreign Keys=True");
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO games(game_id,title,is_hidden,created_utc,updated_utc)
            VALUES($gameId,'Fixture Game',0,$utc,$utc);
            INSERT INTO installations(
                installation_id,game_id,provider,external_id,install_path,
                installed_size_bytes,is_preferred,is_present,last_seen_utc)
            VALUES($installationId,$gameId,1,'1874880','C:/fixture',NULL,1,1,$utc);
            INSERT INTO game_sessions(
                session_id,game_id,observed_started_at_utc,last_seen_at_utc,
                observed_ended_at_utc,state,end_reason,detection_source,
                created_at_utc,updated_at_utc)
            VALUES($sessionId,$gameId,$utc,$utc,NULL,0,NULL,0,$utc,$utc);
            """;
        command.Parameters.AddWithValue("$gameId", actualGameId.ToString());
        command.Parameters.AddWithValue("$installationId", installationId.ToString());
        command.Parameters.AddWithValue("$sessionId", sessionId);
        command.Parameters.AddWithValue("$utc", ObservedAt.ToString("O"));
        await command.ExecuteNonQueryAsync();

        return new Fixture(options, actualGameId, installationId, sessionId);
    }

    private static IdentityResolutionEvidence NoMatchEvidence() =>
        new(
            IdentityResolutionEvidenceKind.NoExactProviderRefMatch,
            CatalogProviderKind.Steam,
            "1874880",
            null);

    private static IdentityResolutionEvidence ExactEvidence(
        CatalogContentId target) =>
        new(
            IdentityResolutionEvidenceKind.ExactProviderRef,
            CatalogProviderKind.Steam,
            "1874880",
            target);

    private static async Task<ProtectedRows> ReadProtectedRowsAsync(
        string databasePath,
        GameId gameId,
        InstallationId installationId,
        string sessionId)
    {
        await using var connection = await OpenAsync(databasePath);
        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                g.game_id,
                g.canonical_content_id,
                i.installation_id,
                s.session_id
            FROM games g
            JOIN installations i ON i.game_id = g.game_id
            JOIN game_sessions s ON s.game_id = g.game_id
            WHERE g.game_id=$gameId
              AND i.installation_id=$installationId
              AND s.session_id=$sessionId;
            """;
        command.Parameters.AddWithValue("$gameId", gameId.ToString());
        command.Parameters.AddWithValue("$installationId", installationId.ToString());
        command.Parameters.AddWithValue("$sessionId", sessionId);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return new ProtectedRows(
            reader.GetString(0),
            reader.IsDBNull(1) ? null : reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3));
    }

    private static async Task<ResolutionRow> ReadResolutionAsync(
        string databasePath,
        GameId gameId)
    {
        var row = await ReadResolutionOrNullAsync(databasePath, gameId);
        return row ?? throw new InvalidOperationException("Resolution row missing.");
    }

    private static async Task<ResolutionRow?> ReadResolutionOrNullAsync(
        string databasePath,
        GameId gameId)
    {
        await using var connection = await OpenAsync(databasePath);
        var command = connection.CreateCommand();
        command.CommandText = "SELECT provisional_id,state,candidate_content_id,created_utc FROM game_identity_resolutions WHERE game_id=$gameId;";
        command.Parameters.AddWithValue("$gameId", gameId.ToString());
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;
        return new ResolutionRow(
            reader.IsDBNull(0) ? null : reader.GetString(0),
            (IdentityResolutionState)reader.GetInt32(1),
            reader.IsDBNull(2) ? null : reader.GetString(2),
            reader.GetString(3));
    }

    private static async Task<string?> ReadCanonicalAsync(
        string databasePath,
        GameId gameId)
    {
        await using var connection = await OpenAsync(databasePath);
        var command = connection.CreateCommand();
        command.CommandText = "SELECT canonical_content_id FROM games WHERE game_id=$gameId;";
        command.Parameters.AddWithValue("$gameId", gameId.ToString());
        var value = await command.ExecuteScalarAsync();
        return value is null || value is DBNull ? null : Convert.ToString(value);
    }

    private static async Task<SqliteConnection> OpenAsync(string databasePath)
    {
        var connection = new SqliteConnection(
            $"Data Source={databasePath};Mode=ReadOnly;Pooling=False;Foreign Keys=True");
        await connection.OpenAsync();
        return connection;
    }

    private static GameId Game(int index) =>
        new(Guid.Parse($"00000000-0000-4000-8000-{index:000000000000}"));

    private sealed record Fixture(
        DatabaseOptions Options,
        GameId GameId,
        InstallationId InstallationId,
        string SessionId);

    private sealed record ProtectedRows(
        string GameId,
        string? CanonicalContentId,
        string InstallationId,
        string SessionId);

    private sealed record ResolutionRow(
        string? ProvisionalId,
        IdentityResolutionState State,
        string? CandidateContentId,
        string CreatedUtc);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
