using PlayStead.Core.Library;
using PlayStead.Core.LocalArtifacts;

namespace PlayStead.Core.Tests.LocalArtifacts;

public sealed class UserDefinedLocalArtifactServiceTests
{
    [Fact]
    public async Task Existing_absolute_directory_is_added_and_duplicate_is_rejected()
    {
        var root = Path.Combine(Path.GetTempPath(), "PlayStead-UserArtifact-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var store = new FakeStore();
            var service = new UserDefinedLocalArtifactService(store);
            var game = GameId.New();
            var added = await service.AddAsync(game, GameLocalArtifactKind.SaveData, root, null, CancellationToken.None);
            Assert.Equal(GameLocalArtifactSource.UserDefined, new GameLocalArtifact(game, added.Kind, added.Path, GameLocalArtifactSource.UserDefined, GameLocalArtifactStatus.KnownAndExists, $"user:{added.Id:D}").Source);
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.AddAsync(game, GameLocalArtifactKind.SaveData, root, null, CancellationToken.None));
            await Assert.ThrowsAsync<ArgumentException>(() => service.AddAsync(game, GameLocalArtifactKind.Log, "relative", null, CancellationToken.None));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Directory_already_covered_by_builtin_rule_is_rejected()
    {
        var root = Path.Combine(Path.GetTempPath(), "PlayStead-BuiltinArtifact-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var game = GameId.New();
            var service = new UserDefinedLocalArtifactService(
                new FakeStore(),
                [new GameLocalArtifactRule(null, GameLocalArtifactKind.Configuration, root, Provider: ProviderKind.Steam, ProviderGameId: "42")]);

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.AddAsync(
                game, ProviderKind.Steam, "42", GameLocalArtifactKind.Configuration, root, null, CancellationToken.None));
        }
        finally { Directory.Delete(root, true); }
    }

    private sealed class FakeStore : IUserDefinedLocalArtifactStore
    {
        private readonly List<UserDefinedLocalArtifact> _items = [];
        public Task<IReadOnlyList<UserDefinedLocalArtifact>> GetByGameAsync(GameId gameId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<UserDefinedLocalArtifact>>(_items.Where(x => x.GameId == gameId).ToArray());
        public Task AddAsync(UserDefinedLocalArtifact artifact, CancellationToken cancellationToken) { _items.Add(artifact); return Task.CompletedTask; }
        public Task RemoveAsync(Guid id, CancellationToken cancellationToken) { _items.RemoveAll(x => x.Id == id); return Task.CompletedTask; }
    }
}
