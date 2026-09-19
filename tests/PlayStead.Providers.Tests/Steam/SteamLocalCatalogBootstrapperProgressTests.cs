using PlayStead.Core.Library;
using PlayStead.Core.Persistence;
using PlayStead.Providers.Steam;

namespace PlayStead.Providers.Tests.Steam;

public sealed class SteamLocalCatalogBootstrapperProgressTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "PlayStead.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Reports_materialized_batch_progress_and_counts_failed_items()
    {
        var steamRoot = Path.Combine(_root, "Steam");
        var steamApps = Path.Combine(steamRoot, "steamapps");
        Directory.CreateDirectory(Path.Combine(steamApps, "common"));
        WriteManifest(steamApps, "101", "First Game");
        WriteManifest(steamApps, "202", "Second Game");

        var snapshot = CreateSnapshot(DateTimeOffset.UtcNow);
        var source = new SteamLocalCatalogImportSource(
            new WindowsSteamRootLocator([steamRoot]),
            new SteamLibraryFoldersReader(),
            new SteamAppManifestReader(),
            new SteamAppInfoReader());
        var writer = new RecordingWriter { FailExternalId = "101" };
        var sut = new SteamLocalCatalogBootstrapper(source, writer);
        var progress = new List<SteamCatalogBootstrapProgress>();

        await sut.RunAsync(snapshot, DateTimeOffset.UtcNow, CancellationToken.None, progress.Add);

        Assert.Equal(["101", "202"], writer.AttemptedExternalIds);
        Assert.Equal([new(1, 2), new(2, 2)], progress);
        Assert.Equal(progress.OrderBy(item => item.Current), progress);
        Assert.Equal(2, progress[^1].Current);
        Assert.Equal(2, progress[^1].Total);
    }

    private static LibrarySnapshot CreateSnapshot(DateTimeOffset now)
    {
        var firstGame = GameId.New();
        var secondGame = GameId.New();
        return new LibrarySnapshot(
            [
                new LogicalGame(firstGame, "First Game", false, now, now),
                new LogicalGame(secondGame, "Second Game", false, now, now)
            ],
            [
                new GameInstallation(InstallationId.New(), firstGame, ProviderKind.Steam, "101", "C:\\Steam\\First", null, true, true, now),
                new GameInstallation(InstallationId.New(), secondGame, ProviderKind.Steam, "202", "C:\\Steam\\Second", null, true, true, now)
            ]);
    }

    private static void WriteManifest(string steamApps, string appId, string name) =>
        File.WriteAllText(Path.Combine(steamApps, $"appmanifest_{appId}.acf"), $$"""
            "AppState"
            {
                "appid" "{{appId}}"
                "name" "{{name}}"
                "installdir" "{{name}}"
            }
            """);

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private sealed class RecordingWriter : ICanonicalCatalogWriter
    {
        public List<string> AttemptedExternalIds { get; } = [];
        public string? FailExternalId { get; init; }

        public Task ImportSteamAsync(IReadOnlyCollection<CanonicalCatalogImportItem> items, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var item = Assert.Single(items);
            AttemptedExternalIds.Add(item.ExternalId);
            if (item.ExternalId == FailExternalId)
                throw new IOException("Simulated per-game import failure.");
            return Task.CompletedTask;
        }
    }
}
