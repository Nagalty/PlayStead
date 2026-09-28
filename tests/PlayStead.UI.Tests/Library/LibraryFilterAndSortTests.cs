using PlayStead.Core.Library;
using PlayStead.Core.Notifications;
using PlayStead.Core.Persistence;
using PlayStead.Core.Scanning;
using PlayStead.Core.ProviderActivity;
using PlayStead.Core.Sessions;
using PlayStead.UI.Library;

namespace PlayStead.UI.Tests.Library;

public sealed class LibraryFilterAndSortTests
{
    [Fact]
    public async Task Default_projection_is_installed_and_sorted_by_name()
    {
        var items = await LoadAsync(
            ("Zulu", 10L),
            ("Alpha", 20L));

        Assert.Equal("Installed", items.QuickFilterKey);
        Assert.Equal("Title", items.SortKey);
        Assert.Equal(["Alpha", "Zulu"], items.VisibleItems.Select(x => x.Title));
    }

    [Fact]
    public async Task Attention_filter_uses_only_games_with_active_attention()
    {
        var attentionGame = GameId.New();
        var otherGame = GameId.New();
        var service = new FakeAttentionService(attentionGame);
        var library = await LoadAsync(
            (attentionGame, "Needs attention", 10L),
            (otherGame, "Other", 20L),
            service);

        library.SetQuickFilter(LibraryQuickFilter.Attention);

        var result = Assert.Single(library.VisibleItems);
        Assert.Equal(attentionGame, result.GameId);
    }

    [Fact]
    public async Task Size_sort_is_descending_and_unknown_values_are_last()
    {
        var library = await LoadAsync(
            ("Small", 10L),
            ("Large", 30L),
            ("Unknown", null));

        library.SetSortKey("Size");

        Assert.Equal(["Large", "Small", "Unknown"], library.VisibleItems.Select(x => x.Title));
    }

    [Fact]
    public async Task Filtering_out_selected_game_closes_the_quick_panel_selection()
    {
        var attentionGame = GameId.New();
        var otherGame = GameId.New();
        var library = await LoadAsync(
            (attentionGame, "Needs attention", 10L),
            (otherGame, "Other", 20L),
            new FakeAttentionService(otherGame));

        library.SelectGame(library.Items.Single(item => item.GameId == attentionGame));
        Assert.NotNull(library.SelectedItem);

        library.SetQuickFilter(LibraryQuickFilter.Attention);

        Assert.Null(library.SelectedItem);
    }

    [Fact]
    public async Task Playtime_sort_prefers_provider_then_observed_fallback_and_is_deterministic()
    {
        var providerGame = GameId.New();
        var observedGame = GameId.New();
        var unknownGame = GameId.New();
        var providerStore = new FakeProviderActivityStore(
            new ProviderActivityMetadata(providerGame, ProviderKind.Steam, "1", TimeSpan.FromHours(10), null, DateTimeOffset.UtcNow, ProviderActivityAvailability.Complete));
        var sessions = new FakeSessionStore(EndedSession(observedGame, TimeSpan.FromHours(4)));
        var library = await LoadWithActivity(
            [(providerGame, "Provider", 10L), (observedGame, "Observed", 20L), (unknownGame, "Unknown", 30L)],
            providerStore,
            sessions);

        library.SetSortKey("Playtime");

        Assert.Equal(["Provider", "Observed", "Unknown"], library.VisibleItems.Select(item => item.Title));
        Assert.Equal(1, providerStore.ReadCount);
        Assert.Equal(3, sessions.ReadCount);
        library.SetSortKey("Title");
        library.SetSortKey("Playtime");
        Assert.Equal(1, providerStore.ReadCount);
        Assert.Equal(3, sessions.ReadCount);
    }

    [Fact]
    public async Task Recent_sort_uses_max_provider_or_observed_activity()
    {
        var providerRecent = GameId.New();
        var observedRecent = GameId.New();
        var oldProvider = GameId.New();
        var providerStore = new FakeProviderActivityStore(
            new ProviderActivityMetadata(providerRecent, ProviderKind.Steam, "1", null, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow, ProviderActivityAvailability.Complete),
            new ProviderActivityMetadata(oldProvider, ProviderKind.Steam, "2", null, DateTimeOffset.UtcNow.AddDays(-30), DateTimeOffset.UtcNow, ProviderActivityAvailability.Complete));
        var sessions = new FakeSessionStore(EndedSession(observedRecent, TimeSpan.FromHours(1), DateTimeOffset.UtcNow.AddDays(-2)));
        var library = await LoadWithActivity(
            [(providerRecent, "Provider", null), (observedRecent, "Observed", null), (oldProvider, "Old", null)],
            providerStore,
            sessions);

        library.SetSortKey("Recent");

        Assert.Equal(["Provider", "Observed", "Old"], library.VisibleItems.Select(item => item.Title));
    }

    [Fact]
    public async Task Advanced_provider_and_drive_filters_combine_with_and_and_reset_only_themselves()
    {
        var steamOnD = GameId.New();
        var steamOnC = GameId.New();
        var epicOnD = GameId.New();
        var library = await LoadAdvanced([
            (steamOnD, "Steam D", "Steam", @"D:\Games"),
            (steamOnC, "Steam C", "Steam", @"C:\Games"),
            (epicOnD, "Epic D", "Epic", @"D:\Games")]);

        library.ProviderFilterOptions.Single(option => option.Key == "Steam").IsSelected = true;
        library.ProviderFilterOptions.Single(option => option.Key == "Epic").IsSelected = true;
        library.DriveFilterOptions.Single(option => option.Key == "D:").IsSelected = true;

        Assert.Equal(2, library.ActiveAdvancedFilterCategoryCount);
        Assert.Equal(["Epic D", "Steam D"], library.VisibleItems.Select(item => item.Title));
        library.ResetAdvancedFilters();
        Assert.Equal(0, library.ActiveAdvancedFilterCategoryCount);
        Assert.Equal(3, library.VisibleItems.Count);
    }

