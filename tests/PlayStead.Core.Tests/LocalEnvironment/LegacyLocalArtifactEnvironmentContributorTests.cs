using PlayStead.Core.Library;
using PlayStead.Core.LocalArtifacts;
using PlayStead.Core.LocalEnvironment;

namespace PlayStead.Core.Tests.LocalEnvironment;

public sealed class LegacyLocalArtifactEnvironmentContributorTests
{
    [Fact]
    public async Task Dune_legacy_rules_and_user_paths_are_projected_to_new_entries()
    {
        var gameId = GameId.New();
        var installation = new GameInstallation(InstallationId.New(), gameId, ProviderKind.Steam, "1172710", Path.GetTempPath(), null, true, true, DateTimeOffset.UtcNow);
        var user = new UserDefinedLocalArtifact(Guid.NewGuid(), gameId, GameLocalArtifactKind.Other, Path.Combine(Path.GetTempPath(), "DuneMods"), "Mods", DateTimeOffset.UtcNow);
        var contributor = new LegacyLocalArtifactEnvironmentContributor(LocalArtifactRuleCatalog.Rules, new MemoryUsers(user));

        var entries = await contributor.DiscoverAsync(installation, CancellationToken.None);

        Assert.Contains(entries, x => x.Kind == LocalEnvironmentEntryKind.Config && x.Path.Contains("DuneSandbox", StringComparison.OrdinalIgnoreCase));
        var gameUserSettings = Assert.Single(entries, x => x.Path.EndsWith("GameUserSettings.ini", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(LocalEnvironmentEntryKind.Config, gameUserSettings.Kind);
        Assert.Equal(LocalEnvironmentPathKind.File, gameUserSettings.PathKind);
        Assert.Contains(entries, x => x.Kind == LocalEnvironmentEntryKind.Log && x.Path.Contains("DuneSandbox", StringComparison.OrdinalIgnoreCase));
        var custom = Assert.Single(entries, x => x.IsUserOverride);
        Assert.Equal(LocalEnvironmentSource.User, custom.Source);
        Assert.Equal(LocalEnvironmentConfidence.Confirmed, custom.Confidence);
    }

    private sealed class MemoryUsers(UserDefinedLocalArtifact item) : IUserDefinedLocalArtifactStore
    {
        public Task<IReadOnlyList<UserDefinedLocalArtifact>> GetByGameAsync(GameId gameId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<UserDefinedLocalArtifact>>(item.GameId == gameId ? [item] : []);
        public Task AddAsync(UserDefinedLocalArtifact artifact, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task RemoveAsync(Guid id, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
