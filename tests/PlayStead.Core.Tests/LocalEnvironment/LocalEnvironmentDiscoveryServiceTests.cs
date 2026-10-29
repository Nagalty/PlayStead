using PlayStead.Core.Library;
using PlayStead.Core.Catalog;
using PlayStead.Core.LocalEnvironment;

namespace PlayStead.Core.Tests.LocalEnvironment;

public sealed class LocalEnvironmentDiscoveryServiceTests
{
    [Fact]
    public async Task Config_directory_and_file_rules_coexist_without_duplicate_entries()
    {
        var root = Path.Combine(Path.GetTempPath(), "PlayStead.DuneConfig-" + Guid.NewGuid().ToString("N"));
        var configDirectory = Path.Combine(root, "Config", "Windows");
        Directory.CreateDirectory(configDirectory);
        var file = Path.Combine(configDirectory, "GameUserSettings.ini");
        await File.WriteAllTextAsync(file, "[ScalabilityGroups]\nsg.ResolutionQuality=100\n");
        var installation = Installation(root);
        var catalogId = CatalogContentId.New();
        var rules = new[]
        {
            new LocalEnvironmentRule(catalogId, LocalEnvironmentEntryKind.Config, "<INSTALL_ROOT>\\Config", LocalEnvironmentPathKind.Directory, LocalEnvironmentConfidence.Confirmed, LocalEnvironmentSource.Catalog),
            new LocalEnvironmentRule(catalogId, LocalEnvironmentEntryKind.Config, "<INSTALL_ROOT>\\Config\\Windows\\GameUserSettings.ini", LocalEnvironmentPathKind.File, LocalEnvironmentConfidence.Confirmed, LocalEnvironmentSource.Catalog),
            new LocalEnvironmentRule(catalogId, LocalEnvironmentEntryKind.Config, "<INSTALL_ROOT>\\Config\\Windows\\GameUserSettings.ini", LocalEnvironmentPathKind.File, LocalEnvironmentConfidence.Detected, LocalEnvironmentSource.Catalog)
        };
        var store = new MemoryStore();
        var service = new LocalEnvironmentDiscoveryService([new RuleBasedLocalEnvironmentContributor(rules, _ => catalogId)], store, (id, _) => Task.FromResult<GameInstallation?>(installation));

        var entries = (await service.RefreshEnvironmentAsync(installation.Id, CancellationToken.None)).Entries;
        Assert.Equal(2, entries.Count);
        Assert.Contains(entries, x => x.PathKind == LocalEnvironmentPathKind.Directory && x.Path.EndsWith("Config", StringComparison.OrdinalIgnoreCase));
        var configFile = Assert.Single(entries, x => x.PathKind == LocalEnvironmentPathKind.File);
        Assert.True(configFile.Exists);
        Assert.EndsWith("GameUserSettings.ini", configFile.Path, StringComparison.OrdinalIgnoreCase);
        Directory.Delete(root, true);
    }