    private static Task<LibraryViewModel> LoadAsync(
        params (string Title, long? Size)[] games) =>
        LoadAsync(games.Select(game => (GameId.New(), game.Title, game.Size)).ToArray(), null);

    private static Task<LibraryViewModel> LoadAsync(
        (GameId Id, string Title, long? Size) first,
        (GameId Id, string Title, long? Size) second,
        FakeAttentionService service) =>
        LoadAsync(new[] { first, second }, service);

    private static async Task<LibraryViewModel> LoadAsync(
        (GameId Id, string Title, long? Size)[] games,
        FakeAttentionService? service)
    {
        var now = DateTimeOffset.UtcNow;
        var snapshot = new LibrarySnapshot(
            games.Select(game => new LogicalGame(game.Id, game.Title, false, now, now)).ToArray(),
            games.Select(game => new GameInstallation(
                InstallationId.New(), game.Id, ProviderKind.Steam, "123456", 
                @"C:\Games", game.Size, true, true, now)).ToArray());
        var library = new LibraryViewModel(new StubLibraryStore(snapshot));
        if (service is not null)
            library.AttachAttentionService(service);
        await library.RefreshAsync(CancellationToken.None);
        return library;
    }

    private static async Task<LibraryViewModel> LoadWithActivity(
        (GameId Id, string Title, long? Size)[] games,
        FakeProviderActivityStore providerStore,
        FakeSessionStore sessionStore)
    {
        var now = DateTimeOffset.UtcNow;
        var snapshot = new LibrarySnapshot(
            games.Select(game => new LogicalGame(game.Id, game.Title, false, now, now)).ToArray(),
            games.Select(game => new GameInstallation(InstallationId.New(), game.Id, ProviderKind.Steam, "123456", @"C:\Games", game.Size, true, true, now)).ToArray());
        var library = new LibraryViewModel(new StubLibraryStore(snapshot), providerStore, sessionStore);
        await library.RefreshAsync(CancellationToken.None);
        return library;
    }

    private static async Task<LibraryViewModel> LoadAdvanced(
        (GameId Id, string Title, string Provider, string Path)[] games)
    {
        var now = DateTimeOffset.UtcNow;
        var snapshot = new LibrarySnapshot(
            games.Select(game => new LogicalGame(game.Id, game.Title, false, now, now)).ToArray(),
            games.Select(game => new GameInstallation(InstallationId.New(), game.Id, Enum.Parse<ProviderKind>(game.Provider), "123456", game.Path, null, true, true, now)).ToArray());
        var library = new LibraryViewModel(new StubLibraryStore(snapshot));
        await library.RefreshAsync(CancellationToken.None);
        return library;
    }

    private static GameSession EndedSession(GameId gameId, TimeSpan duration, DateTimeOffset? end = null)
    {
        var ended = end ?? DateTimeOffset.UtcNow;
        return new GameSession(Guid.NewGuid(), gameId.Value, ended - duration, ended, ended, SessionState.Ended,
            SessionEndReason.ProcessExited, SessionDetectionSource.ProcessMonitor, ended - duration, ended);
    }

    private sealed class FakeAttentionService(GameId gameId) : IAttentionService
    {
        public IReadOnlyList<AttentionItem> Items { get; } =
        [new(NotificationId.New(), gameId.Value, "Attention", "Action", AttentionSeverity.Warning, DateTimeOffset.UtcNow)];
        public event EventHandler? Changed { add { } remove { } }
        public Task<IReadOnlyList<AttentionItem>> GetActiveAsync(CancellationToken cancellationToken) => Task.FromResult(Items);
        public Task RefreshAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class StubLibraryStore(LibrarySnapshot snapshot) : ILibraryStore
    {
        public Task ApplySourceScanAsync(SourceScanResult result, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<LibrarySnapshot> LoadSnapshotAsync(CancellationToken cancellationToken) => Task.FromResult(snapshot);
    }

    private sealed class FakeProviderActivityStore(params ProviderActivityMetadata[] values) : IProviderActivityMetadataStore
    {
        public int ReadCount { get; private set; }
        public Task<IReadOnlyList<ProviderActivityMetadata>> GetAllAsync(CancellationToken cancellationToken)
        {
            ReadCount++;
            return Task.FromResult<IReadOnlyList<ProviderActivityMetadata>>(values);
        }
        public Task UpsertAsync(ProviderActivityMetadata metadata, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeSessionStore(params GameSession[] values) : ISessionStore
    {
        public int ReadCount { get; private set; }
        public Task<IReadOnlyList<GameSession>> GetByGameAsync(Guid gameId, CancellationToken cancellationToken)
        {
            ReadCount++;
            return Task.FromResult<IReadOnlyList<GameSession>>(values.Where(value => value.GameId == gameId).ToArray());
        }
        public Task UpsertAsync(GameSession session, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<GameSession?> GetAsync(Guid sessionId, CancellationToken cancellationToken) => Task.FromResult<GameSession?>(values.FirstOrDefault(value => value.SessionId == sessionId));
        public Task<IReadOnlyList<GameSession>> GetActiveAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<GameSession>>(values.Where(value => value.State == SessionState.Active).ToArray());
        public Task<IReadOnlyList<GameSession>> GetRecentAsync(int limit, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<GameSession>>(values.Take(limit).ToArray());
    }
}
