using System.Globalization;
using Microsoft.Data.Sqlite;
using PlayStead.Core.Library;
using PlayStead.Core.Persistence;
using PlayStead.Data.Database;

namespace PlayStead.Data.Library;

public sealed class SqliteLibraryGameLookup : ILibraryGameLookup
{
    private readonly DatabaseOptions _options;

    public SqliteLibraryGameLookup(DatabaseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    public async Task<GameId?> FindGameIdByProviderRefAsync(
        ProviderKind provider,
        string externalId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await using var connection = new SqliteConnection(
            $"Data Source={_options.DatabasePath};Mode=ReadOnly;Pooling=False");
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT game_id
            FROM provider_game_refs
            WHERE provider = $provider
              AND external_id = $externalId
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$provider", (int)provider);
        command.Parameters.AddWithValue("$externalId", externalId);

        var value = Convert.ToString(
            await command.ExecuteScalarAsync(cancellationToken),
            CultureInfo.InvariantCulture);

        return string.IsNullOrWhiteSpace(value)
            ? null
            : new GameId(Guid.Parse(value));
    }
}
