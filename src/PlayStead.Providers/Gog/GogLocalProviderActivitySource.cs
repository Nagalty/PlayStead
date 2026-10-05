using System.Globalization;
using Microsoft.Data.Sqlite;
using PlayStead.Core.Library;
using PlayStead.Core.ProviderActivity;

namespace PlayStead.Providers.Gog;

/// <summary>Reads lifetime playtime from the local Galaxy database.</summary>
public sealed class GogLocalProviderActivitySource : IProviderActivityMetadataSource
{
    private static readonly string DefaultDatabasePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "GOG.com", "Galaxy", "storage", "galaxy-2.0.db");

    private readonly string _databasePath;

    public GogLocalProviderActivitySource(string? databasePath = null) =>
        _databasePath = string.IsNullOrWhiteSpace(databasePath) ? DefaultDatabasePath : databasePath;

    public ProviderKind Provider => ProviderKind.Gog;

    public async Task<IReadOnlyList<ProviderActivityMetadata>> GetAsync(
        IReadOnlyCollection<GameInstallation> installations,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(installations);
        var gogInstallations = installations
            .Where(x => x.Provider == ProviderKind.Gog && x.IsPresent)
            .ToArray();
        if (gogInstallations.Length == 0 || !File.Exists(_databasePath))
            return Array.Empty<ProviderActivityMetadata>();

        return await Task.Run(() => Read(gogInstallations, cancellationToken), cancellationToken);
    }

    private IReadOnlyList<ProviderActivityMetadata> Read(
        IReadOnlyCollection<GameInstallation> installations,
        CancellationToken cancellationToken)
    {
        var refreshedAt = DateTimeOffset.UtcNow;
        var result = new List<ProviderActivityMetadata>(installations.Count);
        try
        {
            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = _databasePath,
                Mode = SqliteOpenMode.ReadOnly,
                Cache = SqliteCacheMode.Private,
                Pooling = false
            };
            using var connection = new SqliteConnection(builder.ConnectionString);
            connection.Open();
            foreach (var installation in installations)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!long.TryParse(installation.ExternalId, NumberStyles.None, CultureInfo.InvariantCulture, out var productId) ||
                    productId < 0)
                {
                    result.Add(ProviderActivityMetadata.Unknown(
                        installation.GameId, Provider, installation.ExternalId, refreshedAt));
                    continue;
                }

                var minutes = ReadProductMinutes(connection, productId, cancellationToken);
                var totalPlaytime = TryCreateDuration(minutes);
                result.Add(new ProviderActivityMetadata(
                    installation.GameId,
                    Provider,
                    installation.ExternalId,
                    totalPlaytime,
                    null,
                    refreshedAt,
                    totalPlaytime is not null
                        ? ProviderActivityAvailability.Complete
                        : ProviderActivityAvailability.Unknown));
            }
        }
        catch (Exception exception) when (
            exception is SqliteException or IOException or UnauthorizedAccessException)
        {
            return Array.Empty<ProviderActivityMetadata>();
        }

        return result;
    }

    private static long? ReadProductMinutes(
        SqliteConnection connection,
        long productId,
        CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT gt.minutesInGame
              FROM ProductsToReleaseKeys pr
              LEFT JOIN GameTimes gt ON gt.releaseKey = pr.releaseKey
             WHERE pr.gogId = $productId
             ORDER BY gt.minutesInGame DESC
             LIMIT 1;
            """;
        command.Parameters.AddWithValue("$productId", productId);
        using var reader = command.ExecuteReader();
        if (!reader.Read() || reader.IsDBNull(0))
            return null;

        var raw = reader.GetValue(0);
        var minutes = raw switch
        {
            long value => value,
            int value => value,
            _ => long.MinValue
        };
        cancellationToken.ThrowIfCancellationRequested();
        return minutes >= 0 ? minutes : null;
    }

    private static TimeSpan? TryCreateDuration(long? minutes)
    {
        if (minutes is not { } value || value < 0)
            return null;
        try
        {
            return TimeSpan.FromMinutes(value);
        }
        catch (OverflowException)
        {
            return null;
        }
    }
}
