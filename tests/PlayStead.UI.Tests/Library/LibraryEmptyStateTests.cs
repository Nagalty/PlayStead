using PlayStead.Core.Library;
using PlayStead.Core.Modding;
using PlayStead.Core.Notifications;
using PlayStead.Core.Persistence;
using PlayStead.Core.Scanning;
using PlayStead.UI.Library;

namespace PlayStead.UI.Tests.Library;

public sealed class LibraryEmptyStateTests
{
    [Fact]
    public async Task Attention_empty_state_uses_contextual_copy()
    {
        var library = await CreateAsync();
        library.AttachAttentionService(new EmptyAttentionService());
        library.SetQuickFilter(LibraryQuickFilter.Attention);

        Assert.Empty(library.VisibleItems);
        Assert.Equal("Rien à te signaler pour l’instant.\nTout roule, je garde quand même un œil dessus.", library.EmptyStateMessage);
    }

    [Fact]
    public async Task Modded_empty_state_is_explicit_about_confirmed_evidence()
    {
        var library = await CreateAsync();
        library.AttachModEvidenceStore(new EmptyEvidenceStore());
        await library.RefreshAsync(CancellationToken.None);
        library.SetQuickFilter(LibraryQuickFilter.Modded);

        Assert.Empty(library.VisibleItems);
        Assert.Equal("Je n’ai trouvé aucun jeu clairement moddé pour l’instant.\nSi j’ai un doute, je préfère te le dire plutôt que d’inventer.", library.EmptyStateMessage);
    }

    [Fact]
    public async Task Empty_projection_keeps_generic_copy()
    {
        var library = await CreateAsync();
        library.SetSearchQuery("missing");

        Assert.Empty(library.VisibleItems);
        Assert.Equal("Rien ne correspond à ces filtres pour l’instant.", library.EmptyStateMessage);
    }

    private static async Task<LibraryViewModel> CreateAsync()
    {
        var now = DateTimeOffset.UtcNow;
        var game = GameId.New();
        var snapshot = new LibrarySnapshot(
            [new LogicalGame(game, "Example", false, now, now)],
            [new GameInstallation(InstallationId.New(), game, ProviderKind.Steam, "123", @"D:\Games\Example", null, true, true, now)]);
        var library = new LibraryViewModel(new StubLibraryStore(snapshot));
        await library.RefreshAsync(CancellationToken.None);
        return library;
    }

    private sealed class StubLibraryStore(LibrarySnapshot snapshot) : ILibraryStore
    {
        public Task ApplySourceScanAsync(SourceScanResult result, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<LibrarySnapshot> LoadSnapshotAsync(CancellationToken cancellationToken) => Task.FromResult(snapshot);
    }

    private sealed class EmptyAttentionService : IAttentionService
    {
        public IReadOnlyList<AttentionItem> Items => [];
        public event EventHandler? Changed { add { } remove { } }
        public Task<IReadOnlyList<AttentionItem>> GetActiveAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<AttentionItem>>([]);
        public Task RefreshAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class EmptyEvidenceStore : IModEvidenceStore
    {
        public Task<IReadOnlyList<ModEvidence>> GetByGameAsync(GameId gameId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ModEvidence>>([]);
        public Task<IReadOnlyList<ModEvidence>> GetAllAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ModEvidence>>([]);
        public Task UpsertAsync(ModEvidence evidence, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task RemoveAsync(GameId gameId, string detectorId, ModEvidenceKind evidenceKind, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
