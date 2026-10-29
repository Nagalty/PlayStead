using Microsoft.Data.Sqlite;
using PlayStead.Core.Library;
using PlayStead.Core.LocalEnvironment;
using PlayStead.Data.Database;
using PlayStead.Data.LocalEnvironment;

namespace PlayStead.Data.Tests.LocalEnvironment;

public sealed class SqliteBeforeUpdateProtectionPolicyStoreTests
{
    [Fact]
    public async Task Missing_policy_defaults_to_Ask_and_round_trips_per_installation()
    {
        var path = Path.Combine(Path.GetTempPath(), $"playstead-policy-{Guid.NewGuid():N}.db");
        try
        {
            var options = new DatabaseOptions(path, Path.GetDirectoryName(path)!);
            await new DatabaseInitializer(options).InitializeAsync(CancellationToken.None);
            var store = new SqliteBeforeUpdateProtectionPolicyStore(options);
            var first = InstallationId.New(); var second = InstallationId.New();
            var game = GameId.New();
            await using (var connection = new SqliteConnection($"Data Source={path};Foreign Keys=True"))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = "INSERT INTO games(game_id,title,created_utc,updated_utc) VALUES($g,'Game',$u,$u); INSERT INTO installations(installation_id,game_id,provider,external_id,install_path,last_seen_utc) VALUES($i1,$g,0,'1','C:/Game',$u),($i2,$g,0,'2','C:/Game2',$u);";
                command.Parameters.AddWithValue("$g", game.Value.ToString("D")); command.Parameters.AddWithValue("$i1", first.Value.ToString("D")); command.Parameters.AddWithValue("$i2", second.Value.ToString("D")); command.Parameters.AddWithValue("$u", DateTimeOffset.UtcNow.ToString("O"));
                await command.ExecuteNonQueryAsync();
            }
            Assert.Equal(BeforeUpdateProtectionPolicy.Ask, await store.GetAsync(first, CancellationToken.None));
            await store.SetAsync(first, BeforeUpdateProtectionPolicy.Always, CancellationToken.None);
            await store.SetAsync(second, BeforeUpdateProtectionPolicy.Never, CancellationToken.None);
            Assert.Equal(BeforeUpdateProtectionPolicy.Always, await store.GetAsync(first, CancellationToken.None));
            Assert.Equal(BeforeUpdateProtectionPolicy.Never, await store.GetAsync(second, CancellationToken.None));
        }
        finally { SqliteConnection.ClearAllPools(); if (File.Exists(path)) File.Delete(path); }
    }
}
