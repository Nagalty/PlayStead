using System.Windows;
using System.Xml.Linq;
using PlayStead.Core.Notifications;
using PlayStead.UI.Attention;

namespace PlayStead.UI.Tests.Attention;

public sealed class AttentionFoundationTests
{
    [Fact]
    public async Task Service_projects_only_active_action_required_notifications()
    {
        var source = new FakeNotificationService();
        var service = new NotificationAttentionService(source);
        source.Records.Add(Record(NotificationPriority.Info, NotificationState.Unread, "info"));
        source.Records.Add(Record(NotificationPriority.ActionRequired, NotificationState.Resolved, "resolved"));
        Assert.Empty(await service.GetActiveAsync(default));
        source.Records.Add(Record(NotificationPriority.ActionRequired, NotificationState.Unread, "action"));
        Assert.Single(await service.GetActiveAsync(default));
    }

    [Fact]
    public async Task Service_orders_deterministically_and_deduplicates_source_records()
    {
        var source = new FakeNotificationService();
        var first = Record(NotificationPriority.ActionRequired, NotificationState.Unread, "first", DateTimeOffset.UtcNow.AddMinutes(-1));
        var second = Record(NotificationPriority.ActionRequired, NotificationState.Unread, "second", DateTimeOffset.UtcNow);
        source.Records.AddRange([first, first, second]);
        var service = new NotificationAttentionService(source);
        var items = await service.GetActiveAsync(default);
        Assert.Equal(["second", "first", "first"], items.Select(x => x.Title));
    }

    [Fact]
    public async Task Refresh_raises_changed_only_when_projection_changes_and_reprojects()
    {
        var source = new FakeNotificationService();
        var service = new NotificationAttentionService(source);
        var changes = 0;
        service.Changed += (_, _) => changes++;
        await service.RefreshAsync(default);
        await service.RefreshAsync(default);
        Assert.Equal(0, changes);
        source.Records.Add(Record(NotificationPriority.ActionRequired, NotificationState.Unread, "action"));
        await service.RefreshAsync(default);
        Assert.Equal(1, changes);
        source.Records.Clear();
        await service.RefreshAsync(default);
        Assert.Equal(2, changes);
        Assert.Empty(service.Items);
    }

    [Fact]
    public async Task View_model_exposes_empty_and_active_page_state()
    {
        var source = new FakeNotificationService();
        var service = new NotificationAttentionService(source);
        using var viewModel = new AttentionViewModel(service);
        await viewModel.RefreshAsync(default);
        Assert.False(viewModel.HasItems);
        source.Records.Add(Record(NotificationPriority.ActionRequired, NotificationState.Unread, "action"));
        await viewModel.RefreshAsync(default);
        Assert.True(viewModel.HasItems);
        Assert.Single(viewModel.Items);
    }

    [Fact]
    public void Page_uses_authoritative_items_and_existing_attention_route_contract()
    {
        var path = FindUiFile("Attention/AttentionView.xaml");
        var document = XDocument.Load(path);
        var xaml = document.ToString();
        Assert.Contains("ItemsSource=\"{Binding Items}\"", xaml);
        Assert.Contains("Message=\"{Binding EmptyMessage}\"", xaml);
        Assert.DoesNotContain("NotificationCenterService", xaml);
    }

    private static string FindUiFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "PlayStead.UI", relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }
        throw new FileNotFoundException(relativePath);
    }

    [Fact]
    public void Attention_foundation_has_no_home_dependency_and_is_derived()
    {
        Assert.DoesNotContain("Home", typeof(IAttentionService).AssemblyQualifiedName!);
        Assert.Equal(typeof(NotificationAttentionService).Assembly, typeof(IAttentionService).Assembly);
    }

    private static NotificationRecord Record(NotificationPriority priority, NotificationState state, string title, DateTimeOffset? updated = null) =>
        new(NotificationId.New(), NotificationProducer.IdentityResolution, Guid.NewGuid().ToString(), title, title, priority, state, title, title, null, updated ?? DateTimeOffset.UtcNow.AddMinutes(-2), updated ?? DateTimeOffset.UtcNow, null, state == NotificationState.Resolved ? updated : null);

    private sealed class FakeNotificationService : INotificationCenterService
    {
        public List<NotificationRecord> Records { get; } = [];
        public Task<NotificationRecord> PublishOrRefreshAsync(NotificationPublishRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<NotificationRecord> MarkReadAsync(NotificationId id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<NotificationRecord> ResolveAsync(NotificationId id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<int> GetActiveCountAsync(CancellationToken cancellationToken) => Task.FromResult(Records.Count(x => x.State != NotificationState.Resolved));
        public Task<IReadOnlyList<NotificationRecord>> ListAsync(NotificationListFilter filter, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<NotificationRecord>>(filter == NotificationListFilter.Active ? Records.Where(x => x.State != NotificationState.Resolved).ToArray() : Records.Where(x => x.State == NotificationState.Resolved).ToArray());
        public Task<int> PurgeExpiredResolvedAsync(CancellationToken cancellationToken) => Task.FromResult(0);
    }
}
