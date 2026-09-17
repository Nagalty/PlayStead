using Microsoft.Data.Sqlite;
using PlayStead.Core.Catalog;
using PlayStead.Core.Identity;
using PlayStead.Core.Library;
using PlayStead.Data.Database;
using PlayStead.Data.Identity;

namespace PlayStead.Data.Tests.Identity;

public sealed class IdentityDecisionServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "PlayStead.Tests", "DecisionService", Guid.NewGuid().ToString("N"));
    private readonly GameId _game = GameId.New();
    private readonly DatabaseOptions _options;

    public IdentityDecisionServiceTests() { Directory.CreateDirectory(_root); _options = new DatabaseOptions(Path.Combine(_root, "playstead.db"), Path.Combine(_root, "Backups")); }

    [Fact]
    public async Task Confirm_sets_canonical_and_replaces_previous_confirmation()
    {
        await SetupAsync(); var service = new SqliteIdentityDecisionService(_options); var first = CatalogContentId.New(); var second = CatalogContentId.New(); var t = DateTimeOffset.UtcNow;
        await service.ConfirmAsync(_game, first, t, CancellationToken.None);
        await service.ConfirmAsync(_game, second, t.AddMinutes(1), CancellationToken.None);
        await using var c = await OpenAsync();
        Assert.Equal(second.ToString(), await ScalarAsync(c, "SELECT canonical_content_id FROM games WHERE game_id=$g;", ("$g", _game.ToString())));
        Assert.Equal(1L, await ScalarAsync(c, "SELECT COUNT(*) FROM game_identity_decisions WHERE game_id=$g AND decision_type=1 AND revoked_utc IS NULL;", ("$g", _game.ToString())));
        Assert.Equal(1L, await ScalarAsync(c, "SELECT COUNT(*) FROM game_identity_decisions WHERE game_id=$g AND revoked_utc IS NOT NULL;", ("$g", _game.ToString())));
    }

    [Fact]
    public async Task Reject_confirmed_candidate_clears_canonical_and_is_idempotent()
    {
        await SetupAsync(); var service = new SqliteIdentityDecisionService(_options); var candidate = CatalogContentId.New(); var t = DateTimeOffset.UtcNow;
        await service.ConfirmAsync(_game, candidate, t, CancellationToken.None); await service.RejectAsync(_game, candidate, t.AddMinutes(1), CancellationToken.None); await service.RejectAsync(_game, candidate, t.AddMinutes(2), CancellationToken.None);
        await using var c = await OpenAsync(); Assert.True(await ScalarAsync(c, "SELECT canonical_content_id FROM games WHERE game_id=$g;", ("$g", _game.ToString())) is DBNull); Assert.Equal(1L, await ScalarAsync(c, "SELECT COUNT(*) FROM game_identity_decisions WHERE game_id=$g AND decision_type=2 AND revoked_utc IS NULL;", ("$g", _game.ToString())));
    }

    [Fact]
    public async Task Cancelled_token_does_not_mutate()
    {
        await SetupAsync(); using var source = new CancellationTokenSource(); source.Cancel(); var service = new SqliteIdentityDecisionService(_options);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ConfirmAsync(_game, CatalogContentId.New(), DateTimeOffset.UtcNow, source.Token));
    }

    private async Task SetupAsync() { await new DatabaseInitializer(_options).InitializeAsync(CancellationToken.None); await using var c = await OpenAsync(); using var cmd = c.CreateCommand(); cmd.CommandText = "INSERT INTO games(game_id,title,is_hidden,created_utc,updated_utc) VALUES($g,'G',0,$u,$u);"; cmd.Parameters.AddWithValue("$g", _game.ToString()); cmd.Parameters.AddWithValue("$u", DateTimeOffset.UtcNow.ToString("O")); await cmd.ExecuteNonQueryAsync(); }
    private async Task<SqliteConnection> OpenAsync() { var c = new SqliteConnection($"Data Source={_options.DatabasePath};Pooling=False;Foreign Keys=True"); await c.OpenAsync(); return c; }
    private static async Task<object?> ScalarAsync(SqliteConnection c, string sql, (string, object) parameter) { using var cmd = c.CreateCommand(); cmd.CommandText = sql; cmd.Parameters.AddWithValue(parameter.Item1, parameter.Item2); return await cmd.ExecuteScalarAsync(); }
    public void Dispose() { SqliteConnection.ClearAllPools(); if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
