using PlayStead.Core.Library;
using PlayStead.Core.LocalArtifacts;

namespace PlayStead.Core.Tests.LocalArtifacts;

public sealed class LocalArtifactDiscoveryServiceTests
{
    [Fact]
    public async Task Explicit_rules_resolve_config_save_and_log_paths()
    {
        var gameId = GameId.New();
        var root = Path.Combine(Path.GetTempPath(), $"playstead-artifacts-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "config"));
        Directory.CreateDirectory(Path.Combine(root, "saves"));
        Directory.CreateDirectory(Path.Combine(root, "logs"));
        try
        {
            var service = new LocalArtifactDiscoveryService([
                new(gameId, GameLocalArtifactKind.Configuration, Path.Combine(root, "config")),
                new(gameId, GameLocalArtifactKind.SaveData, Path.Combine(root, "saves")),
                new(gameId, GameLocalArtifactKind.Log, Path.Combine(root, "logs"))]);

            var artifacts = await service.DiscoverAsync(gameId, CancellationToken.None);

            Assert.Equal(3, artifacts.Count);
            Assert.All(artifacts, artifact => Assert.Equal(GameLocalArtifactStatus.KnownAndExists, artifact.Status));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Existing_file_and_directory_expose_bounded_details()
    {
        var gameId = GameId.New();
        var root = Path.Combine(Path.GetTempPath(), $"playstead-artifact-details-{Guid.NewGuid():N}");
        var saves = Path.Combine(root, "saves");
        Directory.CreateDirectory(Path.Combine(saves, "nested"));
        var first = Path.Combine(saves, "one.sav");
        var second = Path.Combine(saves, "nested", "two.sav");
        var log = Path.Combine(root, "Player.log");
        await File.WriteAllTextAsync(first, "1234");
        await File.WriteAllTextAsync(second, "12");
        await File.WriteAllTextAsync(log, "log");
        try
        {
            var service = new LocalArtifactDiscoveryService([
                new(gameId, GameLocalArtifactKind.SaveData, saves),
                new(gameId, GameLocalArtifactKind.Log, log)]);

            var artifacts = await service.DiscoverAsync(gameId, CancellationToken.None);
            var directory = Assert.Single(artifacts, x => x.Kind == GameLocalArtifactKind.SaveData);
            var file = Assert.Single(artifacts, x => x.Kind == GameLocalArtifactKind.Log);

            Assert.NotNull(directory.Details);
            Assert.False(directory.Details!.IsFile);
            Assert.Equal(2, directory.Details.FileCount);
            Assert.Equal(6, directory.Details.TotalBytes);
            Assert.NotNull(directory.Details.LastModifiedUtc);
            Assert.Equal("2 fichiers · 6 octets", directory.DetailsLabel.Split(" · modifié")[0]);
            Assert.Equal("1 fichier · 3 octets", file.DetailsLabel.Split(" · modifié")[0]);
            Assert.True(file.IsFile);
            Assert.Equal("Ouvrir le fichier", file.OpenActionLabel);
            Assert.Equal("Ouvrir le dossier", directory.OpenActionLabel);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Environment_variables_expand_and_missing_paths_remain_known()
    {
        var gameId = GameId.New();
        var root = Path.Combine(Path.GetTempPath(), $"playstead-artifacts-{Guid.NewGuid():N}");
        var variable = $"PLAYSTEAD_ARTIFACT_ROOT_{Guid.NewGuid():N}";
        Environment.SetEnvironmentVariable(variable, root);
        try
        {
            var service = new LocalArtifactDiscoveryService([
                new(gameId, GameLocalArtifactKind.SaveData, $"%{variable}%\\missing")]);

            var artifact = Assert.Single(await service.DiscoverAsync(gameId, CancellationToken.None));

            Assert.Equal(Path.GetFullPath(Path.Combine(root, "missing")), artifact.Path);
            Assert.Equal(GameLocalArtifactStatus.KnownButMissing, artifact.Status);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, null);
        }
    }

    [Fact]
    public async Task Invalid_relative_or_empty_rules_are_ignored_safely()
    {
        var gameId = GameId.New();
        var service = new LocalArtifactDiscoveryService([
            new(gameId, GameLocalArtifactKind.Configuration, "relative\\config"),
            new(gameId, GameLocalArtifactKind.Log, "")]);

        Assert.Empty(await service.DiscoverAsync(gameId, CancellationToken.None));
    }

    [Fact]
    public async Task Unknown_game_has_no_artifacts_and_discovery_is_bounded()
    {
        var service = new LocalArtifactDiscoveryService([
            new(GameId.New(), GameLocalArtifactKind.Log, Path.GetTempPath())]);

        Assert.Empty(await service.DiscoverAsync(GameId.New(), CancellationToken.None));
    }

    [Fact]
    public async Task Provider_rules_require_stable_provider_identity_and_do_not_match_title()
    {
        var gameId = GameId.New();
        var root = Path.Combine(Path.GetTempPath(), $"playstead-provider-artifacts-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var service = new LocalArtifactDiscoveryService([
                new(
                    GameId: null,
                    GameLocalArtifactKind.Configuration,
                    root,
                    GameLocalArtifactSource.KnownConvention,
                    ProviderKind.Steam,
                    "1284210")]);

            var match = await service.DiscoverAsync(gameId, ProviderKind.Steam, "1284210", CancellationToken.None);
            var wrongProviderId = await service.DiscoverAsync(gameId, ProviderKind.Steam, "different-title", CancellationToken.None);

            Assert.Single(match);
            Assert.Equal(GameLocalArtifactStatus.KnownAndExists, match[0].Status);
            Assert.Empty(wrongProviderId);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Catalog_contains_only_evidence_backed_steam_app_rules()
    {
        Assert.Contains(LocalArtifactRuleCatalog.Rules, rule =>
            rule.Provider == ProviderKind.Steam && rule.ProviderGameId == "1284210" &&
            rule.Kind == GameLocalArtifactKind.Configuration);
        Assert.Contains(LocalArtifactRuleCatalog.Rules, rule =>
            rule.Provider == ProviderKind.Steam && rule.ProviderGameId == "1284210" &&
            rule.Kind == GameLocalArtifactKind.Configuration &&
            rule.PathTemplate == @"%USERPROFILE%\Documents\Guild Wars 2\InputBinds");
        Assert.Contains(LocalArtifactRuleCatalog.Rules, rule =>
            rule.Provider == ProviderKind.Steam && rule.ProviderGameId == "1203620" &&
            rule.Kind == GameLocalArtifactKind.Configuration &&
            rule.PathTemplate == @"%USERPROFILE%\Saved Games\Enshrouded\enshrouded_user.json");
        Assert.DoesNotContain(LocalArtifactRuleCatalog.Rules, rule =>
            rule.ProviderGameId == "223850");
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Catalog_does_not_match_unknown_app_id_or_display_title()
    {
        var service = new LocalArtifactDiscoveryService(LocalArtifactRuleCatalog.Rules);

        var unknown = await service.DiscoverAsync(
            GameId.New(),
            ProviderKind.Steam,
            "Guild Wars 2",
            CancellationToken.None);

        Assert.Empty(unknown);
    }

    [Fact]
    public void Catalog_contains_verified_save_rules_for_currently_known_games()
    {
        var expected = new[]
        {
            (AppId: "377160", Path: @"%USERPROFILE%\Documents\My Games\Fallout4\Saves"),
            (AppId: "1144200", Path: @"%LOCALAPPDATA%\ReadyOrNot\Saved\SaveGames"),
            (AppId: "1643320", Path: @"%LOCALAPPDATA%\Stalker2\Saved\SaveGames")
        };

        foreach (var item in expected)
        {
            Assert.Contains(LocalArtifactRuleCatalog.Rules, rule =>
                rule.Provider == ProviderKind.Steam &&
                rule.ProviderGameId == item.AppId &&
                rule.Kind == GameLocalArtifactKind.SaveData &&
                rule.PathTemplate == item.Path);
        }

        Assert.Contains(LocalArtifactRuleCatalog.Rules, rule =>
            rule.Provider == ProviderKind.Steam && rule.ProviderGameId == "1172710" &&
            rule.Kind == GameLocalArtifactKind.Configuration &&
            rule.PathTemplate == @"%LOCALAPPDATA%\DuneSandbox\Saved\Config");
        Assert.Contains(LocalArtifactRuleCatalog.Rules, rule =>
            rule.Provider == ProviderKind.Steam && rule.ProviderGameId == "1172710" &&
            rule.Kind == GameLocalArtifactKind.Log &&
            rule.PathTemplate == @"%LOCALAPPDATA%\DuneSandbox\Saved\Logs");
        Assert.DoesNotContain(LocalArtifactRuleCatalog.Rules, rule =>
            rule.Provider == ProviderKind.Steam && rule.ProviderGameId == "1172710" &&
            rule.Kind == GameLocalArtifactKind.SaveData);
    }
}
