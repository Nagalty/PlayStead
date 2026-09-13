using System.Reflection;
using Microsoft.Data.Sqlite;
using PlayStead.Core.Sessions;
using PlayStead.Data.Database;
using PlayStead.Data.Sessions;

namespace PlayStead.Data.Tests.Sessions;

public sealed class Task08Fix01CorrectionPersistenceTests : IDisposable
{
    private static readonly Guid GameId =
        Guid.Parse("85858585-8585-4585-8585-858585858585");

    private static readonly Guid SessionId =
        Guid.Parse("86868686-8686-4686-8686-868686868686");

    private static readonly DateTimeOffset T0 =
        new(2026, 9, 13, 8, 0, 0, TimeSpan.Zero);

    private readonly string _root =
        Path.Combine(
            Path.GetTempPath(),
            "PlayStead.Task08Fix01",
            Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Fresh_schema_contains_traceable_correction_columns()
    {
        var context = await CreateContextAsync();

        await using var connection =
            new SqliteConnection(
                $"Data Source={context.DatabasePath};Pooling=False");

        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info(session_corrections);";

        var columns =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(1));
        }

        Assert.Contains("correction_id", columns);
        Assert.Contains("reason", columns);
        Assert.Contains("created_at_utc", columns);
    }

    [Fact]
    public async Task Roundtrip_preserves_traceability_and_never_rewrites_observed_session()
    {
        var context = await CreateContextAsync();
        var observed = EndedSession();

        await context.SessionStore.UpsertAsync(
            observed,
            CancellationToken.None);

        var correctionId =
            Guid.Parse("87878787-8787-4787-8787-878787878787");

        var createdAt = T0.AddHours(2);

        var correction = CreateCorrection(
            correctionId,
            SessionId,
            T0.AddMinutes(5),
            T0.AddMinutes(65),
            "Correction de durée",
            createdAt);

        await context.CorrectionStore.UpsertAsync(
            correction,
            CancellationToken.None);

        var actual = await context.CorrectionStore.GetAsync(
            SessionId,
            CancellationToken.None);

        Assert.NotNull(actual);
        Assert.Equal(
            correctionId,
            Read<Guid>(actual!, "CorrectionId"));
        Assert.Equal(
            "Correction de durée",
            Read<string?>(actual!, "Reason"));
        Assert.Equal(
            createdAt,
            Read<DateTimeOffset>(actual!, "CreatedAtUtc"));

        Assert.Equal(
            observed,
            await context.SessionStore.GetAsync(
                SessionId,
                CancellationToken.None));
    }

    [Fact]
    public async Task Upsert_for_same_session_replaces_traceable_correction_only()
    {
        var context = await CreateContextAsync();
        var observed = EndedSession();

        await context.SessionStore.UpsertAsync(
            observed,
            CancellationToken.None);

        var first = CreateCorrection(
            Guid.Parse("88888888-8888-4888-8888-888888888881"),
            SessionId,
            T0.AddMinutes(2),
            T0.AddMinutes(62),
            "Première correction",
            T0.AddHours(2));

        var replacementId =
            Guid.Parse("88888888-8888-4888-8888-888888888882");

        var replacement = CreateCorrection(
            replacementId,
            SessionId,
            T0.AddMinutes(3),
            T0.AddMinutes(63),
            "Correction remplacée",
            T0.AddHours(3));

        await context.CorrectionStore.UpsertAsync(
            first,
            CancellationToken.None);

        await context.CorrectionStore.UpsertAsync(
            replacement,
            CancellationToken.None);

        var actual = await context.CorrectionStore.GetAsync(
            SessionId,
            CancellationToken.None);

        Assert.NotNull(actual);
        Assert.Equal(
            replacementId,
            Read<Guid>(actual!, "CorrectionId"));
        Assert.Equal(
            "Correction remplacée",
            Read<string?>(actual!, "Reason"));
        Assert.Equal(
            T0.AddHours(3),
            Read<DateTimeOffset>(actual!, "CreatedAtUtc"));

        Assert.Equal(
            observed,
            await context.SessionStore.GetAsync(
                SessionId,
                CancellationToken.None));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Optional_reason_round_trips_without_becoming_mandatory(
        string? reason)
    {
        var context = await CreateContextAsync();
        var observed = EndedSession();

        await context.SessionStore.UpsertAsync(
            observed,
            CancellationToken.None);

        var correction = CreateCorrection(
            Guid.NewGuid(),
            SessionId,
            T0.AddMinutes(4),
            T0.AddMinutes(64),
            reason,
            T0.AddHours(2));

        await context.CorrectionStore.UpsertAsync(
            correction,
            CancellationToken.None);

        var actual = await context.CorrectionStore.GetAsync(
            SessionId,
            CancellationToken.None);

        Assert.NotNull(actual);
        Assert.Equal(
            reason,
            Read<string?>(actual!, "Reason"));
    }

    private async Task<Context> CreateContextAsync()
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

        await InsertGameAsync(databasePath);

        return new Context(
            databasePath,
            new SqliteSessionStore(options),
            new SqliteSessionCorrectionStore(options));
    }

    private static async Task InsertGameAsync(string databasePath)
    {
        await using var connection =
            new SqliteConnection(
                $"Data Source={databasePath};Pooling=False");

        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText =
            """
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

        command.Parameters.AddWithValue("$gameId", GameId.ToString());
        command.Parameters.AddWithValue("$title", "Task 8 FIX01 Test Game");
        command.Parameters.AddWithValue("$createdUtc", T0.ToString("O"));
        command.Parameters.AddWithValue("$updatedUtc", T0.ToString("O"));

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

    private static SessionCorrection CreateCorrection(
        Guid correctionId,
        Guid sessionId,
        DateTimeOffset? correctedStartedAtUtc,
        DateTimeOffset? correctedEndedAtUtc,
        string? reason,
        DateTimeOffset createdAtUtc)
    {
        var values =
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["correctionId"] = correctionId,
                ["sessionId"] = sessionId,
                ["correctedStartedAtUtc"] = correctedStartedAtUtc,
                ["correctedEndedAtUtc"] = correctedEndedAtUtc,
                ["reason"] = reason,
                ["createdAtUtc"] = createdAtUtc,
                ["correctedAtUtc"] = createdAtUtc
            };

        var constructor = typeof(SessionCorrection)
            .GetConstructors()
            .OrderByDescending(x => x.GetParameters().Length)
            .FirstOrDefault();

        Assert.NotNull(constructor);

        var arguments = constructor!
            .GetParameters()
            .Select(parameter =>
            {
                if (parameter.Name is not null &&
                    values.TryGetValue(parameter.Name, out var value))
                {
                    return value;
                }

                if (parameter.HasDefaultValue)
                {
                    return parameter.DefaultValue;
                }

                throw new Xunit.Sdk.XunitException(
                    $"No test value was supplied for SessionCorrection parameter '{parameter.Name}'.");
            })
            .ToArray();

        return Assert.IsType<SessionCorrection>(
            constructor.Invoke(arguments));
    }

    private static T Read<T>(object target, string propertyName)
    {
        var property = target.GetType().GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public);

        Assert.NotNull(property);

        var value = property!.GetValue(target);

        if (value is null)
            return default!;

        return Assert.IsType<T>(value);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();

        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private sealed record Context(
        string DatabasePath,
        ISessionStore SessionStore,
        ISessionCorrectionStore CorrectionStore);
}
