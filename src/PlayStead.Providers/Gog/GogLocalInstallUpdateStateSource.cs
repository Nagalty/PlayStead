using System.Text.Json;
using Microsoft.Data.Sqlite;
using PlayStead.Core.Library;
using PlayStead.Core.ProviderInstallUpdate;

namespace PlayStead.Providers.Gog;

/// <summary>Reads product-scoped GOG Galaxy install/update state without writing to Galaxy data.</summary>
public sealed class GogLocalInstallUpdateStateSource : IProviderInstallUpdateStateSource
{
    private readonly string _galaxyDatabasePath;
    private readonly string _jobsDatabasePath;
    private readonly TimeProvider _timeProvider;

    public GogLocalInstallUpdateStateSource(
        string? galaxyDatabasePath = null,
        string? jobsDatabasePath = null,
        TimeProvider? timeProvider = null)
    {
        var storage = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "GOG.com", "Galaxy", "storage");
        _galaxyDatabasePath = galaxyDatabasePath ?? Path.Combine(storage, "galaxy-2.0.db");
        _jobsDatabasePath = jobsDatabasePath ?? Path.Combine(storage, "jobs.db");
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public ProviderKind Provider => ProviderKind.Gog;

    public Task<IReadOnlyList<ProviderInstallUpdateState>> GetAsync(
        IReadOnlyCollection<GameInstallation> installations,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(installations);
        var observedAt = _timeProvider.GetUtcNow();
        var result = installations
            .Where(value => value.Provider == ProviderKind.Gog && value.IsPresent)
            .Select(value => ReadSafely(value, observedAt, cancellationToken))
            .OrderBy(value => value.ProviderGameId, StringComparer.Ordinal)
            .ToArray();
        return Task.FromResult<IReadOnlyList<ProviderInstallUpdateState>>(result);
    }

    private ProviderInstallUpdateState ReadSafely(
        GameInstallation installation,
        DateTimeOffset observedAt,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var localBuild = ReadLocalBuild(installation);
            var availableBuild = ReadAvailableBuild(installation.ExternalId);
            var operation = ReadOperation(installation.ExternalId);
            var job = ReadJob(installation.ExternalId);

            var active = operation.GetValueOrDefault() != 0 || job is not null;
            var status = active
                ? ProviderInstallUpdateStatus.Downloading
                : operation.HasValue && BuildsMatch(localBuild, availableBuild)
                    ? ProviderInstallUpdateStatus.UpToDate
                    : operation.HasValue && HasComparableBuilds(localBuild, availableBuild)
                        ? ProviderInstallUpdateStatus.UpdateAvailable
                        : ProviderInstallUpdateStatus.Unknown;

            return new ProviderInstallUpdateState(
                installation.GameId,
                Provider,
                installation.ExternalId,
                localBuild,
                availableBuild,
                status,
                job?.DownloadSize,
                job?.DownloadProgress,
                job?.PatchingSize,
                job?.PatchingProgress,
                job?.VerificationSize,
                operation ?? 0,
                observedAt);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or SqliteException or InvalidOperationException or FormatException)
        {
            return Unknown(installation, observedAt);
        }
    }

    private string? ReadLocalBuild(GameInstallation installation)
    {
        var path = Path.Combine(installation.InstallPath, $"goggame-{installation.ExternalId}.info");
        if (!File.Exists(path))
            return null;
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return GetString(document.RootElement, "buildId");
    }

    private string? ReadAvailableBuild(string productId)
    {
        if (!File.Exists(_galaxyDatabasePath))
            return null;
        using var connection = OpenReadOnly(_galaxyDatabasePath);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT manifest FROM Builds WHERE productId = $productId ORDER BY createdAt DESC LIMIT 1";
        command.Parameters.AddWithValue("$productId", productId);
        var value = command.ExecuteScalar() as string;
        if (string.IsNullOrWhiteSpace(value))
            return null;
        using var document = JsonDocument.Parse(value);
        if (!document.RootElement.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
            return null;

        var candidates = items.EnumerateArray()
            .Where(item => string.Equals(GetString(item, "os"), "windows", StringComparison.OrdinalIgnoreCase))
            .Where(item => string.Equals(GetString(item, "branch"), null, StringComparison.Ordinal))
            .Select(item => new
            {
                BuildId = GetString(item, "build_id"),
                Published = DateTimeOffset.TryParse(GetString(item, "date_published"), out var date) ? date : DateTimeOffset.MinValue,
                Public = !item.TryGetProperty("public", out var isPublic) || isPublic.ValueKind != JsonValueKind.False || isPublic.GetBoolean()
            })
            .Where(item => item.Public && !string.IsNullOrWhiteSpace(item.BuildId))
            .OrderByDescending(item => item.Published)
            .FirstOrDefault();
        return candidates?.BuildId;
    }

    private int? ReadOperation(string productId)
    {
        if (!File.Exists(_galaxyDatabasePath))
            return 0;
        using var connection = OpenReadOnly(_galaxyDatabasePath);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT operation FROM ProductStates WHERE productId = $productId LIMIT 1";
        command.Parameters.AddWithValue("$productId", productId);
        var value = command.ExecuteScalar();
        return value is null or DBNull ? null : checked(Convert.ToInt32(value));
    }

    private JobState? ReadJob(string productId)
    {
        if (!File.Exists(_jobsDatabasePath))
            return null;
        using var connection = OpenReadOnly(_jobsDatabasePath);
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT downloadSize, downloadProgress, patchingSize, patchingProgress,
                   verificationSize, verificationProgress
            FROM UpdateProductJobs AS updateJob
            INNER JOIN Jobs AS job ON job.jobId = updateJob.jobId
            WHERE updateJob.productId = $productId LIMIT 1
            """;
        command.Parameters.AddWithValue("$productId", productId);
        using var reader = command.ExecuteReader();
        return !reader.Read()
            ? null
            : new JobState(
                ReadNullableLong(reader, 0), ReadNullableLong(reader, 1),
                ReadNullableLong(reader, 2), ReadNullableLong(reader, 3),
                ReadNullableLong(reader, 4), ReadNullableLong(reader, 5));
    }

    private static SqliteConnection OpenReadOnly(string path)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Private,
            Pooling = false
        }.ToString());
        connection.Open();
        return connection;
    }

    private static ProviderInstallUpdateState Unknown(GameInstallation installation, DateTimeOffset observedAt) =>
        new(installation.GameId, ProviderKind.Gog, installation.ExternalId, null, null,
            ProviderInstallUpdateStatus.Unknown, null, null, null, null, null, null, observedAt);

    private static bool BuildsMatch(string? local, string? available) =>
        !string.IsNullOrWhiteSpace(local) && string.Equals(local, available, StringComparison.Ordinal);

    private static bool HasComparableBuilds(string? local, string? available) =>
        !string.IsNullOrWhiteSpace(local) && !string.IsNullOrWhiteSpace(available);

    private static string? GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static long? ReadNullableLong(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : Convert.ToInt64(reader.GetValue(ordinal));

    private sealed record JobState(
        long? DownloadSize,
        long? DownloadProgress,
        long? PatchingSize,
        long? PatchingProgress,
        long? VerificationSize,
        long? VerificationProgress);
}
