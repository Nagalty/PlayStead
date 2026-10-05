using System.Text.Json;
using Microsoft.Data.Sqlite;
using PlayStead.Core.Library;
using PlayStead.Core.ProviderInstallUpdate;
using PlayStead.Providers.Gog;

namespace PlayStead.Providers.Tests.Gog;

public sealed class GogLocalInstallUpdateStateSourceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "PlayStead-GogUpdate-" + Guid.NewGuid().ToString("N"));
    private readonly string _installRoot;
    private readonly string _galaxyDb;
    private readonly string _jobsDb;

    public GogLocalInstallUpdateStateSourceTests()
    {
        Directory.CreateDirectory(_root);
        _installRoot = Path.Combine(_root, "Witcher");
        Directory.CreateDirectory(_installRoot);
        _galaxyDb = Path.Combine(_root, "galaxy-2.0.db");
        _jobsDb = Path.Combine(_root, "jobs.db");
    }

    [Fact]
    public async Task Active_product_job_maps_to_downloading()
    {
        WriteInfo("100");
        CreateGalaxyDb("200", operation: 3);
        CreateJobsDb(active: true);

        var state = await ReadStateAsync();

        Assert.Equal(ProviderInstallUpdateStatus.Downloading, state.Status);
        Assert.Equal("100", state.InstalledBuildId);
        Assert.Equal("200", state.TargetBuildId);
        Assert.Equal(3, state.StateFlags);
    }

    [Fact]
    public async Task Build_mismatch_without_job_maps_to_update_available()
    {
        WriteInfo("100");
        CreateGalaxyDb("200", operation: 0);
        CreateJobsDb(active: false);

        Assert.Equal(ProviderInstallUpdateStatus.UpdateAvailable, (await ReadStateAsync()).Status);
    }

    [Fact]
    public async Task Matching_build_without_job_maps_to_up_to_date()
    {
        WriteInfo("200");
        CreateGalaxyDb("200", operation: 0);
        CreateJobsDb(active: false);

        Assert.Equal(ProviderInstallUpdateStatus.UpToDate, (await ReadStateAsync()).Status);
    }

    [Fact]
    public async Task Missing_databases_returns_unknown_without_throwing()
    {
        WriteInfo("100");

        Assert.Equal(ProviderInstallUpdateStatus.Unknown, (await ReadStateAsync()).Status);
    }

    [Fact]
    public async Task Missing_product_returns_unknown_without_affecting_other_products()
    {
        WriteInfo("100");
        CreateGalaxyDbForOtherProduct("other", operation: 3);
        CreateJobsDbForOtherProduct();

        Assert.Equal(ProviderInstallUpdateStatus.Unknown, (await ReadStateAsync()).Status);
    }

    [Fact]
    public async Task Null_build_ids_return_unknown_and_do_not_report_update()
    {
        WriteInfo(null);
        CreateGalaxyDb(null, operation: 0);
        CreateJobsDb(active: false);

        var state = await ReadStateAsync();
        Assert.Equal(ProviderInstallUpdateStatus.Unknown, state.Status);
        Assert.Null(state.InstalledBuildId);
        Assert.Null(state.TargetBuildId);
    }

    [Fact]
    public async Task Other_product_job_does_not_affect_target_game()
    {
        WriteInfo("200");
        CreateGalaxyDb("200", operation: 0);
        CreateJobsDbForOtherProduct();

        Assert.Equal(ProviderInstallUpdateStatus.UpToDate, (await ReadStateAsync()).Status);
    }

    private async Task<ProviderInstallUpdateState> ReadStateAsync()
    {
        var source = new GogLocalInstallUpdateStateSource(_galaxyDb, _jobsDb);
        var result = await source.GetAsync([Installation()], CancellationToken.None);
        return Assert.Single(result);
    }

    private GameInstallation Installation() => new(
        InstallationId.New(), GameId.New(), ProviderKind.Gog, "1495134320", _installRoot,
        null, true, true, DateTimeOffset.UtcNow, InstallationContentKind.Game);

    private void WriteInfo(string? buildId)
    {
        File.WriteAllText(Path.Combine(_installRoot, "goggame-1495134320.info"),
            JsonSerializer.Serialize(new { gameId = "1495134320", buildId }));
    }

    private void CreateGalaxyDb(string? availableBuild, int operation)
    {
        using var connection = Open(_galaxyDb);
        Execute(connection, "create table ProductStates(productId text, installation integer, operation integer)");
        Execute(connection, "create table Builds(productId text, manifest text, createdAt text)");
        Execute(connection, "insert into ProductStates values ('1495134320', 3, $operation)", ("$operation", operation));
        var manifest = availableBuild is null ? "{}" : JsonSerializer.Serialize(new
        {
            items = new[] { new { build_id = availableBuild, os = "windows", branch = (string?)null, @public = true, date_published = "2026-10-01T09:29:42+0000" } }
        });
        Execute(connection, "insert into Builds values ('1495134320', $manifest, '2026-10-01')", ("$manifest", manifest));
    }

    private void CreateGalaxyDbForOtherProduct(string? availableBuild, int operation)
    {
        using var connection = Open(_galaxyDb);
        Execute(connection, "create table ProductStates(productId text, installation integer, operation integer)");
        Execute(connection, "create table Builds(productId text, manifest text, createdAt text)");
        Execute(connection, "insert into ProductStates values ('other', 3, $operation)", ("$operation", operation));
        var manifest = JsonSerializer.Serialize(new { items = new[] { new { build_id = availableBuild, os = "windows", branch = (string?)null, @public = true, date_published = "2026-10-01T09:29:42+0000" } } });
        Execute(connection, "insert into Builds values ('other', $manifest, '2026-10-01')", ("$manifest", manifest));
        var targetManifest = JsonSerializer.Serialize(new { items = new[] { new { build_id = "200", os = "windows", branch = (string?)null, @public = true, date_published = "2026-10-01T09:29:42+0000" } } });
        Execute(connection, "insert into Builds values ('1495134320', $manifest, '2026-10-01')", ("$manifest", targetManifest));
    }

    private void CreateJobsDb(bool active)
    {
        using var connection = Open(_jobsDb);
        Execute(connection, "create table Jobs(jobId integer, jobType integer, jobOrderIndex integer, operationPhase integer)");
        Execute(connection, "create table UpdateProductJobs(jobId integer, gameName text, productId text, languageCode text, selectedBranch text, buildId text, downloadSize integer, patchingSize integer, verificationSize integer, downloadProgress integer, patchingProgress integer, verificationProgress integer, overallDownloadProgress integer, isVerifying integer)");
        if (active)
        {
            Execute(connection, "insert into Jobs values (1, 4, 0, 10)");
            Execute(connection, "insert into UpdateProductJobs values (1, 'Witcher', '1495134320', 'fr-FR', null, null, 100, 10, 5, 20, 0, 0, 20, 0)");
        }
    }

    private void CreateJobsDbForOtherProduct()
    {
        using var connection = Open(_jobsDb);
        Execute(connection, "create table Jobs(jobId integer, jobType integer, jobOrderIndex integer, operationPhase integer)");
        Execute(connection, "create table UpdateProductJobs(jobId integer, gameName text, productId text, languageCode text, selectedBranch text, buildId text, downloadSize integer, patchingSize integer, verificationSize integer, downloadProgress integer, patchingProgress integer, verificationProgress integer, overallDownloadProgress integer, isVerifying integer)");
        Execute(connection, "insert into Jobs values (1, 4, 0, 10)");
        Execute(connection, "insert into UpdateProductJobs values (1, 'Other', 'other', 'en-US', null, null, 100, 0, 0, 20, 0, 0, 20, 0)");
    }

    private static SqliteConnection Open(string path)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false
        }.ToString());
        connection.Open();
        return connection;
    }

    private static void Execute(SqliteConnection connection, string sql, params (string Name, object? Value)[] parameters)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var parameter in parameters)
            command.Parameters.AddWithValue(parameter.Name, parameter.Value ?? DBNull.Value);
        command.ExecuteNonQuery();
    }

    public void Dispose()
    {
        for (var attempt = 0; attempt < 5 && Directory.Exists(_root); attempt++)
        {
            try
            {
                Directory.Delete(_root, recursive: true);
            }
            catch (IOException) when (attempt < 4)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                Thread.Sleep(25);
            }
        }
    }
}
