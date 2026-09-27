using PlayStead.Core.Library;
using PlayStead.Core.Notifications;
using PlayStead.Core.Persistence;
using PlayStead.Core.ProviderInstallUpdate;
using PlayStead.Core.Scanning;

namespace PlayStead.Core.Tests.Notifications;

public sealed class NotificationAttentionInstallUpdateTests
{
    private readonly GameId _gameId = new(Guid.NewGuid());

    [Fact]
    public async Task Update_available_projects_one_game_attention_item()
    {
        var service = CreateAttentionService(ProviderInstallUpdateStatus.UpdateAvailable);

        var items = await service.GetActiveAsync(CancellationToken.None);

        var item = Assert.Single(items);
        Assert.Equal("Helldivers 2 — mise à jour disponible", item.Title);
        Assert.Equal("Une mise à jour Steam est prête à être téléchargée.", item.Message);
        Assert.Equal(_gameId.Value, item.GameId);
    }

    [Theory]
    [InlineData(ProviderInstallUpdateStatus.UpToDate)]
    [InlineData(ProviderInstallUpdateStatus.Unknown)]
    [InlineData(ProviderInstallUpdateStatus.VersionMismatch)]
    public async Task Non_actionable_states_project_no_item(ProviderInstallUpdateStatus status)
    {
        var service = CreateAttentionService(status);

        Assert.Empty(await service.GetActiveAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Refresh_is_deduplicated_and_existing_notification_is_preserved()
    {
        var notifications = new FakeNotificationService();
        notifications.Records.Add(Notification("Existing"));
        var updates = new FakeUpdateSource();
        var stateService = CreateStateService(updates);
        var service = new NotificationAttentionService(notifications, stateService, new FakeLibraryStore(_gameId));
        updates.Values = [State(ProviderInstallUpdateStatus.UpdateAvailable)];
        await stateService.RefreshAsync([Installation()], CancellationToken.None);

        var first = await service.GetActiveAsync(CancellationToken.None);
        var second = await service.GetActiveAsync(CancellationToken.None);

        Assert.Equal(2, first.Count);
        Assert.Equal(first, second);
        Assert.Contains(first, x => x.Title == "Existing");
        Assert.Single(first, x => x.Title.Contains("mise à jour disponible", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Update_available_to_up_to_date_removes_item()
    {
        var updates = new FakeUpdateSource();
        var stateService = CreateStateService(updates);
        var service = new NotificationAttentionService(new FakeNotificationService(), stateService, new FakeLibraryStore(_gameId));
        updates.Values = [State(ProviderInstallUpdateStatus.UpdateAvailable)];
        await stateService.RefreshAsync([Installation()], CancellationToken.None);
        Assert.Single(await service.GetActiveAsync(CancellationToken.None));

        updates.Values = [State(ProviderInstallUpdateStatus.UpToDate)];
        await stateService.RefreshAsync([Installation()], CancellationToken.None);

        Assert.Empty(await service.GetActiveAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Update_available_to_downloading_replaces_same_logical_item_without_duplicate()
    {
        var updates = new FakeUpdateSource();
        var stateService = CreateStateService(updates);
        var service = new NotificationAttentionService(new FakeNotificationService(), stateService, new FakeLibraryStore(_gameId));
        updates.Values = [State(ProviderInstallUpdateStatus.UpdateAvailable)];
        await stateService.RefreshAsync([Installation()], CancellationToken.None);
        var first = Assert.Single(await service.GetActiveAsync(CancellationToken.None));

        updates.Values = [State(ProviderInstallUpdateStatus.Downloading)];
        await stateService.RefreshAsync([Installation()], CancellationToken.None);
        var second = Assert.Single(await service.GetActiveAsync(CancellationToken.None));

        Assert.Equal(first.AttentionId, second.AttentionId);
    }

    private NotificationAttentionService CreateAttentionService(ProviderInstallUpdateStatus status)
    {
        var updates = new FakeUpdateSource { Values = [State(status)] };
        var stateService = CreateStateService(updates);
        stateService.RefreshAsync([Installation()], CancellationToken.None).GetAwaiter().GetResult();
        return new NotificationAttentionService(new FakeNotificationService(), stateService, new FakeLibraryStore(_gameId));
    }

    private static ProviderInstallUpdateStateReconciliationService CreateStateService(FakeUpdateSource source) =>
        new([source]);

    private ProviderInstallUpdateState State(ProviderInstallUpdateStatus status) =>
        new(_gameId, ProviderKind.Steam, "553850", "25327279", "25480438", status,
            status == ProviderInstallUpdateStatus.Downloading ? 100 : 86_653_644,
            status == ProviderInstallUpdateStatus.Downloading ? 10 : 0,
            null, null, null, 6, DateTimeOffset.UtcNow);

    private GameInstallation Installation() =>
        new(InstallationId.New(), _gameId, ProviderKind.Steam, "553850", "G:\\SteamLibrary\\common\\Helldivers 2", null, true, true, DateTimeOffset.UtcNow);

    private static NotificationRecord Notification(string title) =>
        new(NotificationId.New(), NotificationProducer.IdentityResolution, Guid.NewGuid().ToString(), title, title,
            NotificationPriority.ActionRequired, NotificationState.Unread, title, title, null,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, null);

    private sealed class FakeUpdateSource : IProviderInstallUpdateStateSource
    {
        public ProviderKind Provider => ProviderKind.Steam;
        public IReadOnlyList<ProviderInstallUpdateState> Values { get; set; } = [];
        public Task<IReadOnlyList<ProviderInstallUpdateState>> GetAsync(IReadOnlyCollection<GameInstallation> installations, CancellationToken cancellationToken) =>
            Task.FromResult(Values);
    }

    private sealed class FakeLibraryStore(GameId gameId) : ILibraryStore
    {
        public Task ApplySourceScanAsync(SourceScanResult result, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<LibrarySnapshot> LoadSnapshotAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new LibrarySnapshot([
                new LogicalGame(gameId, "Helldivers 2", false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)
            ], []));
    }

    private sealed class FakeNotificationService : INotificationCenterService
    {
        public List<NotificationRecord> Records { get; } = [];
        public Task<NotificationRecord> PublishOrRefreshAsync(NotificationPublishRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<NotificationRecord> MarkReadAsync(NotificationId id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<NotificationRecord> ResolveAsync(NotificationId id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<int> GetActiveCountAsync(CancellationToken cancellationToken) => Task.FromResult(Records.Count);
        public Task<IReadOnlyList<NotificationRecord>> ListAsync(NotificationListFilter filter, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<NotificationRecord>>(Records);
        public Task<int> PurgeExpiredResolvedAsync(CancellationToken cancellationToken) => Task.FromResult(0);
    }
}
