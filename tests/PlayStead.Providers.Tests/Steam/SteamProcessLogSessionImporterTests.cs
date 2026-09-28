using PlayStead.Core.Library;
using PlayStead.Core.ProviderActivity;
using PlayStead.Providers.Steam;

namespace PlayStead.Providers.Tests.Steam;

public sealed class SteamProcessLogSessionImporterTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "PlayStead.Tests", "SteamProcessLog", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Dune_process_log_episode_maps_to_canonical_installation_and_is_persistable()
    {
        Directory.CreateDirectory(Path.Combine(_root, "logs"));
        await File.WriteAllLinesAsync(Path.Combine(_root, "logs", "gameprocess_log.txt"), [
            "[2026-09-28 09:52:13] AppID 1172710 adding PID 5696 as a tracked process",
            "[2026-09-28 09:52:13] AppID 1172710 adding PID 68300 as a tracked process",
            "[2026-09-28 11:51:27] AppID 1172710 no longer tracking PID 68300, exit code 0",
            "[2026-09-28 11:51:27] AppID 1172710 no longer tracking PID 5696, exit code 0"]);
        var gameId = GameId.New();
        var store = new CaptureStore();
        var importer = new SteamProcessLogSessionImporter(
            new WindowsSteamRootLocator([_root]), new SteamProcessLogSessionParser(), store);

        await importer.ImportAsync([
            new GameInstallation(new InstallationId(Guid.NewGuid()), gameId, ProviderKind.Steam, "1172710", Path.Combine(_root, "common", "DuneAwakening"), null, true, true, DateTimeOffset.UtcNow)], CancellationToken.None);

        var imported = Assert.Single(store.Sessions);
        Assert.Equal(gameId, imported.GameId);
        Assert.Equal("1172710", imported.ProviderGameId);
        Assert.True(imported.EndedAtUtc > imported.StartedAtUtc);
        Assert.Equal(ProviderObservedSessionCompleteness.Complete, imported.Completeness);
    }

    [Fact]
    public async Task Reads_log_while_steam_keeps_writer_handle_open()
    {
        Directory.CreateDirectory(Path.Combine(_root, "logs"));
        var path = Path.Combine(_root, "logs", "gameprocess_log.txt");
        await using var writer = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
        await using var text = new StreamWriter(writer);
        await text.WriteLineAsync("[2026-09-28 09:52:13] AppID 1172710 adding PID 5696 as a tracked process");
        await text.WriteLineAsync("[2026-09-28 11:51:27] AppID 1172710 no longer tracking PID 5696, exit code 0");
        await text.FlushAsync();

        var gameId = GameId.New();
        var store = new CaptureStore();
        var importer = new SteamProcessLogSessionImporter(
            new WindowsSteamRootLocator([_root]), new SteamProcessLogSessionParser(), store);

        await importer.ImportAsync([
            new GameInstallation(new InstallationId(Guid.NewGuid()), gameId, ProviderKind.Steam, "1172710", Path.Combine(_root, "common", "DuneAwakening"), null, true, true, DateTimeOffset.UtcNow)], CancellationToken.None);

        var imported = Assert.Single(store.Sessions);
        Assert.Equal("1172710", imported.ProviderGameId);
        Assert.True(imported.EndedAtUtc > imported.StartedAtUtc);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private sealed class CaptureStore : IProviderObservedSessionStore
    {
        public List<ProviderObservedSession> Sessions { get; } = [];
        public Task UpsertAsync(ProviderObservedSession session, CancellationToken cancellationToken)
        {
            Sessions.Add(session);
            return Task.CompletedTask;
        }
        public Task<IReadOnlyList<ProviderObservedSession>> GetByPeriodAsync(DateTimeOffset startUtc, DateTimeOffset endUtc, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ProviderObservedSession>>(Sessions);
    }
}
