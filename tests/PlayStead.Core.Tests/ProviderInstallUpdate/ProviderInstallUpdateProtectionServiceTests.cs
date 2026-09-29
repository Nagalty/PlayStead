using PlayStead.Core.Library;
using PlayStead.Core.LocalArtifacts;
using PlayStead.Core.Notifications;
using PlayStead.Core.Persistence;
using PlayStead.Core.ProviderInstallUpdate;

namespace PlayStead.Core.Tests.ProviderInstallUpdate;

public sealed class ProviderInstallUpdateProtectionServiceTests
{
    [Fact]
    public async Task Reliable_update_creates_one_preupdate_snapshot_and_notifies_after_success()
    {
        var game = GameId.New();
        var artifact = Artifact(game, GameLocalArtifactKind.SaveData);
        var snapshots = new FakeSnapshots();
        var windows = new FakeWindowsNotifications();
        var notifications = new FakeNotifications();
        var sut = new ProviderInstallUpdateProtectionService(
            new FakeProtection([Protected(artifact)]), snapshots, notifications, new FakeLibrary(game, "Dune: Awakening"), windows);
        var state = Update(game, "old", "new");

        await sut.ProtectBeforeUpdateAsync(state, CancellationToken.None);
        await sut.ProtectBeforeUpdateAsync(state, CancellationToken.None);

        var snapshot = Assert.Single(snapshots.Created);
        Assert.Equal(SnapshotReason.PreUpdate, snapshot.Reason);
        Assert.Single(windows.Messages);
        Assert.Empty(notifications.Published);
    }

    [Fact]
    public async Task Snapshot_failure_publishes_attention_without_success_notification()
    {
        var game = GameId.New();
        var notifications = new FakeNotifications();
        var windows = new FakeWindowsNotifications();
        var sut = new ProviderInstallUpdateProtectionService(
            new FakeProtection([Protected(Artifact(game, GameLocalArtifactKind.Configuration))]),
            new FakeSnapshots { Failure = new IOException("quota") },
            notifications,
            new FakeLibrary(game, "Dune: Awakening"),
            windows);

        await sut.ProtectBeforeUpdateAsync(Update(game, "old", "new"), CancellationToken.None);

        Assert.Empty(windows.Messages);
        Assert.Single(notifications.Published);
        Assert.Equal(NotificationProducer.LocalProtection, notifications.Published[0].Producer);
    }

    [Fact]
    public async Task Reconciliation_pipeline_protects_reliable_update_once_with_exact_silent_payload()
    {
        var game = GameId.New();
        var installation = new GameInstallation(
            new InstallationId(Guid.NewGuid()), game, ProviderKind.Steam, "1172710",
            @"G:\\SteamLibrary\\steamapps\\common\\Dune Awakening", null, true, true, DateTimeOffset.UtcNow);
        var artifact = Artifact(game, GameLocalArtifactKind.SaveData);
        var snapshots = new FakeSnapshots();
        var windows = new FakeWindowsNotifications();
        var notifications = new FakeNotifications();
        var protection = new ProviderInstallUpdateProtectionService(
            new FakeProtection([Protected(artifact)]), snapshots, notifications,
            new FakeLibrary(game, "Dune: Awakening"), windows);
        var source = new FakeSource();
        var reconciliation = new ProviderInstallUpdateStateReconciliationService([source], protection: protection);
        source.Values = [Update(game, "old", "alpha2")];

        await reconciliation.RefreshAsync([installation], CancellationToken.None);
        await reconciliation.RefreshAsync([installation], CancellationToken.None);

        Assert.Single(snapshots.Created);
        var message = Assert.Single(windows.Messages);
        Assert.Equal("Mise à jour détectée pour « Dune: Awakening »", message.Title);
        Assert.Equal("J’ai gardé une copie de tes sauvegardes avant qu’elle passe.", message.Message);
        Assert.Empty(notifications.Published);
    }

    private static LocalProtectionArtifactSummary Protected(GameLocalArtifact artifact) =>
        new(artifact with { BaselineStatus = LocalArtifactBaselineStatus.Unchanged }, LocalProtectionState.Protected, 1, DateTimeOffset.UtcNow);

