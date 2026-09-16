using Microsoft.Extensions.DependencyInjection;
using PlayStead.Core.Identity;
using PlayStead.Core.Library;
using PlayStead.Core.Notifications;
using PlayStead.Core.Persistence;
using PlayStead.Core.Scanning;
using PlayStead.Data.Database;
using PlayStead.Platform.Paths;
using PlayStead.UI.Bootstrap;

namespace PlayStead.UI.Tests.Bootstrap;

public sealed class NotificationRetentionStartupTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "PlayStead.Tests", "NotificationStartup", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Initialize_calls_purge_once()
    {
        var service = new RecordingNotificationCenter();
        var sut = new NotificationRetentionStartup(service);

        await sut.InitializeAsync(CancellationToken.None);

        Assert.Equal(1, service.PurgeCalls);
    }

    [Fact]
    public async Task Non_cancellation_purge_failure_is_swallowed()
    {
        var sut = new NotificationRetentionStartup(new RecordingNotificationCenter(new IOException("purge")));

        await sut.InitializeAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Cancellation_is_propagated()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var sut = new NotificationRetentionStartup(new RecordingNotificationCenter(new OperationCanceledException(cts.Token)));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sut.InitializeAsync(cts.Token));
    }

    [Fact]
    public void Host_resolves_notification_services_and_identity_coordinator()
    {
        var layout = UserDataLayout.FromRoot(_root);
        using var host = PlaySteadHost.Build(layout);

        Assert.NotNull(host.Services.GetRequiredService<INotificationStore>());
        Assert.NotNull(host.Services.GetRequiredService<INotificationCenterService>());
        Assert.NotNull(host.Services.GetRequiredService<IIdentityNotificationProducer>());
        Assert.NotNull(host.Services.GetRequiredService<ILocalIdentityResolutionCoordinator>());
        Assert.NotNull(host.Services.GetRequiredService<NotificationRetentionStartup>());
    }

    [Fact]
    public async Task Pipeline_invokes_retention_bootstrap_during_initialize()
    {
        Directory.CreateDirectory(_root);
        var options = new DatabaseOptions(Path.Combine(_root, "playstead.db"), Path.Combine(_root, "Backups"));
        var service = new RecordingNotificationCenter();
        var pipeline = new LocalStartupPipeline(
            new DatabaseInitializer(options),
            new DatabaseHealthChecker(options),
            new EmptyLibraryStore(),
            new LocalScanCoordinator([]),
            new NotificationRetentionStartup(service));

        await pipeline.InitializeAsync(CancellationToken.None);

        Assert.Equal(1, service.PurgeCalls);
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    private sealed class RecordingNotificationCenter(Exception? error = null) : INotificationCenterService
    {
        public int PurgeCalls { get; private set; }
        public Task<int> PurgeExpiredResolvedAsync(CancellationToken cancellationToken)
        {
            PurgeCalls++;
            if (error is not null) return Task.FromException<int>(error);
            return Task.FromResult(0);
        }
        public Task<NotificationRecord> PublishOrRefreshAsync(NotificationPublishRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<NotificationRecord> MarkReadAsync(NotificationId id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<NotificationRecord> ResolveAsync(NotificationId id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<int> GetActiveCountAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<NotificationRecord>> ListAsync(NotificationListFilter filter, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class EmptyLibraryStore : ILibraryStore
    {
        public Task ApplySourceScanAsync(SourceScanResult result, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<LibrarySnapshot> LoadSnapshotAsync(CancellationToken cancellationToken) => Task.FromResult(new LibrarySnapshot([], []));
    }

}
