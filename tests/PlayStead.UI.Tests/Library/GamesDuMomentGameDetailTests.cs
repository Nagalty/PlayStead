using PlayStead.Core.Library;
using PlayStead.Core.Shortlist;
using PlayStead.Core.Steam;
using PlayStead.UI.Library;

namespace PlayStead.UI.Tests.Library;

public sealed class GamesDuMomentGameDetailTests
{
    [Fact]
    public async Task Game_detail_loads_membership_and_add_remove_commands_update_copy()
    {
        var id = new GameId(Guid.NewGuid());
        var service = new FakeGamesDuMomentService();
        var item = new LibraryItemViewModel(id, "Test", ProviderKind.Steam, "Steam", "C:\\Games", null);
        var detail = new GameDetailViewModel(item, null, null, null, gamesDuMomentService: service);

        await detail.LoadAsync(default);
        Assert.False(detail.IsInGamesDuMoment);
        Assert.Equal("Ajouter aux jeux du moment", detail.GamesDuMomentActionLabel);
        await detail.AddToGamesDuMomentCommand.ExecuteAsync(null);
        Assert.True(detail.IsInGamesDuMoment);
        Assert.Equal("Retirer des jeux du moment", detail.GamesDuMomentActionLabel);
        await detail.RemoveFromGamesDuMomentCommand.ExecuteAsync(null);
        Assert.False(detail.IsInGamesDuMoment);
    }

    private sealed class FakeGamesDuMomentService : IGamesDuMomentService
    {
        private readonly List<GamesDuMomentEntry> _entries = [];
        public Task<IReadOnlyList<GamesDuMomentEntry>> GetAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<GamesDuMomentEntry>>(_entries);
        public Task<GamesDuMomentAddResult> AddAsync(GameId gameId, CancellationToken cancellationToken) { if (_entries.Any(x => x.GameId == gameId)) return Task.FromResult(GamesDuMomentAddResult.AlreadyMember); if (_entries.Count == 5) return Task.FromResult(GamesDuMomentAddResult.Full); _entries.Add(new GamesDuMomentEntry(gameId, _entries.Count, DateTimeOffset.UtcNow)); return Task.FromResult(GamesDuMomentAddResult.Added); }
        public Task<bool> RemoveAsync(GameId gameId, CancellationToken cancellationToken) { var e = _entries.FirstOrDefault(x => x.GameId == gameId); if (e is null) return Task.FromResult(false); _entries.Remove(e); return Task.FromResult(true); }
        public Task ReorderAsync(IReadOnlyList<GameId> orderedGameIds, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