    private static GameLocalArtifact Artifact(GameId game, GameLocalArtifactKind kind) =>
        new(game, kind, Path.Combine(Path.GetTempPath(), "known-artifact"), GameLocalArtifactSource.KnownConvention, GameLocalArtifactStatus.KnownAndExists, "rule", LocalArtifactBaselineStatus.Unchanged);

    private static ProviderInstallUpdateState Update(GameId game, string installed, string target) =>
        new(game, ProviderKind.Steam, "1172710", installed, target, ProviderInstallUpdateStatus.UpdateAvailable, null, null, null, null, null, null, DateTimeOffset.UtcNow);

    private sealed class FakeProtection(IReadOnlyList<LocalProtectionArtifactSummary> artifacts) : ILocalProtectionSetupService
    {
        public Task<LocalProtectionInventory> InspectAsync(IReadOnlyList<LocalProtectionGameContext> _, CancellationToken __) => Task.FromResult(new LocalProtectionInventory(artifacts));
        public Task<LocalProtectionRunResult> ProtectAsync(IReadOnlyList<LocalProtectionGameContext> _, CancellationToken __) => throw new NotSupportedException();
    }

    private sealed class FakeSnapshots : ILocalArtifactSnapshotService
    {
        public List<LocalArtifactSnapshot> Created { get; } = [];
        public Exception? Failure { get; init; }
        public Task<LocalArtifactSnapshot> CreateAsync(GameLocalArtifact artifact, CancellationToken _, SnapshotReason reason = SnapshotReason.Manual)
        {
            if (Failure is not null) throw Failure;
            var snapshot = new LocalArtifactSnapshot(Guid.NewGuid(), artifact.GameId, artifact.Kind, artifact.RuleIdentity!, "snapshot.zip", "sha256", "hash", 1, 1, DateTimeOffset.UtcNow, true, reason);
            Created.Add(snapshot);
            return Task.FromResult(snapshot);
        }
        public Task<IReadOnlyList<LocalArtifactSnapshot>> ListAsync(GameLocalArtifact _, CancellationToken __) => Task.FromResult<IReadOnlyList<LocalArtifactSnapshot>>([]);
        public Task DeleteAsync(LocalArtifactSnapshot _, CancellationToken __) => Task.CompletedTask;
    }

    private sealed class FakeWindowsNotifications : IWindowsSilentNotificationSink
    {
        public List<(string Title, string Message)> Messages { get; } = [];
        public Task ShowAsync(string title, string message, CancellationToken _) { Messages.Add((title, message)); return Task.CompletedTask; }
    }

    private sealed class FakeLibrary(GameId game, string title) : ILibraryStore
    {
        public Task ApplySourceScanAsync(PlayStead.Core.Scanning.SourceScanResult _, CancellationToken __) => throw new NotSupportedException();
        public Task<LibrarySnapshot> LoadSnapshotAsync(CancellationToken _) => Task.FromResult(new LibrarySnapshot([
            new LogicalGame(game, title, false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)
        ], []));
    }

    private sealed class FakeNotifications : INotificationCenterService
    {
        public List<NotificationPublishRequest> Published { get; } = [];
        public Task<NotificationRecord> PublishOrRefreshAsync(NotificationPublishRequest request, CancellationToken _) { Published.Add(request); return Task.FromResult<NotificationRecord>(null!); }
        public Task<NotificationRecord> MarkReadAsync(NotificationId _, CancellationToken __) => throw new NotSupportedException();
        public Task<NotificationRecord> ResolveAsync(NotificationId _, CancellationToken __) => throw new NotSupportedException();
        public Task<int> GetActiveCountAsync(CancellationToken _) => Task.FromResult(0);
        public Task<IReadOnlyList<NotificationRecord>> ListAsync(NotificationListFilter _, CancellationToken __) => Task.FromResult<IReadOnlyList<NotificationRecord>>([]);
        public Task<int> PurgeExpiredResolvedAsync(CancellationToken _) => Task.FromResult(0);
    }

    private sealed class FakeSource : IProviderInstallUpdateStateSource
    {
        public ProviderKind Provider => ProviderKind.Steam;
        public IReadOnlyList<ProviderInstallUpdateState> Values { get; set; } = [];
        public Task<IReadOnlyList<ProviderInstallUpdateState>> GetAsync(
            IReadOnlyCollection<GameInstallation> _, CancellationToken __) => Task.FromResult(Values);
    }
}