    [Fact]
    public async Task Rule_templates_discover_configs_and_logs_with_metadata()
    {
        var root = Path.Combine(Path.GetTempPath(), "PlayStead.LocalEnvironment-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "Logs"));
        await File.WriteAllTextAsync(Path.Combine(root, "settings.json"), "{}");
        var installation = Installation(root);
        var catalogId = CatalogContentId.New();
        var rules = new[]
        {
            new LocalEnvironmentRule(catalogId, LocalEnvironmentEntryKind.Config, "<INSTALL_ROOT>\\settings.json", LocalEnvironmentPathKind.File, LocalEnvironmentConfidence.Confirmed, LocalEnvironmentSource.Catalog),
            new LocalEnvironmentRule(catalogId, LocalEnvironmentEntryKind.Log, "<INSTALL_ROOT>\\Logs", LocalEnvironmentPathKind.Directory, LocalEnvironmentConfidence.Detected, LocalEnvironmentSource.Catalog)
        };
        var store = new MemoryStore();
        var service = new LocalEnvironmentDiscoveryService(
            [new RuleBasedLocalEnvironmentContributor(rules, _ => catalogId)], store,
            (id, _) => Task.FromResult<GameInstallation?>(id == installation.Id ? installation : null));

        var environment = await service.RefreshEnvironmentAsync(installation.Id, CancellationToken.None);
        var config = Assert.Single(await service.GetConfigsAsync(installation.Id, CancellationToken.None));
        var log = Assert.Single(await service.GetLogsAsync(installation.Id, CancellationToken.None));
        Assert.True(config.Exists);
        Assert.NotNull(config.LastModifiedUtc);
        Assert.True(log.Exists);
        Assert.Equal(2, environment.Entries.Count);
        Directory.Delete(root, true);
    }

    [Fact]
    public async Task Duplicate_contributors_keep_highest_confidence_and_source()
    {
        var installation = Installation(Path.GetTempPath());
        var path = "same.json";
        var first = Entry(installation.Id, LocalEnvironmentEntryKind.Mod, path, LocalEnvironmentPathKind.Directory) with { Confidence = LocalEnvironmentConfidence.Detected, Source = LocalEnvironmentSource.Catalog, Metadata = new Dictionary<string, string> { ["DetectionEvidence"] = "one" } };
        var second = first with { Confidence = LocalEnvironmentConfidence.Confirmed, Source = LocalEnvironmentSource.Provider, Metadata = new Dictionary<string, string> { ["DetectionEvidence"] = "two" } };
        var store = new MemoryStore();
        var service = new LocalEnvironmentDiscoveryService([new FixedContributor(first), new FixedContributor(second)], store, (id, _) => Task.FromResult<GameInstallation?>(installation));

        var entry = Assert.Single((await service.RefreshEnvironmentAsync(installation.Id, CancellationToken.None)).Entries);
        Assert.Equal(LocalEnvironmentConfidence.Confirmed, entry.Confidence);
        Assert.Equal(LocalEnvironmentSource.Provider, entry.Source);
        Assert.Contains("one", entry.Metadata!["DetectionEvidence"]);
        Assert.Contains("two", entry.Metadata["DetectionEvidence"]);
    }

    [Fact]
    public async Task Refresh_preserves_entry_identity_for_existing_path()
    {
        var installation = Installation();
        var store = new MemoryStore();
        var service = new LocalEnvironmentDiscoveryService([new RecreatedContributor(installation.Id, "settings.ini")], store, (id, _) => Task.FromResult<GameInstallation?>(installation));

        var first = Assert.Single((await service.RefreshEnvironmentAsync(installation.Id, CancellationToken.None)).Entries);
        var second = Assert.Single((await service.RefreshEnvironmentAsync(installation.Id, CancellationToken.None)).Entries);

        Assert.Equal(first.Id, second.Id);
    }

    [Fact]
    public async Task Unknown_template_variable_is_missing_without_throwing()
    {
        var installation = Installation(Path.GetTempPath());
        var entry = Entry(installation.Id, LocalEnvironmentEntryKind.Log, "%UNKNOWN_PLAYSTEAD_ROOT%\\game.log", LocalEnvironmentPathKind.File);
        var store = new MemoryStore();
        var service = Create(store, installation, entry);

        var actual = Assert.Single((await service.RefreshEnvironmentAsync(installation.Id, CancellationToken.None)).Entries);
        Assert.False(actual.Exists);
        Assert.Equal(entry.Path, actual.Path);
    }

    [Fact]
    public async Task Confirmed_HellLetLoose_and_Crysis_fixture_rules_are_provider_neutral()
    {
        var hellLetLooseCatalogId = CatalogContentId.New();
        var crysisCatalogId = CatalogContentId.New();
        var rules = new[]
        {
            new LocalEnvironmentRule(hellLetLooseCatalogId, LocalEnvironmentEntryKind.Config, "%LOCALAPPDATA%\\HLL\\Saved\\Config\\WindowsNoEditor\\GameUserSettings.ini", LocalEnvironmentPathKind.File, LocalEnvironmentConfidence.Confirmed, LocalEnvironmentSource.Catalog),
            new LocalEnvironmentRule(hellLetLooseCatalogId, LocalEnvironmentEntryKind.Log, "%LOCALAPPDATA%\\HLL\\Saved\\Logs", LocalEnvironmentPathKind.Directory, LocalEnvironmentConfidence.Confirmed, LocalEnvironmentSource.Catalog),
            new LocalEnvironmentRule(crysisCatalogId, LocalEnvironmentEntryKind.Config, "<INSTALL_ROOT>\\GalaxyConfig.json", LocalEnvironmentPathKind.File, LocalEnvironmentConfidence.Confirmed, LocalEnvironmentSource.Catalog),
            new LocalEnvironmentRule(crysisCatalogId, LocalEnvironmentEntryKind.Log, "<INSTALL_ROOT>\\Game.log", LocalEnvironmentPathKind.File, LocalEnvironmentConfidence.Confirmed, LocalEnvironmentSource.Catalog)
        };
        var hll = Installation(Path.Combine(Path.GetTempPath(), "HellLetLooseG0WU4"));
        var crysis = Installation(Path.Combine(Path.GetTempPath(), "Crysis Remastered"));
        var contributor = new RuleBasedLocalEnvironmentContributor(rules, installation => installation == hll ? hellLetLooseCatalogId : crysisCatalogId);

        var hllEntries = await contributor.DiscoverAsync(hll, CancellationToken.None);
        var crysisEntries = await contributor.DiscoverAsync(crysis, CancellationToken.None);
        Assert.Equal(2, hllEntries.Count);
        Assert.Equal(2, crysisEntries.Count);
        Assert.All(hllEntries.Concat(crysisEntries), entry => Assert.Equal(LocalEnvironmentSource.Catalog, entry.Source));
    }

    [Fact]
    public async Task Save_file_and_directory_metadata_are_bounded_and_cloud_is_not_inferred()
    {
        var root = Path.Combine(Path.GetTempPath(), "PlayStead.Save-" + Guid.NewGuid().ToString("N"));
        var saveDirectory = Path.Combine(root, "Saves");
        Directory.CreateDirectory(saveDirectory);
        await File.WriteAllTextAsync(Path.Combine(saveDirectory, "one.sav"), "1234");
        await File.WriteAllTextAsync(Path.Combine(saveDirectory, "two.sav"), "567890");
        var installation = Installation(root);
        var directoryEntry = Entry(installation.Id, LocalEnvironmentEntryKind.Save, "Saves", LocalEnvironmentPathKind.Directory);
        var fileEntry = directoryEntry with { Id = Guid.NewGuid(), Path = "Saves\\one.sav", PathKind = LocalEnvironmentPathKind.File };
        var store = new MemoryStore();
        var service = new LocalEnvironmentDiscoveryService([new FixedContributor(directoryEntry), new FixedContributor(fileEntry)], store, (id, _) => Task.FromResult<GameInstallation?>(installation));

        var save = await service.RefreshEnvironmentAsync(installation.Id, CancellationToken.None);
        var saves = await service.GetSavesAsync(installation.Id, CancellationToken.None);
        var directory = Assert.Single(saves, x => x.Path.EndsWith("Saves", StringComparison.OrdinalIgnoreCase));
        var file = Assert.Single(saves, x => x.Path.EndsWith("one.sav", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("2", directory.Metadata!["FileCount"]);
        Assert.Equal("10", directory.Metadata["TotalSizeBytes"]);
        Assert.Equal("Unknown", directory.Metadata["CloudProvider"]);
        Assert.Equal("1", file.Metadata!["FileCount"]);
        Assert.NotNull(file.LastModifiedUtc);
        Assert.Equal(2, save.Entries.Count);
        Directory.Delete(root, true);
    }

    [Fact]
    public async Task Explicit_cloud_provider_metadata_is_preserved()
    {
        var root = Path.Combine(Path.GetTempPath(), "PlayStead.Save-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var installation = Installation(root);
        var entry = Entry(installation.Id, LocalEnvironmentEntryKind.Save, "cloud.sav", LocalEnvironmentPathKind.File) with { Metadata = new Dictionary<string, string> { ["CloudProvider"] = LocalEnvironmentCloudProvider.SteamCloud.ToString() } };
        var store = new MemoryStore();
        var service = Create(store, installation, entry);
        var actual = Assert.Single((await service.RefreshEnvironmentAsync(installation.Id, CancellationToken.None)).Entries);
        Assert.Equal("SteamCloud", actual.Metadata!["CloudProvider"]);
        Directory.Delete(root, true);
    }

    [Fact]
    public async Task Save_scan_is_bounded_by_file_policy()
    {
        var root = Path.Combine(Path.GetTempPath(), "PlayStead.Save-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, "one.sav"), "1");
        await File.WriteAllTextAsync(Path.Combine(root, "two.sav"), "2");
        await File.WriteAllTextAsync(Path.Combine(root, "three.sav"), "3");

        var result = LocalEnvironmentSaveScanner.Scan(root, LocalEnvironmentPathKind.Directory, CancellationToken.None, new LocalEnvironmentSaveScanPolicy(MaxFiles: 2));
        Assert.True(result.Exists);
        Assert.Equal("2", result.Metadata["FileCount"]);
        Directory.Delete(root, true);
    }
    [Fact]
    public async Task Entries_belong_to_installation_and_missing_path_is_safe()
    {
        var installation = Installation();
        var entry = Entry(installation.Id, LocalEnvironmentEntryKind.Save, "%LOCALAPPDATA%\\PlayStead\\missing-save", LocalEnvironmentPathKind.Directory);
        var store = new MemoryStore();
        var service = Create(store, installation, entry);

        var environment = await service.RefreshEnvironmentAsync(installation.Id, CancellationToken.None);

        var actual = Assert.Single(environment.Entries);
        Assert.Equal(installation.Id, actual.InstallationId);
        Assert.False(actual.Exists);
    }

    [Fact]
    public async Task Two_installations_of_same_game_have_separate_environments()
    {
        var first = Installation();
        var second = Installation(first.GameId);
        var store = new MemoryStore();
        var service = new LocalEnvironmentDiscoveryService([], store, (id, _) => Task.FromResult<GameInstallation?>(id == first.Id ? first : second));
        store.Detected[first.Id] = [Entry(first.Id, LocalEnvironmentEntryKind.Config, "config-a.ini", LocalEnvironmentPathKind.File)];
        store.Detected[second.Id] = [Entry(second.Id, LocalEnvironmentEntryKind.Config, "config-b.ini", LocalEnvironmentPathKind.File)];

        Assert.Equal("config-a.ini", (await service.GetEnvironmentAsync(first.Id, CancellationToken.None)).Entries.Single().Path);
        Assert.Equal("config-b.ini", (await service.GetEnvironmentAsync(second.Id, CancellationToken.None)).Entries.Single().Path);
    }

    [Fact]
    public async Task User_override_wins_and_detected_entries_are_refresh_idempotent()
    {
        var installation = Installation();
        var store = new MemoryStore();
        var detected = Entry(installation.Id, LocalEnvironmentEntryKind.Config, "detected.ini", LocalEnvironmentPathKind.File);
        var overrideEntry = detected with { Id = Guid.NewGuid(), Path = "override.ini", IsUserOverride = true, Confidence = LocalEnvironmentConfidence.Confirmed, Source = LocalEnvironmentSource.User };
        store.Detected[installation.Id] = [detected];
        store.Overrides[installation.Id] = [overrideEntry];
        var service = Create(store, installation, detected);

        var first = await service.RefreshEnvironmentAsync(installation.Id, CancellationToken.None);
        var second = await service.RefreshEnvironmentAsync(installation.Id, CancellationToken.None);

        var actual = Assert.Single(first.Entries);
        Assert.Equal("override.ini", actual.Path);
        Assert.Equal(first.Entries.Select(x => x.Path), second.Entries.Select(x => x.Path));

        await service.RemoveUserOverrideAsync(installation.Id, LocalEnvironmentEntryKind.Config, "override.ini", CancellationToken.None);
        Assert.Single((await service.GetEnvironmentAsync(installation.Id, CancellationToken.None)).Entries);
    }

    private static LocalEnvironmentDiscoveryService Create(MemoryStore store, GameInstallation installation, LocalEnvironmentEntry entry) =>
        new([new FixedContributor(entry)], store, (id, _) => Task.FromResult<GameInstallation?>(id == installation.Id ? installation : null));

    private static GameInstallation Installation(string root, GameId? gameId = null) =>
        new(InstallationId.New(), gameId ?? GameId.New(), ProviderKind.Manual, "fixture", root, null, true, true, DateTimeOffset.UtcNow);

    private static GameInstallation Installation(GameId? gameId = null) => Installation(Path.GetTempPath(), gameId);

    private static LocalEnvironmentEntry Entry(InstallationId id, LocalEnvironmentEntryKind kind, string path, LocalEnvironmentPathKind pathKind) =>
        new(Guid.NewGuid(), id, kind, path, pathKind, LocalEnvironmentConfidence.Detected, LocalEnvironmentSource.Heuristic, false, DateTimeOffset.UtcNow);

    private sealed class FixedContributor(LocalEnvironmentEntry entry) : ILocalEnvironmentContributor
    {
        public Task<IReadOnlyList<LocalEnvironmentEntry>> DiscoverAsync(GameInstallation installation, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<LocalEnvironmentEntry>>([entry]);
    }

    private sealed class RecreatedContributor(InstallationId id, string path) : ILocalEnvironmentContributor
    {
        public Task<IReadOnlyList<LocalEnvironmentEntry>> DiscoverAsync(GameInstallation installation, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<LocalEnvironmentEntry>>([new LocalEnvironmentEntry(Guid.NewGuid(), id, LocalEnvironmentEntryKind.Config, path, LocalEnvironmentPathKind.File, LocalEnvironmentConfidence.Confirmed, LocalEnvironmentSource.Catalog, false, DateTimeOffset.MinValue)]);
    }

    private sealed class MemoryStore : ILocalEnvironmentStore
    {
        public Dictionary<InstallationId, IReadOnlyList<LocalEnvironmentEntry>> Detected { get; } = [];
        public Dictionary<InstallationId, IReadOnlyList<LocalEnvironmentEntry>> Overrides { get; } = [];
        public Task<IReadOnlyList<LocalEnvironmentEntry>> GetEntriesAsync(InstallationId installationId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<LocalEnvironmentEntry>>(Detected.GetValueOrDefault(installationId, []).Concat(Overrides.GetValueOrDefault(installationId, [])).ToArray());
        public Task ReplaceDetectedAsync(InstallationId installationId, IReadOnlyCollection<LocalEnvironmentEntry> entries, CancellationToken cancellationToken) { Detected[installationId] = entries.ToArray(); return Task.CompletedTask; }
        public Task UpsertUserOverrideAsync(LocalEnvironmentEntry entry, CancellationToken cancellationToken) { Overrides[entry.InstallationId] = [entry]; return Task.CompletedTask; }
        public Task RemoveUserOverrideAsync(InstallationId installationId, LocalEnvironmentEntryKind kind, string path, CancellationToken cancellationToken)
        {
            Overrides[installationId] = Overrides.GetValueOrDefault(installationId, []).Where(x => x.Kind != kind || x.Path != path).ToArray();
            return Task.CompletedTask;
        }
    }
}
